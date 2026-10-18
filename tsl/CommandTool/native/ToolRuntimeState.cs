using Terraria;
using TShockAPI;
using UnifierTSL;
using UnifierTSL.Servers;

namespace CommandTool;

internal sealed class ToolActor
{
    internal readonly int Slot;
    internal readonly TSPlayer TS;
    internal readonly Player Player;
    internal readonly object Socket;
    internal readonly ServerContext Source;
    internal readonly object SourceTiles;
    internal readonly int SourceWorldId;
    internal ToolActor(int slot,TSPlayer ts,Player player,object socket,ServerContext source)
    { Slot=slot;TS=ts;Player=player;Socket=socket;Source=source;SourceTiles=source.Main.tile;SourceWorldId=source.Main.worldID; }
    internal bool Connected => Slot>=0 && Slot<TShock.Players.Length
        && ReferenceEquals(TShock.Players[Slot],TS)
        && Slot<UnifiedServerCoordinator.globalClients.Length
        && UnifiedServerCoordinator.globalClients[Slot] is { PendingTermination:false } client
        && ReferenceEquals(client.Socket,Socket) && client.IsConnected()
        && ReferenceEquals(UnifiedServerCoordinator.GetPlayer(Slot),Player);
    internal bool In(ServerContext server) => Connected && Player.active && TS.FinishedHandshake
        && (server!=Source||(ReferenceEquals(SourceTiles,server.Main.tile)&&SourceWorldId==server.Main.worldID))
        && !Player.dead && !Player.ghost && UnifiedServerCoordinator.GetClientCurrentlyServer(Slot)==server;
    internal bool Matches(ToolActor other) => ReferenceEquals(TS,other.TS)&&ReferenceEquals(Player,other.Player)
        &&ReferenceEquals(Socket,other.Socket)&&ReferenceEquals(Source,other.Source)
        &&ReferenceEquals(SourceTiles,other.SourceTiles)&&SourceWorldId==other.SourceWorldId;
    internal static ToolActor? Capture(ServerContext server,int slot,bool interaction=true)
    {
        if(slot<0 || slot>=TShock.Players.Length || slot>=server.Main.player.Length
            || slot>=UnifiedServerCoordinator.globalClients.Length)return null;
        var ts=TShock.Players[slot];var p=server.Main.player[slot];var c=UnifiedServerCoordinator.globalClients[slot];
        if(ts is null||p is null||c?.Socket is null)return null;
        var actor=new ToolActor(slot,ts,p,c.Socket,server);
        bool routed=actor.Connected&&p.active&&ts.FinishedHandshake&&UnifiedServerCoordinator.GetClientCurrentlyServer(slot)==server;
        return routed&&(!interaction||(actor.In(server)&&p.gravDir==1f))?actor:null;
    }
}

internal sealed class ToolRegionState
{
    internal string? Dwelling;
    internal long Entered;
    internal readonly HashSet<string> Latched=new(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<string,long> NextAllowed=new(StringComparer.OrdinalIgnoreCase);
    internal long SuppressUntil;
    internal void Reset() {Dwelling=null;Entered=0;Latched.Clear();NextAllowed.Clear();}
    internal RegionBinding? Advance(IEnumerable<RegionBinding> regions,float tileX,float tileY,long now)
    {
        var inside=regions.Where(r=>r.Contains(tileX,tileY)).OrderByDescending(r=>r.Priority)
            .ThenBy(r=>r.Id,StringComparer.OrdinalIgnoreCase).ToArray();
        var set=inside.Select(r=>r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Latched.RemoveWhere(id=>!set.Contains(id));
        foreach(var id in NextAllowed.Where(p=>p.Value<=now && !set.Contains(p.Key)).Select(p=>p.Key).ToArray()) NextAllowed.Remove(id);
        if(now<SuppressUntil) { foreach(var r in inside)Latched.Add(r.Id);Dwelling=null;return null; }
        var selected=inside.FirstOrDefault();
        if(selected is null||Latched.Contains(selected.Id)||NextAllowed.GetValueOrDefault(selected.Id)>now)
        {Dwelling=null;return null;}
        if(!string.Equals(Dwelling,selected.Id,StringComparison.OrdinalIgnoreCase)) {Dwelling=selected.Id;Entered=now;}
        if(now-Entered<selected.DwellMs)return null;
        foreach(var r in inside)Latched.Add(r.Id); // 重叠区域只执行最高优先级，其他区域亦须先离开。
        NextAllowed[selected.Id]=now+Math.Max(250,selected.CooldownMs);
        Dwelling=null;return selected;
    }
}

internal sealed class ToolPlayerState(ToolActor actor,long generation)
{
    internal ToolActor Actor=actor;
    internal long Generation=generation;
    internal ToolRegionState Region=new();
    internal readonly Dictionary<string,long> Cooldowns=new(StringComparer.OrdinalIgnoreCase);
    internal volatile bool Executing;
    internal string? Selecting;
    internal string? SelectedKind;
    internal int SelectedX,SelectedY;
    internal string? SelectedText;
    internal long SelectionUntil;
    internal int ConfirmX,ConfirmY;
    internal long ConfirmSuppressUntil;
    internal PointBinding? Draft;
    internal (int XMin,int YMin,int XMax,int YMax)? Corner;
    internal (int XMin,int YMin,int XMax,int YMax)? Corner2;
    internal string SelectionMode="point";
}
