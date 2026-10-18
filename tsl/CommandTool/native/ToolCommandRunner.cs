using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria.Chat;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.Commanding;
using UnifierTSL.Commanding;
using UnifierTSL.Commanding.Endpoints;
using UnifierTSL.Servers;

namespace CommandTool;

/// <summary>以真实玩家执行一次自动动作；授权和聊天静默均只存在于本次 ExecutionContext。</summary>
internal sealed class ToolCommandRunner
{
    private static readonly AsyncLocal<ChatScope?> Chat = new();
    private static readonly AsyncLocal<PermissionScope?> Permission = new();
    private readonly ConcurrentDictionary<PermissionScope, byte> _active = new();
    private readonly object _gate = new();
    private Hook? _playerPermissionHook;
    private Hook? _executorPermissionHook;
    private readonly List<Hook> _identityHooks = [];
    private volatile bool _bound;

    private delegate bool PlayerPermissionOrig(TSPlayer self, string permission);
    private delegate bool PlayerPermissionDetour(PlayerPermissionOrig orig, TSPlayer self, string permission);
    // CommandExecutor 是 readonly struct；实例方法隐藏的 this 必须按引用传递。
    private delegate bool ExecutorPermissionOrig(ref CommandExecutor self, string permission);
    private delegate bool ExecutorPermissionDetour(ExecutorPermissionOrig orig, ref CommandExecutor self, string permission);
    private delegate TSPlayer ExecutorPlayerOrig(ref CommandExecutor self);
    private delegate TSPlayer ExecutorPlayerDetour(ExecutorPlayerOrig orig, ref CommandExecutor self);
    private delegate Terraria.Player NativePlayerOrig(TSPlayer self);
    private delegate Terraria.Player NativePlayerDetour(NativePlayerOrig orig, TSPlayer self);
    private delegate ServerContext CurrentServerOrig(TSPlayer self);
    private delegate ServerContext CurrentServerDetour(CurrentServerOrig orig, TSPlayer self);

    public void Bind()
    {
        lock (_gate)
        {
            if (_bound) return;
            try
            {
                _playerPermissionHook = new Hook(RequiredPermissionMethod(typeof(TSPlayer)),
                    (PlayerPermissionDetour)CheckPlayerPermission);
                _executorPermissionHook = new Hook(RequiredPermissionMethod(typeof(CommandExecutor)),
                    (ExecutorPermissionDetour)CheckExecutorPermission);
                _identityHooks.Add(new Hook(typeof(CommandExecutor).GetProperty(nameof(CommandExecutor.InGamePlayer))!.GetMethod!,
                    (ExecutorPlayerDetour)GetExecutorPlayer));
                _identityHooks.Add(new Hook(typeof(CommandExecutor).GetProperty(nameof(CommandExecutor.Player))!.GetMethod!,
                    (ExecutorPlayerDetour)GetExecutorPlayer));
                _identityHooks.Add(new Hook(typeof(TSPlayer).GetProperty(nameof(TSPlayer.TPlayer))!.GetMethod!,
                    (NativePlayerDetour)GetNativePlayer));
                _identityHooks.Add(new Hook(typeof(TSPlayer).GetMethod(nameof(TSPlayer.GetCurrentServer))!,
                    (CurrentServerDetour)GetCurrentServer));
                On.Terraria.Chat.ChatHelperSystemContext.SendChatMessageToClient += SendToClient;
                On.Terraria.Chat.ChatHelperSystemContext.SendChatMessageToClientAs += SendToClientAs;
                On.Terraria.Chat.ChatHelperSystemContext.BroadcastChatMessage += Broadcast;
                On.Terraria.Chat.ChatHelperSystemContext.BroadcastChatMessageAs += BroadcastAs;
                _bound = true;
            }
            catch
            {
                Unbind();
                throw; // 无法完整绑定时不允许以“已静默/已授权”状态运行。
            }
        }
    }

    public void Unbind()
    {
        lock (_gate)
        {
            _bound = false;
            foreach (var scope in _active.Keys) scope.Active = false;
            On.Terraria.Chat.ChatHelperSystemContext.SendChatMessageToClient -= SendToClient;
            On.Terraria.Chat.ChatHelperSystemContext.SendChatMessageToClientAs -= SendToClientAs;
            On.Terraria.Chat.ChatHelperSystemContext.BroadcastChatMessage -= Broadcast;
            On.Terraria.Chat.ChatHelperSystemContext.BroadcastChatMessageAs -= BroadcastAs;
            foreach (var hook in _identityHooks.AsEnumerable().Reverse()) hook.Dispose();
            _identityHooks.Clear();
            try { _executorPermissionHook?.Dispose(); }
            finally
            {
                _executorPermissionHook = null;
                _playerPermissionHook?.Dispose();
                _playerPermissionHook = null;
            }
        }
    }

