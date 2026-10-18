using System.Collections.Immutable;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using TrProtocol.NetPackets;
using TShockAPI;
using UnifierTSL;
using UnifierTSL.Events.Core;
using UnifierTSL.Events.Handlers;
using UnifierTSL.Module;
using UnifierTSL.Plugins;
using UnifierTSL.Servers;

[assembly: CoreModule]

namespace BoundaryTransfer;

[PluginMetadata("BoundaryTransfer", "1.2.0", "Pigeon-X", "碰边向外原生跨图，保持接触高度直达对侧边界，无传送特效；海边保护只沿地表海岸修正，不进水/地底/地狱，不改出生点。")]
public sealed partial class BoundaryTransferPlugin : BasePlugin
{
    private const string AdminPermission = "boundarytransfer.admin";
    private readonly object _gate = new();
    private readonly Dictionary<int, PlayerSession> _sessions = [];
    private readonly Dictionary<ServerContext, long> _nextPoll = [];
    private volatile BoundarySettings _settings = BoundarySettings.Default;
    private volatile bool _running;
    private volatile bool _stopping;
    private IPluginConfigHandle<BoundaryTransferConfig>? _config;
    private Command? _command;
    private Task? _shutdownTask;

    public override int InitializationOrder => TShock.Order + 1;

