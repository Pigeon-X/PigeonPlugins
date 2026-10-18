using Terraria;
using TrProtocol.NetPackets;
using UnifiedServerProcess;
using UnifierTSL.Events.Core;
using UnifierTSL.Events.Handlers;
using UnifierTSL.Servers;

namespace CommandTool;

public sealed partial class CommandToolPlugin
{
    private readonly AsyncLocal<ToolActor?> _editingActor=new();
    private void BindProtection()
    {
        On.Terraria.WorldGenSystemContext.KillTile+=OnKillTile;
        On.Terraria.MessageBuffer.GetData_RootContext_int_int_refInt32+=OnMessageData;
        NetPacketHandler.Register<TileSquare>(OnTileSquare,HandlerPriority.Highest);
    }
    private void UnbindProtection()
    {
        NetPacketHandler.UnRegister<TileSquare>(OnTileSquare);
        On.Terraria.MessageBuffer.GetData_RootContext_int_int_refInt32-=OnMessageData;
        On.Terraria.WorldGenSystemContext.KillTile-=OnKillTile;
    }
    private void OnMessageData(On.Terraria.MessageBuffer.orig_GetData_RootContext_int_int_refInt32 orig,
        MessageBuffer self,RootContext root,int start,int length,out int messageType)
    {
        var previous=_editingActor.Value;
        _editingActor.Value=root is ServerContext server?ToolActor.Capture(server,self.whoAmI,false):null;
        try{orig(self,root,start,length,out messageType);}
        finally{_editingActor.Value=previous;}
    }
    private bool ProtectedAt(ServerContext server,int x,int y,out string kind)
    {
        kind="";if(!_running||!_settings.Enabled)return false;
        if(_settings.ProtectSwitches&&ToolEntities.Switch(server,x,y,true) is { } lever&&Point(_settings.Switches,server,lever) is not null)
        {kind="开关";return true;}
        if(_settings.ProtectSigns&&ToolEntities.Sign(server,x,y) is { } sign&&Point(_settings.Signs,server,sign) is not null)
        {kind="标牌";return true;}
        if(RegionProtectsAt(server,x,y)){kind="区域";return true;}
        return false;
    }
    private bool RegionProtectsAt(ServerContext server,int x,int y)
        =>_settings.ProtectRegions&&_settings.Regions.Any(r=>r.Enabled&&InWorld(r,server)
            &&r.ContainsExpanded(x,y,_settings.RegionProtectMargin));
    private bool RegionIntersects(ServerContext server,int left,int top,int right,int bottom)
        =>_settings.ProtectRegions&&_settings.Regions.Any(r=>r.Enabled&&InWorld(r,server)
            &&r.IntersectsExpanded(left,top,right,bottom,_settings.RegionProtectMargin));
    private void OnKillTile(On.Terraria.WorldGenSystemContext.orig_KillTile orig,
        WorldGenSystemContext self,int x,int y,bool fail,bool effectOnly,bool noItem)
    {
        if(self.root is ServerContext selectingServer)
        {
            var selecting=_editingActor.Value;
            if(selecting is not null&&selecting.Connected&&selecting.Source==selectingServer
                &&State(selecting).Corner is not null)return;
        }
        ServerContext? boundServer=null;string kind="";
        if(self.root is ServerContext server&&ProtectedAt(server,x,y,out kind))
        {
            var actor=_editingActor.Value;
            // 爆炸、支撑掉落等无可验证编辑者的销毁也不能破坏绑定实体；普通开关完全不在此范围。
            if(actor is null||!actor.Connected||actor.Source!=server||!CanEdit(actor.TS,kind))return;
            boundServer=server;
        }
        orig(self,x,y,fail,effectOnly,noItem);
        if(boundServer is not null&&kind!="区域")DisableMissing(boundServer,x-1,y-1,x+2,y+2);
    }
    private void OnTileSquare(ref ReceivePacketEvent<TileSquare> args)
    {
        if(!_running||!_settings.Enabled)return;
        var actor=ToolActor.Capture(args.Server,args.Who,false);if(actor is null)return;
        if(State(actor).Corner is not null)
        {
            args.HandleMode=PacketHandleMode.Cancel;args.StopPropagation=true;
            actor.TS.SendTileSquareCentered(args.Packet.Data.TilePosX,args.Packet.Data.TilePosY,16);return;
        }
        var data=args.Packet.Data;int left=data.TilePosX,top=data.TilePosY;
        long right=(long)left+data.Width,bottom=(long)top+data.Height;
        bool blocked=false;
        foreach(var pair in new[]{(_settings.Signs,"标牌",_settings.ProtectSigns),(_settings.Switches,"开关",_settings.ProtectSwitches)})
        {
            if(!pair.Item3||CanEdit(actor.TS,pair.Item2))continue;
            foreach(var point in pair.Item1)
            {
                if(!Active(point)||!InWorld(point,args.Server))continue;
                var entity=pair.Item2=="标牌"?ToolEntities.Sign(args.Server,point.X,point.Y):ToolEntities.Switch(args.Server,point.X,point.Y,true);
                if(entity is not { } e)continue;
                if(left<e.X+e.Width&&right>e.X&&top<e.Y+e.Height&&bottom>e.Y)
                {blocked=true;actor.TS.SendTileSquareCentered(e.X,e.Y,4);break;}
            }
            if(blocked)break;
        }
        if(!blocked&&_settings.ProtectRegions&&!CanEdit(actor.TS,"区域")
            &&RegionIntersects(args.Server,left,top,(int)right-1,(int)bottom-1))
        {
            blocked=true;
            byte size=(byte)Math.Clamp(Math.Max(data.Width,data.Height)+4,4,255);
            actor.TS.SendTileSquareCentered(left+data.Width/2,top+data.Height/2,size);
        }
        if(blocked){args.HandleMode=PacketHandleMode.Cancel;args.StopPropagation=true;}
        else args.PacketProcessed+=SquareEdited;
    }
    private void SquareEdited(in ReceivePacketEvent<TileSquare> args,PacketHandleMode mode)
    {
        if(mode==PacketHandleMode.Cancel||!_running)return;
        var d=args.Packet.Data;DisableMissing(args.Server,d.TilePosX-1,d.TilePosY-1,d.TilePosX+d.Width+1,d.TilePosY+d.Height+1);
    }
    private void DisableMissing(ServerContext server,int left,int top,int right,int bottom)
    {
        if(!_settings.Signs.Concat(_settings.Switches).Any(p=>p.Enabled&&p.X>=left&&p.X<right&&p.Y>=top&&p.Y<bottom&&InWorld(p,server)))return;
        var copy=ToolValidation.Snapshot(_settings);bool changed=false;
        foreach(var p in copy.Signs.Concat(copy.Switches).Where(p=>p.Enabled&&p.X>=left&&p.X<right&&p.Y>=top&&p.Y<bottom&&InWorld(p,server)))
        {
            var entity=copy.Signs.Contains(p)?ToolEntities.Sign(server,p.X,p.Y):ToolEntities.Switch(server,p.X,p.Y,true);
            if(entity is null){p.Enabled=false;changed=true;}
        }
        if(!changed)return;
        try{Save(copy);}
        catch(Exception e){Apply(copy);UnifierTSL.UnifierApi.Logger.Warning("[CommandTool] 已临时禁用失效绑定，但保存失败，请修复配置写权限："+e.Message);}
    }
}
