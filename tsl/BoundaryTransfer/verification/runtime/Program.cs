using System.Collections;
using System.Reflection;
using BoundaryTransfer;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TShockAPI;
using TrProtocol.NetPackets;
using UnifierTSL;
using UnifierTSL.Events.Handlers;
using UnifierTSL.Logging;
using UnifierTSL.Servers;

var log = Path.Combine(AppContext.BaseDirectory, "runtime.log");
File.WriteAllText(log, "BoundaryTransfer 真实运行时/协议钩子离线验证\n");
int passed = 0;
void L(string s) { File.AppendAllText(log, s + Environment.NewLine); Console.WriteLine(s); }
void Check(string s, bool ok) { L((ok ? "PASS " : "FAIL ") + s); if (!ok) throw new Exception(s); passed++; }
try
{
    typeof(UnifierApi).Assembly.GetType("UnifierTSL.Surface.Hosting.LauncherSurfaceConsole")!
        .GetMethod("Initialize", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[new TestConsole()]);
    Initializer.Initialize();
    // CI/重定向输出没有控制台句柄；仅离线探针固定终端尺寸，避免发行版 SampleServer 构造失败。
    using var terminalHeight=new MonoMod.RuntimeDetour.Hook(typeof(Console).GetProperty(nameof(Console.WindowHeight))!.GetMethod!,
        (Func<Func<int>,int>)(_=>50));
    using var terminalWidth=new MonoMod.RuntimeDetour.Hook(typeof(Console).GetProperty(nameof(Console.WindowWidth))!.GetMethod!,
        (Func<Func<int>,int>)(_=>100));
    using var terminalLeft=new MonoMod.RuntimeDetour.Hook(typeof(Console).GetProperty(nameof(Console.WindowLeft))!.GetMethod!,
        (Func<Func<int>,int>)(_=>0));
    using var terminalTop=new MonoMod.RuntimeDetour.Hook(typeof(Console).GetProperty(nameof(Console.WindowTop))!.GetMethod!,
        (Func<Func<int>,int>)(_=>0));
    typeof(UnifierApi).GetField("<EventHub>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,new EventHub());
    TShock.Config = new TShockAPI.Configuration.TShockConfig(new TestRegistrar(), "TShock");
    var source = World("中间世界");
    var target = World("左侧世界");
    int originalSpawnX = source.Main.spawnTileX = 250, originalSpawnY = source.Main.spawnTileY = 100;
    int targetSpawnX = target.Main.spawnTileX = 255, targetSpawnY = target.Main.spawnTileY = 120;
    var settings = BoundarySettings.FromConfig(new() { WorldOrder = ["左侧世界","中间世界","右侧世界"] });
    var map = new TerrariaLandingMap(target);
    Check("真实TileData空气不阻挡", !map.Get(400,1100).BlocksBody);
    Floor(target, 1100, 0);
    Check("真实石块/土块承重", map.Get(400,1100).SupportsFeet);
    ref var cell = ref target.Main.tile[400,1100];
    cell.halfBrick(true);
    Check("真实半砖不当完整脚底面", !map.Get(400,1100).SupportsFeet);
    cell.halfBrick(false); cell.slope(1);
    Check("真实斜坡不当完整脚底面", !map.Get(400,1100).SupportsFeet);
    cell.slope(0); cell.inActive(true);
    Check("真实虚化块不承重", !map.Get(400,1100).SupportsFeet);
    cell.inActive(false);
    foreach (int type in new[] { 37, 48, 58, 76, 232, 137, 138, 141, 162 })
        Check($"危险方块元数据 {type} 被拒绝", TerrariaLandingMap.IsHazardousType(type));
    Check("灰烬不是危险地面", !TerrariaLandingMap.IsHazardousType(57));
    for (int kind=0; kind<4; kind++)
    {
        ref var liquid = ref target.Main.tile[401,1099];
        liquid.liquid=255; liquid.liquidType(kind);
        Check($"真实液体种类 {kind} 全部不选为落点", map.Get(401,1099).Liquid);
        liquid.liquid=0;
    }
    var right = BoundaryPlanner.Find(map,BoundarySide.Right,1100*16,20,42,settings);
    Check("真实图格地狱同Y找到右侧落点",right.Success && right.Spot.FeetY==1100 && right.Spot.X>target.Main.maxTilesX*8);
    var left = BoundaryPlanner.Find(map,BoundarySide.Left,1100*16,20,42,settings);
    Check("真实图格地狱同Y找到左侧落点",left.Success && left.Spot.FeetY==1100 && left.Spot.X<target.Main.maxTilesX*8);
    Check("真实引擎SolidCollision确认身体无碰撞", !Collision.SolidCollision(target,new(right.Spot.X,right.Spot.Y),20,42));
    Check("真实引擎液体检测确认干燥", !Collision.WetCollision(target,new(right.Spot.X,right.Spot.Y),20,42,out _,out _));
    Check("安全搜索从未改世界出生点",source.Main.spawnTileX==originalSpawnX && source.Main.spawnTileY==originalSpawnY && target.Main.spawnTileX==targetSpawnX && target.Main.spawnTileY==targetSpawnY);
    FallbackChecks(Check,World,Floor);

    var registrar = new TestRegistrar();
    var plugin = new BoundaryTransferPlugin();
    await plugin.InitializeAsync(registrar,[]);
    var handle = (TestConfig<BoundaryTransferConfig>)registrar.Handle!;
    Check("所有边界别名不进入公共指令目录",Commands.GetRegisteredCommands().All(c=>!c.HasAlias("边界") && !c.HasAlias("boundarytransfer") && !c.HasAlias("borders")));
    Check("默认无世界排列不自动误传",Settings(plugin).Worlds.Length==0);
    Check("配置回调注册一次",handle.SubscriberCount==1);
    await handle.Change(settings.ToConfig());
    Check("原生配置回调重载排列",Settings(plugin).Worlds.SequenceEqual(settings.Worlds));
    await handle.Change(new() { WorldOrder=["重复","重复"] });
    Check("无效热重载保留原设置",Settings(plugin).Worlds.SequenceEqual(settings.Worlds));

    const int slot=0;
    var player = new Player { whoAmI=slot, name="边界验证玩家", active=true, width=20,height=42, position=new(48*16,1100*16-42), SpawnX=123,SpawnY=456 };
    var ts = new QuietPlayer(slot);
    var socket = new CaptureSocket();
    typeof(TSPlayer).GetField("FakePlayer",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(ts,player);
    TShock.Players[slot]=ts;
    UnifiedServerCoordinator.players[slot]=player;
    UnifiedServerCoordinator.globalClients[slot].Socket=socket;
    UnifiedServerCoordinator.globalClients[slot].State=10;
    target.Main.player[slot]=player;
    SetRoute(slot,target);
    HiddenCommandChecks(plugin,target,source,ts,Check);
    var session=NewPrivate("PlayerSession",player,ts,socket);
    TouchBoundaryChecks(target,player,Check);
    // 自动失败必须静默；这里只驱动票据失败，不发起真实网络转移。
    var rejected=NewPrivate("TransferTicket",slot,session,source,target,BoundarySide.Left,settings,Environment.TickCount64);
    Set(session,"Pending",rejected); Sessions(plugin)[slot]=session; ts.Messages.Clear();
    typeof(BoundaryTransferPlugin).GetMethod("Fail",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,[rejected,"切换准备失败，本次不切换。"]);
    Check("边界切换自动失败不发送任何玩家聊天",ts.Messages.Count==0);
    var direct=BoundaryPlanner.Direct(BoundarySide.Right, player.Bottom.Y, player.width, player.height,
        new BoundaryBounds(target.Main.leftWorld,target.Main.rightWorld,target.Main.topWorld,target.Main.bottomWorld));
    Check("1.1.0直达落点保持接触高度并贴对侧原生边界",
        direct.Success && direct.Spot.Y==player.Bottom.Y-player.height
        && direct.Spot.X==target.Main.rightWorld-640-player.width);
    Check("静默失败仍清理票据保留原连接",session.GetType().GetField("Pending")!.GetValue(session) is null && socket.Connected);
    var ticket=NewPrivate("TransferTicket",slot,session,source,target,BoundarySide.Left,settings,Environment.TickCount64);
    Set(session,"Pending",ticket);
    Sessions(plugin)[slot]=session;
    Set(ticket,"Spot",right.Spot); Set(ticket,"Routing",true); Set(ticket,"NativeComplete",true); Set(ticket,"Landed",true);
    Set(ticket,"LastTeleport",Environment.TickCount64); Set(ticket,"GuardUntil",Environment.TickCount64+2500);

    Set(ticket,"NativeComplete",false); Set(ticket,"NeedsLanding",false);
    UnifierApi.EventHub.Coordinator.PostServerTransfer.Invoke(new(source,target,slot));
    Check("原生Post事件不提前发布NativeComplete",!Flag(ticket,"NativeComplete") && !Flag(ticket,"NeedsLanding"));
    Set(ticket,"Routing",false);
    UnifierApi.EventHub.Coordinator.PostServerTransfer.Invoke(new(source,target,slot));
    Check("预检期间外部转服会取消旧票据",session.GetType().GetField("Pending")!.GetValue(session) is null);
    Set(session,"Pending",ticket); Set(ticket,"Routing",true); Set(ticket,"NativeComplete",true);
    PacketChecks(plugin,target,slot,ticket,right.Spot,Check);
    Check("包钩子从未将玩家SpawnX/Y当坐标",player.SpawnX==123 && player.SpawnY==456);
    var sameWorld=typeof(BoundaryTransferPlugin).GetMethod("SameWorld",BindingFlags.Static|BindingFlags.NonPublic)!;
    var snapshot=ticket.GetType().GetField("TargetWorld")!.GetValue(ticket);
    Check("原地图身份匹配",(bool)sameWorld.Invoke(null,[target,snapshot])!);
    var oldTiles=target.Main.tile;
    target.Main.tile=new TileCollection.DefaultTileCollection(600,1200);
    Check("同尺寸同上下文换图格容器也能识别",!(bool)sameWorld.Invoke(null,[target,snapshot])!);
    target.Main.tile=oldTiles;
    Set(ticket,"ClientArrived",false); Set(ticket,"Deadline",Environment.TickCount64-1);
    _=ControlsCancelled(plugin,target,slot,new(0,0));
    Check("未确认切换超时关闭捕获连接",!socket.Connected);
    Check("超时后票据正确清除",session.GetType().GetField("Pending")!.GetValue(session) is null);
    socket.Connected=true; Set(session,"Pending",ticket); Set(ticket,"Deadline",Environment.TickCount64+20000);
    Set(ticket,"NeedsLanding",false); Set(ticket,"ClientArrived",false); Set(ticket,"GuardUntil",Environment.TickCount64-1);
    typeof(BoundaryTransferPlugin).GetMethod("ProcessPending",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,[target,Environment.TickCount64]);
    Check("落点保护到期仍未确认则安全关闭原连接",!socket.Connected);
    socket.Connected=true; Set(session,"Pending",ticket); Set(ticket,"GuardUntil",Environment.TickCount64+2500);
    Set(ticket,"Routing",false);
    Check("合法配置应用成功",plugin.ApplyConfig(settings.ToConfig()));
    Check("热重载取消尚未原生切换的预检",session.GetType().GetField("Pending")!.GetValue(session) is null);
    Set(session,"Pending",ticket); Set(ticket,"Routing",true);
    SetRoute(slot,target);

    // 槽位不变、socket已换时，旧票据不得拦截新连接。
    UnifiedServerCoordinator.globalClients[slot].Socket=new CaptureSocket();
    Check("换socket后不误处理新连接", !ControlsCancelled(plugin,target,slot,new(0,0)));
    UnifiedServerCoordinator.globalClients[slot].Socket=socket;
    TShock.Players[slot]=new QuietPlayer(slot);
    Check("换TSPlayer对象后不误处理新会话", !ControlsCancelled(plugin,target,slot,new(0,0)));
    TShock.Players[slot]=ts;

    // 不调用真实网络Transfer冒充客户端；仅驱动原生事件后的状态机。
    var newSocket=new CaptureSocket();
    UnifiedServerCoordinator.globalClients[slot].Socket=newSocket;
    Set(session,"Pending",ticket); Set(ticket,"NativeComplete",false); Set(ticket,"Landed",false);
    await plugin.ShutdownAsync();
    Check("卸载残留切换只关闭捕获旧socket",!socket.Connected);
    Check("卸载不误关闭复用槽位的新socket",newSocket.Connected);
    Check("关闭解除配置回调",handle.SubscriberCount==0);
    Check("关闭后公共目录仍不出现边界命令",Commands.ChatCommands.All(c=>!c.HasAlias("boundarytransfer")));
    var afterShutdown = new MessageEvent(new MessageSender(target,0),"/边界 列表","/边界 列表");
    UnifierApi.EventHub.Chat.MessageEvent.Invoke(in afterShutdown,out bool stillHandled);
    Check("关闭移除隐藏命令消息钩子",!stillHandled);
    Check("关闭清空会话",Sessions(plugin).Count==0);
    await plugin.DisposeAsync();
    Check("关闭和Dispose重复执行不报错",true);
    Check("整个离线探针没有改变世界出生点",source.Main.spawnTileX==originalSpawnX && source.Main.spawnTileY==originalSpawnY && target.Main.spawnTileX==targetSpawnX && target.Main.spawnTileY==targetSpawnY);
    L($"RESULT PASS={passed} FAIL=0");
    Environment.ExitCode=0;
}
catch(Exception ex) { L("UNHANDLED "+ex); Environment.ExitCode=1; }

static ServerContext World(string name)
{
    var s=new ServerContext(name,new FakeWorld(name),new Logger());
    s.Main.maxTilesX=600;s.Main.maxTilesY=1200;s.Main.rightWorld=600*16;s.Main.bottomWorld=1200*16;
    s.Main.maxSectionsX=3;s.Main.maxSectionsY=8;
    s.Main.tile=new TileCollection.DefaultTileCollection(600,1200);
    s.Main.tileSolid[0]=true;s.Main.tileSolid[1]=true;s.Main.tileSolid[57]=true;
    return s;
}
static void Floor(ServerContext s,int row,ushort type)
{
    for(int x=0;x<s.Main.maxTilesX;x++){ref var t=ref s.Main.tile[x,row];t.type=type;t.active(true);}
}
static BoundarySettings Settings(object plugin)=>(BoundarySettings)typeof(BoundaryTransferPlugin).GetField("_settings",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!;
static IDictionary Sessions(object plugin)=>(IDictionary)typeof(BoundaryTransferPlugin).GetField("_sessions",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!;
static object NewPrivate(string name,params object[] args)=>Activator.CreateInstance(typeof(BoundaryTransferPlugin).GetNestedType(name,BindingFlags.NonPublic)!,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,args,null)!;
static void Set(object target,string field,object? value)=>target.GetType().GetField(field,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(target,value);
static bool Flag(object target,string field)=>(bool)target.GetType().GetField(field)!.GetValue(target)!;
static void SetRoute(int slot,ServerContext server)=>typeof(UnifiedServerCoordinator).GetMethod("SetClientCurrentlyServer",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[slot,server]);
static unsafe bool ControlsCancelled(BoundaryTransferPlugin plugin,ServerContext server,int slot,Vector2 position)
{
    ReceiveBytesInfo info=new(UnifiedServerCoordinator.clientSenders[slot],server.PacketReceiver,null,null);
    ReceivePacketEvent<PlayerControls> packet=new(in info);
    packet.Packet.Position=position;
    var handler=typeof(BoundaryTransferPlugin).GetMethod("OnControlsPacket",BindingFlags.NonPublic|BindingFlags.Instance)!.CreateDelegate<ReceivePacket<PlayerControls>>(plugin);
    handler(ref packet);
    return packet.HandleMode==PacketHandleMode.Cancel && packet.StopPropagation;
}
static unsafe void SpawnCheck(BoundaryTransferPlugin plugin,ServerContext server,int slot,object ticket,Action<string,bool> check,string prefix)
{
    ReceiveBytesInfo info=new(UnifiedServerCoordinator.clientSenders[slot],server.PacketReceiver,null,null);
    ReceivePacketEvent<SpawnPlayer> packet=new(in info);
    packet.Packet.Context=PlayerSpawnContext.SpawningIntoWorld;
    var handler=typeof(BoundaryTransferPlugin).GetMethod("OnSpawnPacket",BindingFlags.NonPublic|BindingFlags.Instance)!.CreateDelegate<ReceivePacket<SpawnPlayer>>(plugin);
    handler(ref packet);
    check(prefix+"Spawn先附加处理后回调",packet.PacketProcessed is not null);
    check(prefix+"Spawn不抢原生数据包处理",packet.HandleMode==PacketHandleMode.None);
    packet.PacketProcessed!.Invoke(in packet,PacketHandleMode.Cancel);
    check(prefix+"TShock已处理Cancel仍请求重新定位",Flag(ticket,"NeedsLanding"));
}
static unsafe void PacketChecks(BoundaryTransferPlugin plugin,ServerContext server,int slot,object ticket,LandingSpot spot,Action<string,bool> check)
{
    check("切图后旧地图坐标被短时阻断",ControlsCancelled(plugin,server,slot,new(48*16,1100*16-42)));
    check("到达落点的坐标包放行",!ControlsCancelled(plugin,server,slot,new(spot.X,spot.Y)));
    check("到达坐标形成确认",Flag(ticket,"ClientArrived"));
    check("确认后正常走路不锁回落点",!ControlsCancelled(plugin,server,slot,new(spot.X-200,spot.Y)));
    Set(ticket,"NeedsLanding",false);
    SpawnCheck(plugin,server,slot,ticket,check,"首次");
}
sealed class QuietPlayer(int slot):TSPlayer(slot)
{
    public List<string> Messages { get; } = [];
    public override void SendMessage(string msg,byte r,byte g,byte b)=>Messages.Add(msg);
    public override void SendMessage(string msg,Color color)=>Messages.Add(msg);
}
