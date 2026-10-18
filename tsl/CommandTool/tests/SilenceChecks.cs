using System.Reflection;
using System.Runtime.CompilerServices;
using CommandTool;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.Chat;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.DB;
using UnifierTSL;
using UnifierTSL.Commanding;
using UnifierTSL.Commanding.Composition;
using UnifierTSL.Logging;
using UnifierTSL.Servers;

internal static class SilenceChecks
{
    private delegate void SpawnOrig(TSPlayer self,PlayerSpawnContext context,int? timer);
    private delegate void SpawnDetour(SpawnOrig orig,TSPlayer self,PlayerSpawnContext context,int? timer);
    private delegate bool TeleportOrig(TSPlayer self,Vector2 position,bool useBottom,byte style);
    private delegate bool TeleportDetour(TeleportOrig orig,TSPlayer self,Vector2 position,bool useBottom,byte style);
    internal static async Task Run(Action<string,bool> check)
    {
        int chat=0;
        void ToClient(On.Terraria.Chat.ChatHelperSystemContext.orig_SendChatMessageToClient orig,ChatHelperSystemContext self,NetworkText text,Color color,int who)=>Interlocked.Increment(ref chat);
        void ToClientAs(On.Terraria.Chat.ChatHelperSystemContext.orig_SendChatMessageToClientAs orig,ChatHelperSystemContext self,byte author,NetworkText text,Color color,int who)=>Interlocked.Increment(ref chat);
        void Broadcast(On.Terraria.Chat.ChatHelperSystemContext.orig_BroadcastChatMessage orig,ChatHelperSystemContext self,NetworkText text,Color color,int exclude)=>Interlocked.Increment(ref chat);
        void BroadcastAs(On.Terraria.Chat.ChatHelperSystemContext.orig_BroadcastChatMessageAs orig,ChatHelperSystemContext self,byte author,NetworkText text,Color color,int exclude)=>Interlocked.Increment(ref chat);
        On.Terraria.Chat.ChatHelperSystemContext.SendChatMessageToClient+=ToClient;
        On.Terraria.Chat.ChatHelperSystemContext.SendChatMessageToClientAs+=ToClientAs;
        On.Terraria.Chat.ChatHelperSystemContext.BroadcastChatMessage+=Broadcast;
        On.Terraria.Chat.ChatHelperSystemContext.BroadcastChatMessageAs+=BroadcastAs;
        var runner=new ToolCommandRunner();var added=new List<Command>();
        IDisposable? registration=null;
        const int slot=4;
        var prior=TShock.Players[slot];var priorPlayer=UnifiedServerCoordinator.players[slot];
        var priorRoute=UnifiedServerCoordinator.GetClientCurrentlyServer(slot);
        var world=new ServerContext("静默探针",new FixtureWorld("静默探针"),new Logger());
        world.Main.maxTilesX=400;world.Main.maxTilesY=400;
        world.Main.rightWorld=400*16;world.Main.bottomWorld=400*16;
        var player=new Player{whoAmI=slot,active=true,width=20,height=42,name="A\" B\\C",position=new(1600,1600)};
        var group=new Group("fixture");var ts=new TSPlayer(slot){Group=group,FinishedHandshake=true};
        TShock.Players[slot]=ts;UnifiedServerCoordinator.players[slot]=player;world.Main.player[slot]=player;
        SetRoute(world);
        void Add(Command command){Commands.ChatCommands.Add(command);added.Add(command);}
        try
        {
            runner.Bind();
            ts.SendSuccessMessage("正常聊天");
            check("真实TSPlayer普通消息未被全局吞掉",chat==1);chat=0;
            using(ToolCommandRunner.SuppressChat())
            {
                ts.SendSuccessMessage("隐藏成功");ts.SendErrorMessage("隐藏错误");
                world.ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral("隐藏广播"),Color.White,-1);
                world.ChatHelper.BroadcastChatMessageAs(255,NetworkText.FromLiteral("隐藏As广播"),Color.White,-1);
                world.ChatHelper.SendChatMessageToClientAs(255,NetworkText.FromLiteral("隐藏As单发"),Color.White,slot);
            }
            check("标准TSPlayer成功错误和四类ChatHelper出口静默",chat==0);
            ts.SendInfoMessage("恢复正常");check("离开调用范围后普通聊天恢复",chat==1);chat=0;
            var go=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var ordinary=Task.Run(async()=>{await go.Task;ts.SendInfoMessage("另一个独立上下文的正常消息");});
            using(ToolCommandRunner.SuppressChat())
            {go.SetResult();await ordinary;ts.SendInfoMessage("同范围隐藏");}
            check("并发独立上下文的正常消息不受影响",chat==1);chat=0;

            int ran=0;bool granted=false,parameterSafe=false,lateCancelled=false,lateElevated=false;Task? late=null;
            var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Add(new Command("fixture.privileged",args=>
            {
                ran++;granted=args.Player.HasPermission("fixture.privileged");parameterSafe=args.Parameters.Single()==player.name;
                args.Player.SendSuccessMessage("legacy成功");args.Player.SendErrorMessage("legacy错误");
                world.ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral("legacy广播"),Color.White,-1);
                late=Task.Run(async()=>
                {
                    await release.Task;
                    try{lateElevated=args.Player!.HasPermission("fixture.privileged");args.Player.SendInfoMessage("返回后异步提示");}catch(OperationCanceledException){lateCancelled=true;}
                    world.ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral("延迟广播仍静默"),Color.White,-1);
                });
            },"fixture-auto"));
            bool accepted=await runner.ExecuteAsync(ts,world,new(){Command="fixture-auto $name",AsAdmin=true},()=>true);
            check("真实Legacy固定指令只执行一次",accepted&&ran==1);
            check("本次执行权限确实有效",granted);
            check("名字占位按参数替换而非注入文本",parameterSafe);
            check("Legacy内部成功错误广播无聊天",chat==0);
            release.SetResult();if(late is not null)await late;
            check("同一连接迟发普通逻辑可用但额外权限已撤回",!lateCancelled&&!lateElevated);
            check("继承上下文的返回后异步消息仍静默",chat==0);
            check("执行不改真实用户组和tempGroup",ReferenceEquals(ts.Group,group)&&ts.tempGroup is null&&!ts.HasPermission("fixture.privileged"));
            bool denied=await runner.ExecuteAsync(ts,world,new(){Command="fixture-auto name"},()=>true);
            check("普通权限不足时不执行且无聊天",!denied&&ran==1&&chat==0);
            int duplicated=0;Add(new Command(args=>duplicated++,"fixture-duplicate"));Add(new Command(args=>duplicated++,"fixture-duplicate"));
            _=await runner.ExecuteAsync(ts,world,new(){Command="fixture-duplicate"},()=>true);
            check("同名Legacy重复注册仍只执行一个处理器",duplicated==1);
            bool staleBlocked=false;Task? staleTask=null;var staleRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Add(new Command(args=>staleTask=Task.Run(async()=>
            {
                await staleRelease.Task;
                try{_ = args.Player!.TPlayer;}catch(OperationCanceledException){staleBlocked=true;}
            }),"fixture-stale"));
            _=await runner.ExecuteAsync(ts,world,new(){Command="fixture-stale"},()=>true);
            TShock.Players[slot]=new TSPlayer(slot){Group=new Group("replacement"),FinishedHandshake=true};
            try{staleRelease.SetResult();if(staleTask is not null)await staleTask;check("迟发动态getter拒绝复用槽位的新玩家",staleBlocked);}
            finally{TShock.Players[slot]=ts;}
            int thrown=0;
            Add(new Command(args=>{thrown++;throw new InvalidOperationException("fixture intentional");},"fixture-throw"));
            _=await runner.ExecuteAsync(ts,world,new(){Command="fixture-throw"},()=>true);
            check("执行后抛错不走另一路重放且错误静默",thrown==1&&chat==0);

            var configure=typeof(TShock).Assembly.GetType("TShockAPI.Commanding.TShockCommandRegistration")!
                .GetMethod("Configure",BindingFlags.Static|BindingFlags.Public)!
                .CreateDelegate<Action<CommandRegistrationBuilder>>();
            registration=CommandSystem.Install(configure);
            int spawnCalls=0,teleportCalls=0;
            using var spawnHook=new Hook(typeof(TSPlayer).GetMethod("Spawn",[typeof(PlayerSpawnContext),typeof(int?)])!,
                (SpawnDetour)((orig,self,context,timer)=>spawnCalls++));
            using var teleportHook=new Hook(typeof(TSPlayer).GetMethod("Teleport",[typeof(Vector2),typeof(bool),typeof(byte)])!,
                (TeleportDetour)((orig,self,position,bottom,style)=>{teleportCalls++;return true;}));
            var oldWarps=TShock.Warps;
            try
            {
                var manager=(WarpManager)RuntimeHelpers.GetUninitializedObject(typeof(WarpManager));
                typeof(WarpManager).GetField("Warps",BindingFlags.Instance|BindingFlags.NonPublic)!
                    .SetValue(manager,new List<Warp>{new(world.Main.worldID.ToString(),new Point(100,150),"大厅")});
                TShock.Warps=manager;
                bool home=await runner.ExecuteAsync(ts,world,new(){Command="home",AsAdmin=true},()=>true);
                check("真实TShockV2 home成功结果不输出",home&&spawnCalls==1&&chat==0);
                bool warp=await runner.ExecuteAsync(ts,world,new(){Command="warp 大厅",AsAdmin=true},()=>true);
                check("真实TShockV2 warp成功结果不输出",warp&&teleportCalls==1&&chat==0);
                _=await runner.ExecuteAsync(ts,world,new(){Command="warp 不存在",AsAdmin=true},()=>true);
                check("真实TShockV2 warp错误也不输出",teleportCalls==1&&chat==0);
                player.dead=true;ts.Dead=true;
                _=await runner.ExecuteAsync(ts,world,new(){Command="home",AsAdmin=true},()=>true);
                check("真实TShockV2 home死亡错误也不输出",spawnCalls==1&&chat==0);
                player.dead=false;ts.Dead=false;
            }
            finally{TShock.Warps=oldWarps;}
            runner.Unbind();ts.SendInfoMessage("卸载后正常消息");
            check("卸载后没有遗留全局聊天拦截",chat==1);
        }
        catch(Exception e){check("静默和权限真实API断言组",false);Console.Error.WriteLine(e);}
        finally
        {
            runner.Unbind();registration?.Dispose();foreach(var command in added)Commands.ChatCommands.Remove(command);
            TShock.Players[slot]=prior;UnifiedServerCoordinator.players[slot]=priorPlayer;SetRoute(priorRoute);
            On.Terraria.Chat.ChatHelperSystemContext.SendChatMessageToClient-=ToClient;
            On.Terraria.Chat.ChatHelperSystemContext.SendChatMessageToClientAs-=ToClientAs;
            On.Terraria.Chat.ChatHelperSystemContext.BroadcastChatMessage-=Broadcast;
            On.Terraria.Chat.ChatHelperSystemContext.BroadcastChatMessageAs-=BroadcastAs;
        }
        void SetRoute(ServerContext? s)=>typeof(UnifiedServerCoordinator).GetMethod("SetClientCurrentlyServer",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[slot,s]);
    }
}