    private static MethodInfo RequiredPermissionMethod(Type type) =>
        type.GetMethod(nameof(TSPlayer.HasPermission), BindingFlags.Instance | BindingFlags.Public,
            null, [typeof(string)], null) ?? throw new MissingMethodException(type.FullName, "HasPermission(string)");

    public async Task<bool> ExecuteAsync(TSPlayer player, ServerContext server, ToolAction action, Func<bool> isCurrent)
    {
        if (!_bound || player is null || server is null || action is null || isCurrent is null) return false;
        if (!ToolText.TryParse(action.Command, out var root, out var parameters) || ToolText.IsManagement(root)) return false;
        if (!isCurrent() || !ReferenceEquals(player.GetCurrentServer(), server)) return false;
        var scope = new PermissionScope(this, player, player.TPlayer, server, action.AsAdmin,
            action.GrantPermission?.Trim() ?? "", isCurrent);
        if (!scope.IsCurrent()) return false;
        // 先词法拆分，再替换参数中的名字，最后逐 token 转义；名字不能注入额外参数/指令。
        parameters = parameters.Select(p => p.Replace("$name", player.Name, StringComparison.Ordinal)
            .Replace("{name}", player.Name, StringComparison.Ordinal)).ToList();
        // 限定用户组包括父组链，仍使用真实组，不因为 AsAdmin 而绕过。
        if (!MatchesGroup(player.Group, action.RequireGroup)) return false;
        var previous = Permission.Value;
        using var silence = SuppressChat();
        Permission.Value = scope;
        _active.TryAdd(scope, 0);
        try
        {
            if (!scope.IsCurrent()) return false;
            var executor = new CommandExecutor(server, checked((byte)player.Index));
            var legacy = Commands.GetRegisteredLegacyCommands()
                .Where(c => c.Names.Any(n => string.Equals(n, root, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (legacy.Length != 0)
            {
                // 只对明确注册的 Legacy root 使用 Run；绝不进入 HandleCommand 的审核广播链。
                // 同名 Legacy 注册选择目录顺序中首个有权限的处理器，固定动作最多进入一个处理器。
                // 执行后抛错也不重放到下一个同名处理器或 V2 dispatcher。
                var command = legacy.FirstOrDefault(c => c.CanRun(executor));
                if (command is null || !scope.IsCurrent()) return false;
                string message = root + (parameters.Count == 0 ? "" : " " + string.Join(" ", parameters.Select(ToolText.Quote)));
                return command.Run(message, silent: true, executor, parameters);
            }
            var request = new CommandDispatchRequest
            {
                EndpointId = new CommandEndpointId("tshock-player"),
                ExecutionContext = new TSExecutionContext(executor, true, new CommandInvocationTarget("player")),
                RawInput = Commands.Specifier + root + (parameters.Count == 0 ? "" : " " + string.Join(" ", parameters.Select(ToolText.Quote))),
                CommandPrefixes = new[] { Commands.Specifier, Commands.SilentSpecifier }
            };
            // 不调用 OutcomeWriter/AuditDispatch；仍用 scoped ChatHelper hooks 拦截指令内部直接发送与广播。
            // 不在世界线程同步等待；ExecutionContext 会把本次两个 scope 传入异步 continuation。
            var result = await CommandDispatchCoordinator.DispatchAsync(request).ConfigureAwait(false);
            return result.Matched && result.Handled;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[CommandTool] 自动指令 {root} 执行失败（未重试）: {ex}");
            return false;
        }
        finally
        {
            // 子 Task 即使复制了 ExecutionContext，动作结束后也不得继续继承临时授权。
            scope.Active = false;
            _active.TryRemove(scope, out _);
            Permission.Value = previous;
        }
    }

    internal static IDisposable SuppressChat()
    {
        var scope = new ChatScope(Chat.Value);
        Chat.Value = scope;
        return scope;
    }

    private static bool IsChatSuppressed() => Chat.Value is not null;

    private sealed class ChatScope(ChatScope? previous) : IDisposable
    {
        internal readonly ChatScope? Previous = previous;
        public void Dispose()
        {
            // 只还原当前调用链；已继承 ExecutionContext 的延迟输出仍属于该自动指令而保持静默。
            // 没有全局计时器/共享玩家开关，独立正常聊天上下文完全不受影响。
            if (ReferenceEquals(Chat.Value, this)) Chat.Value = Previous;
        }
    }

    internal static bool MatchesGroup(Group? group, string? required)
    {
        if (string.IsNullOrWhiteSpace(required)) return true;
        var visited = new HashSet<Group>(ReferenceEqualityComparer.Instance);
        for (; group is not null && visited.Add(group); group = group.Parent)
            if (string.Equals(group.Name, required.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private sealed class PermissionScope(ToolCommandRunner owner, TSPlayer player, Terraria.Player nativePlayer, ServerContext server,
        bool admin, string grant, Func<bool> current)
    {
        internal volatile bool Active = true;
        internal TSPlayer Player => player;
        internal Terraria.Player NativePlayer => nativePlayer;
        internal ServerContext Server => server;
        internal bool Matches(in CommandExecutor executor) => !executor.IsRest && executor.IsClient
            && executor.UserId == player.Index && ReferenceEquals(executor.SourceServer, server);
        internal void EnsureCurrent()
        {
            if (!IdentityCurrent()) throw new OperationCanceledException("CommandTool 自动指令的玩家身份/世界/配置范围已失效。");
        }
        internal bool IsCurrent() => Active && IdentityCurrent();
        private bool IdentityCurrent()
        {
            if (!owner._bound || player.Index < 0 || player.Index >= TShock.Players.Length
                || !ReferenceEquals(TShock.Players[player.Index], player)) return false;
            // current 由父捕获 socket/world/nativePlayer/配置代数，不回调本文件拦截的 getter。
            // 动作结束只撤额外权限；仍属原连接的迟发普通逻辑可使用捕获身份，且继续静默。
            try { return current(); }
            catch { return false; }
        }
        internal bool Allows(string permission)
        {
            if (!IsCurrent()) return false;
            if (admin) return true;
            if (string.IsNullOrWhiteSpace(grant) || string.IsNullOrWhiteSpace(permission)) return false;
            return grant == "*" || string.Equals(grant, permission, StringComparison.OrdinalIgnoreCase)
                || (grant.EndsWith(".*", StringComparison.Ordinal)
                    && permission.StartsWith(grant[..^1], StringComparison.OrdinalIgnoreCase));
        }
    }

    private static bool CheckPlayerPermission(PlayerPermissionOrig orig, TSPlayer self, string permission)
    {
        var scope = Permission.Value;
        if (scope is null || !ReferenceEquals(scope.Player, self)) return orig(self, permission);
        scope.EnsureCurrent(); // 失效后不能 fallback orig 去读取复用 slot 的新玩家。
        return scope.Allows(permission) || orig(self, permission);
    }
    private static bool CheckExecutorPermission(ExecutorPermissionOrig orig, ref CommandExecutor self, string permission)
    {
        var scope = Permission.Value;
        if (scope is null || !scope.Matches(in self)) return orig(ref self, permission);
        scope.EnsureCurrent();
        return scope.Allows(permission) || orig(ref self, permission);
    }

    private static TSPlayer GetExecutorPlayer(ExecutorPlayerOrig orig, ref CommandExecutor self)
    {
        var scope = Permission.Value;
        if (scope is null || !scope.Matches(in self)) return orig(ref self);
        scope.EnsureCurrent();
        return scope.Player;
    }
    private static Terraria.Player GetNativePlayer(NativePlayerOrig orig, TSPlayer self)
    {
        var scope = Permission.Value;
        if (scope is null || !ReferenceEquals(scope.Player, self)) return orig(self);
        scope.EnsureCurrent();
        return scope.NativePlayer;
    }
    private static ServerContext GetCurrentServer(CurrentServerOrig orig, TSPlayer self)
    {
        var scope = Permission.Value;
        if (scope is null || !ReferenceEquals(scope.Player, self)) return orig(self);
        scope.EnsureCurrent();
        return scope.Server;
    }

    private static void SendToClient(On.Terraria.Chat.ChatHelperSystemContext.orig_SendChatMessageToClient orig,
        ChatHelperSystemContext self, NetworkText text, Color color, int playerId)
    { if (!IsChatSuppressed()) orig(self, text, color, playerId); }
    private static void SendToClientAs(On.Terraria.Chat.ChatHelperSystemContext.orig_SendChatMessageToClientAs orig,
        ChatHelperSystemContext self, byte author, NetworkText text, Color color, int playerId)
    { if (!IsChatSuppressed()) orig(self, author, text, color, playerId); }
    private static void Broadcast(On.Terraria.Chat.ChatHelperSystemContext.orig_BroadcastChatMessage orig,
        ChatHelperSystemContext self, NetworkText text, Color color, int excludedPlayer)
    { if (!IsChatSuppressed()) orig(self, text, color, excludedPlayer); }
    private static void BroadcastAs(On.Terraria.Chat.ChatHelperSystemContext.orig_BroadcastChatMessageAs orig,
        ChatHelperSystemContext self, byte author, NetworkText text, Color color, int excludedPlayer)
    { if (!IsChatSuppressed()) orig(self, author, text, color, excludedPlayer); }
}