    public override async Task InitializeAsync(IPluginConfigRegistrar configRegistrar,
        ImmutableArray<PluginInitInfo> priorInitializations, CancellationToken cancellationToken = default)
    {
        foreach (var item in priorInitializations)
            if (item.Plugin.Name == "TShock")
                await item.InitializationTask.ConfigureAwait(false);
        _config = configRegistrar.CreateConfigRegistration<BoundaryTransferConfig>("BoundaryTransfer.json", ConfigFormat.NewtonsoftJson)
            .WithDefault(static () => new())
            .OnDeserializationFailure(DeserializationFailureHandling.ReturnNewInstance, false)
            .TriggerReloadOnExternalChange(true).Complete();
        _config.OnChangedAsync += OnConfigChanged;
        ApplyConfig(await _config.RequestAsync(cancellationToken: cancellationToken).ConfigureAwait(false) ?? new());
        _command = new Command(AdminPermission, OnCommand, "边界", "boundarytransfer", "borders")
        {
            AllowServer = true,
            DoLog = false
        };
        // 不加入公共指令目录：/help、目录查询和基于目录的补全均不会列出这些别名。
        // DoLog=false只省略参数，仍会广播指令名，因此由私密消息入口执行，而非HandleCommand。
        _running = true;
        UnifierApi.EventHub.Chat.MessageEvent.Register(OnHiddenCommandMessage, HandlerPriority.Highest);
        UnifierApi.EventHub.Game.PostUpdate.Register(OnUpdate, HandlerPriority.Lowest);
        UnifierApi.EventHub.Coordinator.PostServerTransfer.Register(OnTransferred, HandlerPriority.Lowest);
        UnifierApi.EventHub.Netplay.LeaveEvent.Register(OnLeave, HandlerPriority.Normal);
        NetPacketHandler.Register<SpawnPlayer>(OnSpawnPacket, HandlerPriority.Highest);
        NetPacketHandler.Register<PlayerControls>(OnControlsPacket, HandlerPriority.Highest);
        UnifierApi.Logger.Info($"[BoundaryTransfer] 已加载。世界排列：{string.Join(" → ", _settings.Worlds)}；配置：{_config.FilePath}");
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) return _shutdownTask ??= ShutdownCoreAsync();
    }

    private async Task ShutdownCoreAsync()
    {
        _stopping = true;
        lock (_gate)
            foreach (var session in _sessions.Values)
                if (session.Pending is { Routing: false }) session.Pending = null;
        // 不同步阻塞世界线程；保持钩子让已开始的原生切换收尾。
        long drainUntil = Environment.TickCount64 + 6000;
        while (Environment.TickCount64 < drainUntil)
        {
            lock (_gate)
                if (_sessions.Values.All(s => s.Pending is null)) break;
            await Task.Delay(50).ConfigureAwait(false);
        }
        TransferTicket[] unfinished;
        lock (_gate) unfinished = [.. _sessions.Values.Select(s => s.Pending).OfType<TransferTicket>()];
        foreach (var ticket in unfinished)
        {
            // 只关闭票据捕获的旧socket，不能通过复用的slot踢掉新登录玩家。
            if (ticket.Session.Socket is not Terraria.Net.Sockets.ISocket socket || !socket.IsConnected()) continue;
            UnifierApi.Logger.Warning($"[BoundaryTransfer] 卸载时 {ticket.Session.Player.name} 的原生切换尚未安全收尾，关闭捕获的原连接；该玩家需要重连。");
            try { socket.Close(); }
            catch (Exception ex) { UnifierApi.Logger.Warning("[BoundaryTransfer] 关闭未完成切换原连接失败：" + ex.Message); }
        }
        _running = false;
        UnifierApi.EventHub.Chat.MessageEvent.UnRegister(OnHiddenCommandMessage);
        UnifierApi.EventHub.Game.PostUpdate.UnRegister(OnUpdate);
        UnifierApi.EventHub.Coordinator.PostServerTransfer.UnRegister(OnTransferred);
        UnifierApi.EventHub.Netplay.LeaveEvent.UnRegister(OnLeave);
        NetPacketHandler.UnRegister<SpawnPlayer>(OnSpawnPacket);
        NetPacketHandler.UnRegister<PlayerControls>(OnControlsPacket);
        _command = null;
        if (_config is not null)
            _config.OnChangedAsync -= OnConfigChanged;
        _config = null;
        lock (_gate) { _sessions.Clear(); _nextPoll.Clear(); }
    }

    public override async ValueTask DisposeAsync(bool isDisposing)
    {
        await ShutdownAsync().ConfigureAwait(false);
        await base.DisposeAsync(isDisposing).ConfigureAwait(false);
    }

    private ValueTask<bool> OnConfigChanged(IPluginConfigHandle<BoundaryTransferConfig> handle, BoundaryTransferConfig? config)
    {
        ApplyConfig(config ?? new());
        return ValueTask.FromResult(false);
    }

    internal bool ApplyConfig(BoundaryTransferConfig config)
    {
        try
        {
            BoundarySettings next = BoundarySettings.FromConfig(config);
            _settings = next;
            lock (_gate)
            {
                _nextPoll.Clear();
                // 已启动原生切换的操作仍完成安全落地；尚在预检的操作可以取消。
                foreach (var session in _sessions.Values)
                    if (session.Pending is { Routing: false }) session.Pending = null;
            }
            return true;
        }
        catch (ArgumentException ex)
        {
            UnifierApi.Logger.Warning("[BoundaryTransfer] 配置无效，保留上次有效设置：" + ex.Message);
            return false;
        }
    }

    private void OnUpdate(ref ReadonlyNoCancelEventArgs<ServerEvent> args)
    {
        if (!_running) return;
        var server = args.Content.Server;
        long now = Environment.TickCount64;
        ProcessPending(server, now);
        var settings = _settings;
        if (_stopping || !settings.Enabled || settings.Worlds.Length < 2) return;
        lock (_gate)
        {
            if (_nextPoll.TryGetValue(server, out long next) && now < next) return;
            _nextPoll[server] = now + settings.PollMs;
        }
        if (!server.IsRunning) return;
        for (int slot = 0; slot < TShock.Players.Length && slot < server.Main.player.Length; slot++)
        {
            if (!TryGetLivePlayer(server, slot, out var player, out var tsPlayer, out var socket)) continue;
            PlayerSession session;
            lock (_gate)
            {
                if (!_sessions.TryGetValue(slot, out session!) || !session.Matches(player, tsPlayer, socket))
                    _sessions[slot] = session = new(player, tsPlayer, socket);
                if (session.Pending is not null) continue;
                if (!session.Armed)
                {
                    if (now >= session.NextAllowed && BoundaryContact.IsInterior(player.position.X, player.width,
                            server.Main.leftWorld, server.Main.rightWorld))
                        session.Armed = true;
                    continue;
                }
                if (now < session.NextAllowed) continue;
            }
            var side = BoundaryContact.Side(player.position.X, player.width,
                server.Main.leftWorld, server.Main.rightWorld, settings.ContactTolerance);
            if (side is null || !BoundaryContact.IsOutward(side.Value, player.velocity.X,
                    player.controlLeft, player.controlRight)) continue;
            if (player.gravDir != 1f)
            {
                Warn(session, "当前为倒置重力，无法保证脚底安全着陆；恢复正常重力后再尝试。", now);
                continue;
            }
            string? neighbor = BoundaryPlanner.Neighbor(settings, server.Name, side.Value);
            if (neighbor is null) continue;
            ServerContext? target = FindWorld(neighbor);
            if (target is null || !target.IsRunning)
            {
                Warn(session, $"相邻世界“{neighbor}”未运行，本次不切换。", now);
                continue;
            }
            if (target.Main.maxTilesX != server.Main.maxTilesX || target.Main.maxTilesY != server.Main.maxTilesY)
            {
                Warn(session, "两张地图尺寸不同，UTSL 原生无缝切换要求同尺寸，本次不切换。", now);
                continue;
            }
            var ticket = new TransferTicket(slot, session, server, target, side.Value, settings, now);
            lock (_gate)
            {
                if (!_running || _stopping || !ReferenceEquals(settings, _settings)
                    || !_sessions.TryGetValue(slot, out var current) || !ReferenceEquals(current, session)
                    || session.Pending is not null) continue;
                session.Pending = ticket;
            }
            // 不预检地形：立即按接触高度直接切到对侧边界。
            BeginNativeTransfer(ticket);
        }
    }

    private void BeginNativeTransfer(TransferTicket ticket)
    {
        if (!IsCurrent(ticket) || ticket.Routing || Environment.TickCount64 >= ticket.Deadline) return;
        if (!SameWorld(ticket.Source, ticket.SourceWorld) || !SameWorld(ticket.Target, ticket.TargetWorld)
            || !TryGetLivePlayer(ticket.Source, ticket.Slot, out var player, out var ts, out var socket)
            || !ticket.Session.Matches(player, ts, socket))
        { Fail(ticket, "切换前玩家或世界已变化，本次不切换。"); return; }
        if (BoundaryContact.Side(player.position.X, player.width, ticket.Source.Main.leftWorld,
                ticket.Source.Main.rightWorld, ticket.Settings.ContactTolerance) != ticket.ExitSide
            || !BoundaryContact.IsOutward(ticket.ExitSide, player.velocity.X, player.controlLeft, player.controlRight)
            || player.gravDir != 1f)
        { Fail(ticket, "玩家已离开边界，请重新走到边界。"); return; }
        // 默认保持脚底高度直达对侧边界；只有目标点在海里/无地面时才做海岸保护。
        var direct = BoundaryPlanner.Direct(ticket.EntrySide, player.Bottom.Y, player.width, player.height,
            new BoundaryBounds(ticket.Target.Main.leftWorld, ticket.Target.Main.rightWorld,
                ticket.Target.Main.topWorld, ticket.Target.Main.bottomWorld));
        if (!direct.Success) { Fail(ticket, direct.Error); return; }
        var landing = direct;
        var map = new TerrariaLandingMap(ticket.Target);
        if (ticket.Settings.CoastGuard && !RuntimeSafe(ticket.Target, map, direct.Spot, player, ticket.Settings.HazardPadding))
        {
            var coast = BoundaryPlanner.Coastal(map, ticket.EntrySide, player.width, player.height,
                ticket.Settings.CoastSearchX, ticket.Settings.CoastSearchY,
                (int)ticket.Target.Main.worldSurface, (int)ticket.Target.Main.rockLayer,
                ticket.Settings.HazardPadding, ticket.Settings.MaxCandidates);
            if (!coast.Success) { Fail(ticket, coast.Error); return; }
            landing = coast;
        }
        ticket.Spot = landing.Spot;
        var client = UnifiedServerCoordinator.globalClients[ticket.Slot];
        if (!ticket.Target.CheckPlayerCanJoinIn(player, client, out var reason))
        { Fail(ticket, "相邻世界拒绝进入：" + reason?.ToString()); return; }
        lock (_gate)
        {
            // 其他插件的CanJoin事件可能触发重载/卸载：校验之后必须再次原子认领。
            if (!_running || _stopping || !ReferenceEquals(ticket.Session.Pending, ticket)
                || ticket.Routing || Environment.TickCount64 >= ticket.Deadline) return;
            ticket.Deadline = Environment.TickCount64 + ticket.Settings.TimeoutMs;
            ticket.Routing = true;
        }
        try
        {
            // 唯一的跨服入口：不改世界spawnTile、不设置玩家SpawnX/Y、不调用Spawn伪造落点。
            UnifiedServerCoordinator.TransferPlayerToServer((byte)ticket.Slot, ticket.Target);
            if (UnifiedServerCoordinator.GetClientCurrentlyServer(ticket.Slot) != ticket.Target)
            { Fail(ticket, "原生切换未完成（可能被其他插件取消），仍留在原世界。"); return; }
            ticket.NativeComplete = true;
            ticket.NeedsLanding = true;
        }
        catch (Exception ex)
        {
            if (UnifiedServerCoordinator.GetClientCurrentlyServer(ticket.Slot) == ticket.Target)
            { ticket.NativeComplete = true; ticket.NeedsLanding = true; }
            else Fail(ticket, "原生切换失败：" + ex.Message);
        }
    }

    private void OnTransferred(ref ReadonlyNoCancelEventArgs<PostServerTransferEvent> args)
    {
        var e = args.Content;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(e.Who, out var session) || session.Pending is not { } ticket) return;
            if (ticket.Routing && ticket.ActiveTarget == e.Server && ticket.Source == e.From)
            {
                // 事件在原生入口返回之前触发；不能提前让另一线程开始落地甚至回退。
                // 唯一的NativeComplete发布点是TransferPlayerToServer实际返回之后。
                return;
            }
            else
            {
                // 管理员/其他插件主动切到了第三个世界：放弃旧落点，绝不能再拉回来。
                session.Pending = null;
                session.Armed = false;
                session.NextAllowed = Environment.TickCount64 + ticket.Settings.CooldownMs;
            }
        }
    }

    private void ProcessPending(ServerContext server, long now)
    {
        TransferTicket[] tickets;
        lock (_gate) tickets = [.. _sessions.Values.Select(s => s.Pending).OfType<TransferTicket>()];
        foreach (var ticket in tickets)
        {
            if (!IsCurrent(ticket)) continue;
            if (!SameConnection(ticket)) { End(ticket, now); continue; }
            var route = UnifiedServerCoordinator.GetClientCurrentlyServer(ticket.Slot);
            if (now >= ticket.Deadline)
            {
                if (route == server)
                {
                    if (ticket.Routing && !ticket.ClientArrived)
                        AbortUnconfirmed(ticket, "原生切换/客户端落点确认超时");
                    else Fail(ticket, ticket.Routing ? "切换确认窗口结束。" : "切换准备超时，本次不切换。");
                }
                continue;
            }
            if (!ticket.Routing) continue;
            if (route != server || server != ticket.ActiveTarget || !ticket.NativeComplete) continue;
            if (ticket.Session.Player.dead || ticket.Session.Player.ghost) { End(ticket, now); continue; }
            if (!ticket.Session.Player.active) continue;
            if (ticket.NeedsLanding)
            {
                ticket.NeedsLanding = false;
                try { Land(ticket, now); }
                catch (Exception ex) { AbortUnconfirmed(ticket, "无特效落点同步异常：" + ex.Message); }
            }
            if (ticket.Landed && now >= ticket.GuardUntil)
            {
                if (!ticket.ClientArrived)
                    AbortUnconfirmed(ticket, "服务端已定位，但短时保护内未收到客户端到达坐标");
                else End(ticket, now);
            }
        }
    }

    // 6 在 Terraria.TeleportEffect / UpdateTeleportVisuals 中没有粒子或传送杖表现，视觉上等同原生切图。
    private const int SilentTeleportStyle = 6;

    private void Land(TransferTicket ticket, long now)
    {
        if (!IsCurrent(ticket)) return;
        if (!SameWorld(ticket.ActiveTarget, ticket.ActiveWorld))
        { AbortUnconfirmed(ticket, "落地前目标地图已更换"); return; }
        var player = ticket.Session.Player;
        if (player.dead || player.ghost) { End(ticket, now); return; }
        var map = new TerrariaLandingMap(ticket.ActiveTarget);
        if (!RuntimeSafe(ticket.ActiveTarget, map, ticket.Spot, player, ticket.Settings.HazardPadding))
        { AbortUnconfirmed(ticket, "海边/边界落点在切图后失效"); return; }
        if (!TeleportSilent(ticket, ticket.Spot))
        { AbortUnconfirmed(ticket, "无特效落点同步失败"); return; }
        ticket.LastTeleport = now;
        if (!ticket.Landed)
        {
            ticket.GuardUntil = Math.Min(ticket.Deadline, now + ticket.Settings.GuardMs);
            ticket.Landed = true;
            lock (_gate)
            {
                ticket.Session.Armed = false;
                ticket.Session.NextAllowed = now + ticket.Settings.CooldownMs;
            }
            // 自动成功不发玩家聊天提示；保留服务端诊断日志。
            UnifierApi.Logger.Info($"[BoundaryTransfer] {player.name}: 边界直达 {ticket.ActiveTarget.Name}，脚底坐标 ({(int)(player.Center.X / 16)}, {ticket.Spot.FeetY})，保持接触高度。");
        }
    }

    private static bool RuntimeSafe(ServerContext server, ILandingMap map, LandingSpot spot, Player player, int padding)
    {
        var position = new Vector2(spot.X, spot.Y);
        return BoundaryPlanner.IsSafe(map, spot, player.width, player.height, padding)
            && !Collision.SolidCollision(server, position, player.width, player.height)
            && !Collision.WetCollision(server, position, player.width, player.height, out _, out _)
            && !Collision.LavaCollision(server, position, player.width, player.height);
    }

    /// <summary>
    /// 不走 TShock Teleport 的 style=1 特效；直接设置原生位置并用手工 65 号包同步客户端。
    /// 不使用世界出生点、不改变 SpawnX/SpawnY，也不做任何地形安全搜索。
    /// </summary>
    private static bool TeleportSilent(TransferTicket ticket, LandingSpot spot)
    {
        if (!float.IsFinite(spot.X) || !float.IsFinite(spot.Y)) return false;
        var server = ticket.ActiveTarget;
        var player = ticket.Session.Player;
        var ts = ticket.Session.TSPlayer;
        var position = new Vector2(spot.X, spot.Y);
        player.velocity = Vector2.Zero;
        player.fallStart = player.fallStart2 = spot.FeetY;
        player.grappling[0] = -1;
        player.grapCount = 0;
        player.pulley = false;
        ts.SendTileSquareCentered((int)(spot.X / 16f), (int)(spot.Y / 16f), 15);
        server.RemoteClient.CheckSection(ts.Index, position, 1);
        player.Teleport(server, position, SilentTeleportStyle, 0);
        server.NetMessage.SendData(65, -1, -1, NetworkText.Empty, 0, player.whoAmI,
            spot.X, spot.Y, SilentTeleportStyle, 0, 0);
        player.velocity = Vector2.Zero;
        player.fallStart = player.fallStart2 = spot.FeetY;
        ts.LastNetPosition = player.position;
        return true;
    }

    private void OnSpawnPacket(ref ReceivePacketEvent<SpawnPlayer> args)
    {
        if (!_running || !TryPending(args.Who, args.Server, out var ticket)) return;
        if (args.Packet.Context != PlayerSpawnContext.SpawningIntoWorld)
        {
            // 死亡复活/床重生不是切图握手，不得把正常复活拉回边界。
            End(ticket, Environment.TickCount64);
            return;
        }
        args.PacketProcessed += OnSpawnProcessed;
    }

    private void OnSpawnProcessed(in ReceivePacketEvent<SpawnPlayer> args, PacketHandleMode mode)
    {
        // TShock可能自行完成原版Spawn再标Cancel，所以不能只接受None。
        if (!TryPending(args.Who, args.Server, out var ticket) || ticket.Session.Player.dead) return;
        ticket.ClientArrived = false;
        ticket.NeedsLanding = true;
        ticket.GuardUntil = Math.Min(ticket.Deadline, Environment.TickCount64 + ticket.Settings.GuardMs);
    }

    private void OnControlsPacket(ref ReceivePacketEvent<PlayerControls> args)
    {
        if (!_running || !TryPending(args.Who, args.Server, out var ticket) || !ticket.Routing) return;
        // 直达落点可能被边界实体稍微挤出，给 16 格容差；仍能区分目标出生点等远距离旧坐标。
        const float arrivalRadius = 16 * 16; // 16 格 × 16 像素
        if (ticket.Landed && Vector2.DistanceSquared(args.Packet.Position, new(ticket.Spot.X, ticket.Spot.Y)) <= arrivalRadius * arrivalRadius)
        { ticket.ClientArrived = true; return; }
        if (ticket.ClientArrived) return; // 已到达后，允许正常走路、传送，不把玩家锁在原地。
        args.HandleMode = PacketHandleMode.Cancel;
        args.StopPropagation = true; // 只挡短窗口里仍携带旧地图坐标的包。
        if (ticket.Landed && Environment.TickCount64 - ticket.LastTeleport >= 350)
            ticket.NeedsLanding = true;
    }

    private void OnLeave(ref ReadonlyNoCancelEventArgs<LeaveEvent> args)
    {
        int slot = args.Content.Who;
        lock (_gate) _sessions.Remove(slot);
    }

    private bool TryPending(int slot, ServerContext server, out TransferTicket ticket)
    {
        lock (_gate)
        {
            ticket = null!;
            if (!_sessions.TryGetValue(slot, out var session) || session.Pending is not { } found
                || !found.Routing || found.ActiveTarget != server) return false;
            ticket = found;
        }
        if (!IsCurrent(ticket) || !SameConnection(ticket) || UnifiedServerCoordinator.GetClientCurrentlyServer(slot) != server)
            return false;
        if (Environment.TickCount64 >= ticket.Deadline)
        {
            if (!ticket.ClientArrived) AbortUnconfirmed(ticket, "收到包时切换确认已经超时");
            else End(ticket, Environment.TickCount64);
            return false;
        }
        return true;
    }

    private bool IsCurrent(TransferTicket ticket)
    {
        lock (_gate) return _running && _sessions.TryGetValue(ticket.Slot, out var session)
            && ReferenceEquals(session, ticket.Session) && ReferenceEquals(session.Pending, ticket);
    }

    private static bool SameConnection(TransferTicket ticket) =>
        ticket.Slot >= 0 && ticket.Slot < TShock.Players.Length
        && ReferenceEquals(TShock.Players[ticket.Slot], ticket.Session.TSPlayer)
        && ReferenceEquals(UnifiedServerCoordinator.globalClients[ticket.Slot].Socket, ticket.Session.Socket)
        && UnifiedServerCoordinator.globalClients[ticket.Slot].IsConnected()
        && ReferenceEquals(UnifiedServerCoordinator.GetPlayer(ticket.Slot), ticket.Session.Player);

    private static bool TryGetLivePlayer(ServerContext server, int slot, out Player player, out TSPlayer ts, out object socket)
    {
        player = server.Main.player[slot];
        ts = TShock.Players[slot]!;
        socket = UnifiedServerCoordinator.globalClients[slot]?.Socket!;
        var client = UnifiedServerCoordinator.globalClients[slot];
        return ts is not null && player is not null && player.active && !player.dead && !player.ghost
            && ts.Active && socket is not null && client is not null && client.State == 10 && client.IsConnected()
            && !client.PendingTermination && UnifiedServerCoordinator.GetClientCurrentlyServer(slot) == server;
    }

    private static bool HasRoutedPlayers(ServerContext server)
    {
        for (int i = 0; i < UnifiedServerCoordinator.globalClients.Length; i++)
            if (UnifiedServerCoordinator.GetClientCurrentlyServer(i) == server) return true;
        return false;
    }
    private static WorldStamp CaptureWorld(ServerContext server) => new(server.Main, server.Main.tile,
        server.Main.ActiveWorldFileData, server.Main.worldID, server.Main.maxTilesX, server.Main.maxTilesY);
    private static bool SameWorld(ServerContext server, object snapshot) => snapshot is WorldStamp stamp
        && ReferenceEquals(server.Main, stamp.Main) && ReferenceEquals(server.Main.tile, stamp.Tiles)
        && ReferenceEquals(server.Main.ActiveWorldFileData, stamp.File) && server.Main.worldID == stamp.WorldId
        && server.Main.maxTilesX == stamp.Width && server.Main.maxTilesY == stamp.Height;
    private sealed record WorldStamp(object Main, object Tiles, object? File, int WorldId, int Width, int Height);
    private static BoundarySide Opposite(BoundarySide side) => side == BoundarySide.Left ? BoundarySide.Right : BoundarySide.Left;
    private static ServerContext? FindWorld(string name)
    {
        var found = UnifiedServerCoordinator.Servers.Where(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        return found.Length == 1 ? found[0] : null;
    }
    private static void Warn(PlayerSession session, string message, long now)
    {
        if (now < session.NextWarning) return;
        session.NextWarning = now + 5000;
        session.NextAllowed = now + 1000;
        UnifierApi.Logger.Warning($"[BoundaryTransfer] {session.Player.name}: {message}");
    }
    private void Fail(TransferTicket ticket, string message)
    {
        if (!IsCurrent(ticket)) return;
        Warn(ticket.Session, message, Environment.TickCount64);
        End(ticket, Environment.TickCount64);
    }
    private void AbortUnconfirmed(TransferTicket ticket, string reason)
    {
        if (!IsCurrent(ticket)) return;
        UnifierApi.Logger.Warning($"[BoundaryTransfer] {ticket.Session.Player.name}: {reason}，关闭捕获的原连接，请该玩家重连（不修改出生点）。");
        try
        {
            if (ticket.Session.Socket is Terraria.Net.Sockets.ISocket socket) socket.Close();
        }
        catch (Exception ex) { UnifierApi.Logger.Warning("[BoundaryTransfer] 关闭异常连接失败：" + ex.Message); }
        End(ticket, Environment.TickCount64);
    }

    private void End(TransferTicket ticket, long now)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(ticket.Session.Pending, ticket)) return;
            ticket.Session.Pending = null;
            ticket.Session.NextAllowed = Math.Max(ticket.Session.NextAllowed, now + ticket.Settings.CooldownMs);
            ticket.Session.Armed = false;
        }
    }

    private void OnCommand(CommandArgs args)
    {
        var p = args.Parameters;
        string sub = p.Count == 0 ? "帮助" : p[0].ToLowerInvariant();
        switch (sub)
        {
            case "世界": case "worlds":
                args.Executor.SendInfoMessage("当前运行世界（排列使用准确的世界名称，不使用数字序号）：");
                foreach (var server in UnifiedServerCoordinator.Servers)
                    args.Executor.SendInfoMessage($"{server.Name} [{(server.IsRunning ? "运行" : "停止")}] {server.Main.maxTilesX}×{server.Main.maxTilesY}");
                return;
            case "列表": case "list":
                args.Executor.SendInfoMessage($"边界传送：{(_settings.Enabled ? "启用" : "关闭")}；首尾循环：{_settings.Loop}");
                args.Executor.SendInfoMessage("从左到右：" + (_settings.Worlds.Length == 0 ? "尚未设置" : string.Join(" ↔ ", _settings.Worlds)));
                return;
            case "重载": case "reload":
                _config?.Reload();
                args.Executor.SendInfoMessage("已请求重载边界配置；无效配置会被拒绝并记录日志。");
                return;
            case "排列": case "order":
                var names = p.Skip(1).ToArray();
                if (names.Length < 2)
                { args.Executor.SendInfoMessage("用法：/边界 排列 <最左世界> <中间世界> <最右世界>（至少两个）"); return; }
                if (names.Any(n => FindWorld(n) is null))
                { args.Executor.SendInfoMessage("存在找不到或重名的世界，请先 /边界 世界 查看准确名称。"); return; }
                var changed = _settings.ToConfig();
                changed.WorldOrder = [.. names];
                try { _ = BoundarySettings.FromConfig(changed); }
                catch (ArgumentException ex) { args.Executor.SendInfoMessage(ex.Message); return; }
                _config?.Overwrite(changed);
                ApplyConfig(changed);
                args.Executor.SendInfoMessage("已保存从左到右排列：" + string.Join(" ↔ ", names));
                return;
            default:
                args.Executor.SendInfoMessage("/边界 世界：查看运行世界；/边界 排列 左侧世界 中间世界 右侧世界：保存连接");
                args.Executor.SendInfoMessage("/边界 列表：查看连接；/边界 重载：重读配置。权限：" + AdminPermission);
                return;
        }
    }

    private sealed class PlayerSession(Player player, TSPlayer ts, object socket)
    {
        public readonly Player Player = player;
        public readonly TSPlayer TSPlayer = ts;
        public readonly object Socket = socket;
        public bool Armed = true;
        public long NextAllowed;
        public long NextWarning;
        public TransferTicket? Pending;
        public bool Matches(Player p, TSPlayer t, object s) => ReferenceEquals(Player, p) && ReferenceEquals(TSPlayer, t) && ReferenceEquals(Socket, s);
    }

    private sealed class TransferTicket(int slot, PlayerSession session, ServerContext source,
        ServerContext target, BoundarySide side, BoundarySettings settings, long now)
    {
        public readonly int Slot = slot;
        public readonly PlayerSession Session = session;
        public readonly ServerContext Source = source;
        public readonly ServerContext Target = target;
        public readonly object SourceWorld = CaptureWorld(source);
        public readonly object TargetWorld = CaptureWorld(target);
        public readonly BoundarySide ExitSide = side;
        public readonly BoundarySettings Settings = settings;
        public readonly Vector2 OriginalPosition = session.Player.position;
        public readonly int PlayerWidth = session.Player.width;
        public readonly int PlayerHeight = session.Player.height;
        public long Deadline = now + settings.TimeoutMs;
        public ServerContext ActiveTarget => Target;
        public object ActiveWorld => TargetWorld;
        public BoundarySide EntrySide => Opposite(ExitSide);
        public LandingSpot Spot;
        public volatile bool Routing;
        public volatile bool NativeComplete;
        public volatile bool NeedsLanding;
        public volatile bool Landed;
        public volatile bool ClientArrived;
        public long GuardUntil;
        public long LastTeleport;
    }
}
