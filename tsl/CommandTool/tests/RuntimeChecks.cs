using System.Reflection;
using CommandTool;
using Terraria;
using Terraria.DataStructures;
using Terraria.Net;
using Terraria.Net.Sockets;
using TrProtocol.NetPackets;
using TShockAPI;
using UnifierTSL;
using UnifierTSL.Events.Handlers;
using UnifierTSL.Logging;
using UnifierTSL.Servers;

internal static class RuntimeChecks
{
    internal static unsafe void Run(Action<string,bool> check)
    {
        const int slot=7;
        var source=World("来源");
        var p=new Player{whoAmI=slot,name="实体探针",active=true,width=20,height=42,gravDir=1,position=new(100*16,100*16)};
        var ts=new ProbePlayer(slot){FinishedHandshake=true,Group=new Group("member")};var socket=new ProbeSocket();
        var oldTs=TShock.Players[slot];var oldP=UnifiedServerCoordinator.players[slot];var oldSocket=UnifiedServerCoordinator.globalClients[slot].Socket;
        var oldState=UnifiedServerCoordinator.globalClients[slot].State;var oldRoute=UnifiedServerCoordinator.GetClientCurrentlyServer(slot);
        TShock.Players[slot]=ts;UnifiedServerCoordinator.players[slot]=p;
        source.Main.player[slot]=p;
        UnifiedServerCoordinator.globalClients[slot].Socket=socket;UnifiedServerCoordinator.globalClients[slot].State=10;
        SetRoute(slot,source);UnifiedServerCoordinator.AddServer(source);
        try
        {
            var actor=ToolActor.Capture(source,slot)!;check("捕获真实TS玩家Socket及来源地图",actor is not null&&actor.Connected&&actor.In(source));
            var pointBox=CommandToolPlugin.PointBox(actor);
            check("点1/点2是纯单格",pointBox==(100,102,100,102));
            var bodyBox=CommandToolPlugin.SelectionBox(actor,null);
            check("占位框按玩家实际占用约2×3格",bodyBox==(100,100,101,102));
            var threeBox=CommandToolPlugin.SelectionBox(actor,"3x3");
            check("占位框可指定3×3",threeBox==(99,100,101,102));
            check("占位框尺寸非法拒绝",ThrowsArgument(()=>CommandToolPlugin.SelectionBox(actor,"3")));
            var pluginForSelection=new CommandToolPlugin();
            Set(pluginForSelection,"_running",true);
            var state=typeof(CommandToolPlugin).GetMethod("State",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pluginForSelection,[actor])!;
            Set(state,"Corner",(100,100,100,100));
            var selectionTile=typeof(CommandToolPlugin).GetMethod("OnTile",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<ReceivePacket<TileChange>>(pluginForSelection);
            var selectionInfo=new ReceiveBytesInfo(UnifiedServerCoordinator.clientSenders[slot],source.PacketReceiver,null,null);
            var selectionPacket=new ReceivePacketEvent<TileChange>(in selectionInfo){Packet=new(){Position=new Point16(130,100)}};
            selectionTile(ref selectionPacket);
            check("选区期间玩家图格修改被阻止",selectionPacket.HandleMode==PacketHandleMode.Cancel);
            Set(state,"Corner",null);
            selectionPacket=new(in selectionInfo){Packet=new(){Position=new Point16(130,100)}};selectionTile(ref selectionPacket);
            check("取消选区后图格修改恢复",selectionPacket.HandleMode==PacketHandleMode.None);
            ref var ordinary=ref source.Main.tile[105,100];ordinary.type=136;ordinary.active(true);
            check("真实小开关136实体归一化",ToolEntities.Switch(source,105,100) is {X:105,Y:100,Width:1});
            ref var plate=ref source.Main.tile[106,100];plate.type=135;plate.active(true);
            check("压力板135可作为开关",ToolEntities.Switch(source,106,100) is {X:106,Y:100,Width:1});
            for(int dx=0;dx<2;dx++)for(int dy=0;dy<2;dy++)
            {ref var cell=ref source.Main.tile[100+dx,100+dy];cell.type=132;cell.active(true);cell.frameX=(short)(36+dx*18);cell.frameY=(short)(dy*18);}
            check("真实2x2拉杆任意格同一锚点",ToolEntities.Switch(source,101,101) is {X:100,Y:100,Width:2,Height:2});
            source.Main.tile[101,101].inActive(true);
            check("虚化拉杆不可执行但保护仍识别",ToolEntities.Switch(source,101,101) is null&&ToolEntities.Switch(source,101,101,true) is not null);
            source.Main.tile[101,101].inActive(false);
            var plugin=new CommandToolPlugin();
            Set(plugin,"_running",true);Set(plugin,"_settings",new ToolConfig{Switches=[new(){Id="s",World=source.Name,X=100,Y=100,Actions=[new(){Command="home"}]}]});
            var tile=typeof(CommandToolPlugin).GetMethod("OnTile",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<ReceivePacket<TileChange>>(plugin);
            var info=new ReceiveBytesInfo(UnifiedServerCoordinator.clientSenders[slot],source.PacketReceiver,null,null);
            var packet=new ReceivePacketEvent<TileChange>(in info){Packet=new(){Position=new Point16(101,101)}};
            tile(ref packet);check("指令拉杆四格受保护且回发图格",packet.HandleMode==PacketHandleMode.Cancel&&ts.TileSyncs>0);
            packet=new(in info){Packet=new(){Position=new Point16(105,100)}};tile(ref packet);
            check("未绑定普通开关不被保护",packet.HandleMode==PacketHandleMode.None);
            packet=new(in info){Packet=new(){Position=new Point16(106,100)}};tile(ref packet);
            check("未绑定压力板不被保护",packet.HandleMode==PacketHandleMode.None);
            p.gravDir=-1;packet=new(in info){Packet=new(){Position=new Point16(100,100)}};tile(ref packet);
            check("倒重力不能绕过绑定开关保护",packet.HandleMode==PacketHandleMode.Cancel);p.gravDir=1;
            ts.Group=new Group("editor",permissions:"commandtool.edit");packet=new(in info){Packet=new(){Position=new Point16(100,100)}};tile(ref packet);
            check("有明确编辑权限的玩家可编辑绑定实体",packet.HandleMode==PacketHandleMode.None);ts.Group=new Group("member");
            Set(plugin,"_settings",new ToolConfig{Regions=[new(){Id="r",World=source.Name,XMin=200,XMax=210,YMin=200,YMax=210,Actions=[new(){Command="home"}]}]});
            packet=new(in info){Packet=new(){Position=new Point16(195,205)}};tile(ref packet);
            check("区域外扩5格边界不可破坏",packet.HandleMode==PacketHandleMode.Cancel);
            packet=new(in info){Packet=new(){Position=new Point16(194,205)}};tile(ref packet);
            check("区域外扩第6格不保护",packet.HandleMode==PacketHandleMode.None);
            packet=new(in info){Packet=new(){Position=new Point16(215,205)}};tile(ref packet);
            check("区域另一侧外扩5格不可破坏",packet.HandleMode==PacketHandleMode.Cancel);
            packet=new(in info){Packet=new(){Position=new Point16(216,205)}};tile(ref packet);
            check("区域另一侧第6格不保护",packet.HandleMode==PacketHandleMode.None);
            ts.Group=new Group("editor",permissions:"commandtool.edit");packet=new(in info){Packet=new(){Position=new Point16(195,205)}};tile(ref packet);
            check("管理员可编辑区域保护范围",packet.HandleMode==PacketHandleMode.None);ts.Group=new Group("member");
            source.Main.sign[0]=new Sign{x=110,y=100,text="命令标牌"};
            for(int dx=0;dx<2;dx++)for(int dy=0;dy<2;dy++)
            {ref var cell=ref source.Main.tile[110+dx,100+dy];cell.type=55;cell.active(true);cell.frameX=(short)(dx*18);cell.frameY=(short)(dy*18);}
            var signSettings=new ToolConfig{Signs=[new(){Id="sign",World=source.Name,X=110,Y=100,Text=null,Actions=[new(){Command="fixture-sign"}]}]};
            Set(plugin,"_settings",signSettings);
            var runner=(ToolCommandRunner)Field(plugin,"_runner")!;runner.Bind();
            int signRuns=0;var signCommand=new Command(args=>signRuns++,"fixture-sign");Commands.ChatCommands.Add(signCommand);
            try
            {
                var click=typeof(CommandToolPlugin).GetMethod("OnSignClick",BindingFlags.NonPublic|BindingFlags.Instance)!.CreateDelegate<ReceivePacket<RequestReadSign>>(plugin);
                var confirm=typeof(CommandToolPlugin).GetMethod("OnSignConfirm",BindingFlags.NonPublic|BindingFlags.Instance)!.CreateDelegate<ReceivePacket<ReadSign>>(plugin);
                var clickPacket=new ReceivePacketEvent<RequestReadSign>(in info){Packet=new(){Position=new Point16(110,100)}};click(ref clickPacket);
                check("旧文字快照为空的真实标牌仍可点击执行",clickPacket.HandleMode==PacketHandleMode.Cancel&&signRuns==1);
                var confirmPacket=new ReceivePacketEvent<ReadSign>(in info){Packet=new(){SignSlot=0,Position=new Point16(110,100),Text="命令标牌"}};confirm(ref confirmPacket);
                check("点击后的确认包不重复执行",confirmPacket.HandleMode==PacketHandleMode.Cancel&&signRuns==1);
                confirmPacket=new(in info){Packet=new(){SignSlot=0,Position=new Point16(300,300),Text="伪造坐标改字"}};confirm(ref confirmPacket);
                check("伪造普通坐标不能用绑定SignSlot改字",confirmPacket.HandleMode==PacketHandleMode.Cancel);
                confirmPacket=new(in info){Packet=new(){SignSlot=1,Position=new Point16(110,100),Text="伪造索引"}};confirm(ref confirmPacket);
                check("绑定标牌坐标必须与索引一致",confirmPacket.HandleMode==PacketHandleMode.Cancel);
                confirmPacket=new(in info){Packet=new(){SignSlot=0,Position=new Point16(110,100),Text="非法改字"}};confirm(ref confirmPacket);
                check("绑定标牌改字被拒绝并回发原实体",confirmPacket.HandleMode==PacketHandleMode.Cancel&&ts.SignSyncs>0);
                confirmPacket=new(in info){Packet=new(){SignSlot=1,Position=new Point16(300,300),Text="普通标牌"}};confirm(ref confirmPacket);
                check("非绑定标牌保持原版处理",confirmPacket.HandleMode==PacketHandleMode.None);
            }
            finally{runner.Unbind();Commands.ChatCommands.Remove(signCommand);}
            var originalTiles=source.Main.tile;source.Main.tile=new TileCollection.DefaultTileCollection(400,400);
            check("同上下文同尺寸更换地图使旧actor失效",!actor!.In(source));source.Main.tile=originalTiles;
            var newSocket=new ProbeSocket();UnifiedServerCoordinator.globalClients[slot].Socket=newSocket;
            check("复用槽位换Socket不匹配旧actor",!actor!.Connected);UnifiedServerCoordinator.globalClients[slot].Socket=socket;
        }
        catch(Exception e){check("真实图格实体和标牌保护断言组",false);Console.Error.WriteLine(e);}
        finally
        {
            TShock.Players[slot]=oldTs;UnifiedServerCoordinator.players[slot]=oldP;
            UnifiedServerCoordinator.globalClients[slot].Socket=oldSocket;UnifiedServerCoordinator.globalClients[slot].State=oldState;
            SetRoute(slot,oldRoute);UnifiedServerCoordinator.RemoveServer(source);
        }
    }
    private static ServerContext World(string name)
    {
        var s=new ServerContext(name,new FixtureWorld(name),new Logger());
        s.Main.maxTilesX=400;s.Main.maxTilesY=400;s.Main.rightWorld=6400;s.Main.bottomWorld=6400;
        s.Main.tile=new TileCollection.DefaultTileCollection(400,400);
        typeof(ServerContext).GetProperty("IsRunning")!.SetValue(s,true);return s;
    }
    private static bool ThrowsArgument(Action action){try{action();return false;}catch(ArgumentException){return true;}}
    private static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.SetValue(o,value);
    private static object? Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(o);
    private static void SetRoute(int slot,ServerContext? server)=>typeof(UnifiedServerCoordinator).GetMethod("SetClientCurrentlyServer",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[slot,server]);
    private sealed class ProbePlayer(int index):TSPlayer(index)
    {
        internal int TileSyncs,SignSyncs;
        public override bool SendTileSquareCentered(int x,int y,byte size=10){TileSyncs++;return true;}
        public override void SendData(TrProtocol.MessageID type,string text="",int number=0,float n2=0,float n3=0,float n4=0,int n5=0){if(type==TrProtocol.MessageID.SignNew)SignSyncs++;}
    }
    private sealed class ProbeSocket:ISocket
    {
        internal bool Connected=true;
        public void Close()=>Connected=false;public bool IsConnected()=>Connected;public void Connect(RemoteAddress address)=>Connected=true;
        public void AsyncSend(byte[] data,int offset,int size,SocketSendCallback callback,object state)=>callback(state);
        public void AsyncSendNoCopy(byte[] data,int offset,int size,SocketSendCallback callback,object? state=null)=>callback(state!);
        public void AsyncSend(ReadOnlyMemory<byte> data,SocketSendCallback callback,object? state=null)=>callback(state!);
        public void AsyncReceive(byte[] data,int offset,int size,SocketReceiveCallback callback,object state){}
        public bool IsDataAvailable()=>false;public RemoteAddress GetRemoteAddress()=>new TcpAddress(System.Net.IPAddress.Loopback,7777);
        public bool StartListening(UnifiedServerProcess.RootContext root,SocketConnectionAccepted callback)=>false;public void StopListening(){}
    }
}
