using System;
using System.Collections.Generic;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Extension;
using TrProtocol.NetPackets;
using UnifierTSL.Events.Core;
using UnifierTSL.Events.Handlers;

namespace FixTools;

/// <summary>
/// TShock 旧版 GetDataHandlers 事件模型的兼容桥（FixTools 专用）。
/// 旧插件代码使用 GetDataHandlers.Xxx.Register/UnRegister + GetDataHandlers.XxxEventArgs；
/// 新版 TShockAPI 无公开事件，此桥以 NetPacketHandler 原包监听重建同等事件。
/// </summary>
public sealed class CompatEvent<TArgs>
{
    private readonly List<Action<object?, TArgs>> _handlers = new();

    public void Register(Action<object?, TArgs> handler)
    {
        if (handler is null) return;
        lock (_handlers)
        {
            if (!_handlers.Contains(handler)) _handlers.Add(handler);
        }
    }

    public void UnRegister(Action<object?, TArgs> handler)
    {
        if (handler is null) return;
        lock (_handlers)
        {
            _handlers.Remove(handler);
        }
    }

    internal void Invoke(TArgs args)
    {
        Action<object?, TArgs>[] snapshot;
        lock (_handlers)
        {
            if (_handlers.Count == 0) return;
            snapshot = _handlers.ToArray();
        }
        foreach (var h in snapshot)
        {
            try { h(null, args); } catch { }
        }
    }
}

public static class CompatGetDataHandlers
{
    public static readonly CompatEvent<SpawnEventArgs> PlayerSpawn = new();
    public static readonly CompatEvent<PlayerUpdateEventArgs> PlayerUpdate = new();
    public static readonly CompatEvent<KillMeEventArgs> KillMe = new();
    public static readonly CompatEvent<PlayerTeamEventArgs> PlayerTeam = new();
    public static readonly CompatEvent<PlaceObjectEventArgs> PlaceObject = new();
    public static readonly CompatEvent<MassWireOperationEventArgs> MassWireOperation = new();

    #region 兼容 EventArgs
    public sealed class SpawnEventArgs : HandledEventArgs
    {
        public TSPlayer? Player { get; set; }
        public PlayerSpawnContext SpawnContext { get; set; }
    }

    public sealed class PlayerUpdateEventArgs : HandledEventArgs
    {
        public TSPlayer? Player { get; set; }
    }

    public sealed class KillMeEventArgs : HandledEventArgs
    {
        public int PlayerId { get; set; }
    }

    public sealed class PlayerTeamEventArgs : HandledEventArgs
    {
        public TSPlayer? Player { get; set; }
        public int Team { get; set; }
    }

    public sealed class PlaceObjectEventArgs : HandledEventArgs
    {
        public TSPlayer? Player { get; set; }
        public short Type { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    public sealed class MassWireOperationEventArgs : HandledEventArgs
    {
        public TSPlayer? Player { get; set; }
        public int StartX { get; set; }
        public int StartY { get; set; }
        public int EndX { get; set; }
        public int EndY { get; set; }
        public int ToolMode { get; set; }
    }
    #endregion

    static CompatGetDataHandlers()
    {
        NetPacketHandler.Register<SpawnPlayer>(OnSpawnPlayer, HandlerPriority.Low);
        NetPacketHandler.Register<PlayerControls>(OnPlayerControls, HandlerPriority.Low);
        NetPacketHandler.Register<PlayerDeathV2>(OnPlayerDeathV2, HandlerPriority.Low);
        NetPacketHandler.Register<PlayerTeam>(OnPlayerTeamPacket, HandlerPriority.Low);
        NetPacketHandler.Register<PlaceObject>(OnPlaceObjectPacket, HandlerPriority.Low);
        NetPacketHandler.Register<MassWireOperation>(OnMassWireOperationPacket, HandlerPriority.Low);
    }

    private static void OnSpawnPlayer(ref ReceivePacketEvent<SpawnPlayer> args)
    {
        PlayerSpawn.Invoke(new SpawnEventArgs
        {
            Player = args.GetTSPlayer(),
            SpawnContext = args.Packet.Context
        });
    }

    private static void OnPlayerControls(ref ReceivePacketEvent<PlayerControls> args)
    {
        PlayerUpdate.Invoke(new PlayerUpdateEventArgs
        {
            Player = args.GetTSPlayer()
        });
    }

    private static void OnPlayerDeathV2(ref ReceivePacketEvent<PlayerDeathV2> args)
    {
        KillMe.Invoke(new KillMeEventArgs
        {
            PlayerId = args.Who
        });
    }

    private static void OnPlayerTeamPacket(ref ReceivePacketEvent<PlayerTeam> args)
    {
        PlayerTeam.Invoke(new PlayerTeamEventArgs
        {
            Player = args.GetTSPlayer(),
            Team = args.Packet.Team
        });
    }

    private static void OnPlaceObjectPacket(ref ReceivePacketEvent<PlaceObject> args)
    {
        PlaceObject.Invoke(new PlaceObjectEventArgs
        {
            Player = args.GetTSPlayer(),
            X = args.Packet.Position.X,
            Y = args.Packet.Position.Y,
            Type = args.Packet.ObjectType
        });
    }

    private static void OnMassWireOperationPacket(ref ReceivePacketEvent<MassWireOperation> args)
    {
        MassWireOperation.Invoke(new MassWireOperationEventArgs
        {
            Player = args.GetTSPlayer(),
            StartX = args.Packet.Start.X,
            StartY = args.Packet.Start.Y,
            EndX = args.Packet.End.X,
            EndY = args.Packet.End.Y,
            ToolMode = (int)args.Packet.Mode
        });
    }
}