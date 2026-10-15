using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TrProtocol;
using UnifierTSL.Plugins;
using UnifierTSL;
using UnifierTSL.Events.Core;
using UnifierTSL.Events.Handlers;
using TShockAPI.Hooks;
using UnifierTSL.PluginService;
using UnifierTSL.Logging;
using UnifierTSL.Servers;
using UnifiedServerProcess;

namespace TerrariaApi.Server;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ApiVersionAttribute : Attribute
{
    public ApiVersionAttribute(int major, int minor) { }
}

public delegate void HookHandler<T>(T args);

public class HandledEventArgs : EventArgs
{
    public bool Handled { get; set; }
}

public sealed class JoinEventArgs : EventArgs { public int Who { get; set; } }
public sealed class LeaveEventArgs : EventArgs { public int Who { get; set; } }
public sealed class GreetPlayerEventArgs : EventArgs { public int Who { get; set; } }

public sealed class CompatMessageBuffer
{
    public byte[] readBuffer = Array.Empty<byte>();
    public int whoAmI;
}

public sealed class GetDataEventArgs : HandledEventArgs
{
    public CompatMessageBuffer Msg { get; set; }
    public MessageID MsgID { get; set; }
    public int Index { get; set; }
    public int Length { get; set; }
}

public sealed class NpcStrikeEventArgs : HandledEventArgs
{
    public NPC Npc { get; set; }
    public Player Player { get; set; }
    public int Damage { get; set; }
}

public sealed class ProjectileAiUpdateEventArgs : HandledEventArgs
{
    public Projectile Projectile { get; set; }
}

public sealed class NpcAiUpdateEventArgs : HandledEventArgs { public NPC? Npc { get; set; } }
public sealed class NpcKilledEventArgs : EventArgs { public NPC? npc { get; set; } }
public sealed class DropBossBagEventArgs : EventArgs { public int NpcArrayIndex { get; set; } public Microsoft.Xna.Framework.Vector2 Position { get; set; } }

public sealed class ServerChatEventArgs : HandledEventArgs { public int Who { get; set; } public string Text { get; set; } = ""; public Terraria.Chat.ChatCommandId CommandId { get; set; } }

public abstract class TerrariaPlugin : BasePlugin
{
    protected TerrariaPlugin(Main game) { }
    public virtual string Name => GetType().Name;
    public virtual string Author => "Unknown";
    public virtual string Description => "";
    public virtual Version Version => new(1, 0);
    public int Order { get; set; }
    public abstract void Initialize();
    protected virtual void Dispose(bool disposing) { }
    public override Task InitializeAsync(IPluginConfigRegistrar configRegistrar, ImmutableArray<PluginInitInfo> priorInitializations, CancellationToken cancellationToken = default)
    {
        Initialize();
        return Task.CompletedTask;
    }
    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        Dispose(true);
        return Task.CompletedTask;
    }
}

public sealed class HookManager<T>
{
    private readonly List<HookHandler<T>> _handlers = new();
    public void Register(TerrariaPlugin plugin, HookHandler<T> handler) { if (handler != null) _handlers.Add(handler); }
    public void Deregister(TerrariaPlugin plugin, HookHandler<T> handler) { _handlers.Remove(handler); }
    public void Invoke(T args) { HookHandler<T>[] snapshot; lock (_handlers) { if (_handlers.Count == 0) return; snapshot = _handlers.ToArray(); } foreach (var handler in snapshot) { try { handler(args); } catch { } } }
}

public static class ServerApi
{
    static ServerApi()
    {
        PlayerHooks.PlayerPostLogin += args =>
        {
            if (args?.Player != null) Hooks.ServerJoin.Invoke(new JoinEventArgs { Who = args.Player.Index });
        };
        PlayerHooks.PlayerLogout += args =>
        {
            if (args?.Player != null) Hooks.ServerLeave.Invoke(new LeaveEventArgs { Who = args.Player.Index });
        };
        NetPacketHandler.ProcessPacketEvent.Register(OnRawPacket, HandlerPriority.Highest);
        UnifierApi.EventHub.Game.GameInitialize.Register(OnGameInitialize, HandlerPriority.Normal);
        UnifierApi.EventHub.Game.GamePostInitialize.Register(OnGamePostInitialize, HandlerPriority.Normal);
        On.Terraria.NPC.AI += OnNpcAIHook;
        On.Terraria.NPC.NPCLoot += OnNpcLootHook;
        On.Terraria.Main.Update += OnMainUpdateHook;
    }

    private static void OnGameInitialize(ref ReadonlyNoCancelEventArgs<UnifierTSL.Events.Handlers.ServerEvent> args) { Hooks.GameInitialize.Invoke(EventArgs.Empty); }

    private static void OnGamePostInitialize(ref ReadonlyNoCancelEventArgs<UnifierTSL.Events.Handlers.ServerEvent> args)
    {
        Hooks.GamePostInitialize.Invoke(EventArgs.Empty);
    }

    private static void OnRawPacket(ref ReadonlyEventArgs<ProcessPacketEvent> args)
    {
        ProcessPacketEvent evt = args.Content;
        if (evt.EventType != ProcessPacketEventType.BeforeOriginalProcess) return;
        ReadOnlySpan<byte> raw = evt.RawData;
        if (raw.Length < 3) return;
        var data = new CompatMessageBuffer
        {
            readBuffer = raw.ToArray(),
            whoAmI = evt.Who
        };
        var legacy = new GetDataEventArgs
        {
            Msg = data,
            MsgID = (MessageID)raw[0],
            Index = 1,
            Length = raw.Length - 1
        };
        Hooks.NetGetData.Invoke(legacy);
        if (legacy.Handled) args.Handled = true;
    }

    private static void OnNpcAIHook(On.Terraria.NPC.orig_AI orig, NPC self, RootContext root)
    {
        if (self is null || !self.active) { orig(self, root); return; }
        var e = new NpcAiUpdateEventArgs { Npc = self };
        try { Hooks.NpcAIUpdate.Invoke(e); } catch { }
        if (!e.Handled) orig(self, root);
    }

    private static void OnNpcLootHook(On.Terraria.NPC.orig_NPCLoot orig, NPC self, RootContext root)
    {
        if (self is null) { orig(self, root); return; }
        // 旧 NpcKilled（死亡确认）与 DropBossBag（宝藏袋掉落）语义统一映射到此相位触发
        try
        {
            Hooks.NpcKilled.Invoke(new NpcKilledEventArgs { npc = self });
            Hooks.DropBossBag.Invoke(new DropBossBagEventArgs { NpcArrayIndex = self.whoAmI, Position = self.Center });
        }
        catch { }
        orig(self, root);
    }

    private static void OnMainUpdateHook(On.Terraria.Main.orig_Update orig, Terraria.Main self, RootContext root, Microsoft.Xna.Framework.GameTime gameTime)
    {
        if (root is ServerContext)
        {
            try { Hooks.GameUpdate.Invoke(EventArgs.Empty); } catch { }
        }
        orig(self, root, gameTime);
    }

    public static class Hooks
    {
        public static readonly HookManager<JoinEventArgs> ServerJoin = new();
        public static readonly HookManager<LeaveEventArgs> ServerLeave = new();
        public static readonly HookManager<GetDataEventArgs> NetGetData = new();
        public static readonly HookManager<NpcStrikeEventArgs> NpcStrike = new();
        public static readonly HookManager<ProjectileAiUpdateEventArgs> ProjectileAIUpdate = new();
        public static readonly HookManager<EventArgs> GameInitialize = new();
        public static readonly HookManager<EventArgs> GamePostInitialize = new();
        public static readonly HookManager<ServerChatEventArgs> ServerChat = new();
        public static readonly HookManager<GreetPlayerEventArgs> NetGreetPlayer = new();
        public static readonly HookManager<NpcAiUpdateEventArgs> NpcAIUpdate = new();
        public static readonly HookManager<NpcKilledEventArgs> NpcKilled = new();
        public static readonly HookManager<DropBossBagEventArgs> DropBossBag = new();
        public static readonly HookManager<EventArgs> GameUpdate = new();
    }
}

/// <summary>旧 TShock 日志方法名兼容（ConsoleInfo/ConsoleError/ConsoleWarn ⇒ Info/Error/Warning）</summary>
public static class LegacyLogExtensions
{
    public static void ConsoleInfo(this UnifierTSL.Logging.RoleLogger log, string message) => log.Info(message);
    public static void ConsoleError(this UnifierTSL.Logging.RoleLogger log, string message) => log.Error(message);
    public static void ConsoleWarn(this UnifierTSL.Logging.RoleLogger log, string message) => log.Warning(message);
}

/// <summary>FixTools 迁移用全局目标（旧 TSPlayer.All / TSPlayer.Server 替代）</summary>
public static class CompatHooks
{
    public static readonly TShockAPI.TSPlayerAll AllTargets = new();
    public static TShockAPI.TSPlayer ServerTarget => (TShockAPI.TSPlayer)TShock.TSLauncherPlr;

    /// <summary>旧 Utils.Broadcast(msg,color) 的迁移桥：对全部运行中服务器广播（多服环境逐一投递）</summary>
    public static void Broadcast(string msg, Microsoft.Xna.Framework.Color color)
    {
        foreach (var server in UnifierTSL.UnifiedServerCoordinator.Servers)
        {
            try { TShockAPI.Utils.Broadcast(server, msg, color); } catch { }
        }
    }
}
