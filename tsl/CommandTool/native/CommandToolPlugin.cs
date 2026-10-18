using System.Collections.Immutable;
using Terraria;
using TrProtocol.NetPackets;
using TShockAPI;
using TShockAPI.Hooks;
using UnifierTSL;
using UnifierTSL.Events.Core;
using UnifierTSL.Events.Handlers;
using UnifierTSL.Plugins;
using UnifierTSL.Module;
using UnifierTSL.Servers;

[assembly: CoreModule]

namespace CommandTool;

[PluginMetadata("CommandTool","2.5.0","Pigeon-X","UTSL原生开关/标牌/区域指令工具：点1/点2单格选角、占位1/占位2玩家占位框、区域外扩5格不可破坏；不处理传送/落点；隐藏入口不含ct。")]
public sealed partial class CommandToolPlugin:BasePlugin
{
    private const int MovedByCommandSuppressMs = 3000;
    private readonly object _gate=new();
    private readonly Dictionary<int,ToolPlayerState> _players=[];
    private readonly Dictionary<ServerContext,long> _poll=[];
    private volatile ToolConfig _settings=new();
    private IPluginConfigHandle<ToolConfig>? _handle;
    private ToolCommandRunner _runner=new();
    private volatile bool _running;
    private long _generation;
    public override int InitializationOrder=>TShock.Order+1;
    public override async Task InitializeAsync(IPluginConfigRegistrar registrar,ImmutableArray<PluginInitInfo> prior,CancellationToken cancellationToken=default)
    {
        foreach(var item in prior)
        {
            if(item.Plugin.Name=="TShock")await item.InitializationTask.ConfigureAwait(false);
            if(item.Plugin.Name=="SignCommand")throw new InvalidOperationException("CommandTool已合并标牌指令，请先停用旧SignCommand，避免双重执行。");
        }
        _handle=registrar.CreateConfigRegistration<ToolConfig>("CommandTool.v2.json",ConfigFormat.NewtonsoftJson)
            .WithDefault(static()=>new()).OnDeserializationFailure(DeserializationFailureHandling.ReturnNewInstance,false)
            .TriggerReloadOnExternalChange(true).Complete();
        await _handle.RequestAsync(cancellationToken:cancellationToken).ConfigureAwait(false);
        Apply(ToolConfigStore.Read(_handle.FilePath));_handle.OnChangedAsync+=Changed;
        _runner.Bind();BindProtection();_running=true;
        NetPacketHandler.Register<HitSwitch>(OnSwitch,HandlerPriority.Highest);
        NetPacketHandler.Register<RequestReadSign>(OnSignClick,HandlerPriority.Highest);
        NetPacketHandler.Register<ReadSign>(OnSignConfirm,HandlerPriority.Highest);
        NetPacketHandler.Register<TileChange>(OnTile,HandlerPriority.Highest);
        UnifierApi.EventHub.Chat.MessageEvent.Register(OnHidden,HandlerPriority.Highest);
        UnifierApi.EventHub.Game.PostUpdate.Register(OnUpdate,HandlerPriority.Lowest);
        GeneralHooks.ReloadEvent+=OnReload;
        UnifierApi.Logger.Info("[CommandTool] 2.5.0指令版已加载；点1/点2单格、占位1/占位2玩家占位框，区域及外扩保护，不处理传送/落点。配置："+_handle.FilePath);
    }
    private ValueTask<bool> Changed(IPluginConfigHandle<ToolConfig> handle,ToolConfig? value)
    {try{Reload();}catch(Exception e){UnifierApi.Logger.Warning("[CommandTool] 无效重载保留旧配置："+e.Message);}return ValueTask.FromResult(false);}
    private void Reload()
    {
        lock(_gate)
        {
            if(_handle is null)return;
            var next=ToolConfigStore.Read(_handle.FilePath);
            _handle.ModifyInMemory(_=>next);Apply(next);
        }
    }
    private void OnReload(ReloadEventArgs args){try{Reload();}catch(Exception e){UnifierApi.Logger.Warning("[CommandTool] 重载失败："+e.Message);}}
    private void Apply(ToolConfig config)
    {
        var next=ToolValidation.Snapshot(config);
        lock(_gate){_settings=next;Interlocked.Increment(ref _generation);_players.Clear();_poll.Clear();}
    }
    private void Save(ToolConfig config)
    {
        var next=ToolValidation.Snapshot(config);
        lock(_gate)
        {
            if(_handle is null)throw new InvalidOperationException("插件已卸载。");
            ToolConfigStore.SaveAtomic(_handle.FilePath,next);
            _handle.ModifyInMemory(_=>next);Apply(next);
        }
    }
    public override Task ShutdownAsync(CancellationToken cancellationToken=default)
    {
        _running=false;Interlocked.Increment(ref _generation);
        NetPacketHandler.UnRegister<HitSwitch>(OnSwitch);NetPacketHandler.UnRegister<RequestReadSign>(OnSignClick);
        NetPacketHandler.UnRegister<ReadSign>(OnSignConfirm);NetPacketHandler.UnRegister<TileChange>(OnTile);
        UnifierApi.EventHub.Chat.MessageEvent.UnRegister(OnHidden);UnifierApi.EventHub.Game.PostUpdate.UnRegister(OnUpdate);
        GeneralHooks.ReloadEvent-=OnReload;
        UnbindProtection();_runner.Unbind();
        if(_handle is not null)_handle.OnChangedAsync-=Changed;
        lock(_gate){_players.Clear();_poll.Clear();}_handle=null;return Task.CompletedTask;
    }
    public override async ValueTask DisposeAsync(bool disposing){await ShutdownAsync().ConfigureAwait(false);await base.DisposeAsync(disposing).ConfigureAwait(false);}
    private ToolPlayerState State(ToolActor actor)
    {
        lock(_gate)
        {
            _players.TryGetValue(actor.Slot,out var state);
            if(state is null||!state.Actor.Matches(actor)||state.Generation!=_generation)
            {
                long suppression=state is not null&&ReferenceEquals(state.Actor.Socket,actor.Socket)
                    &&ReferenceEquals(state.Actor.Player,actor.Player)?state.Region.SuppressUntil:0;
                _players[actor.Slot]=state=new(actor,_generation);
                state.Region.SuppressUntil=suppression;
            }
            return state;
        }
    }
    private bool Current(ToolPlayerState state)=>_running&&state.Generation==Volatile.Read(ref _generation)&&state.Actor.In(state.Actor.Source);
    private bool InWorld(ToolBinding binding,ServerContext server)=>Resolve(binding.World)==server;
    private bool Active(ToolBinding binding)=>_settings.Enabled&&binding.Enabled&&binding.Actions.Count>0;
    private PointBinding? Point(List<PointBinding> points,ServerContext server,ToolEntity entity)
        =>points.FirstOrDefault(p=>Active(p)&&InWorld(p,server)&&entity.Contains(p.X,p.Y));
    private void SuppressRegion(ToolActor actor,int milliseconds)
    {
        lock(_gate)
            if(_players.TryGetValue(actor.Slot,out var p)&&ReferenceEquals(p.Actor.Socket,actor.Socket))
            {p.Region.SuppressUntil=Environment.TickCount64+milliseconds;p.Region.Dwelling=null;}
    }
    private void OnUpdate(ref ReadonlyNoCancelEventArgs<ServerEvent> args)
    {
        if(!_running)return;var server=args.Content.Server;long now=Environment.TickCount64;
        var config=_settings;if(!config.Enabled||!server.IsRunning)return;
        lock(_gate){if(_poll.GetValueOrDefault(server)>now)return;_poll[server]=now+config.PollMs;}
        for(int slot=0;slot<server.Main.player.Length&&slot<TShock.Players.Length;slot++)
        {
            var actor=ToolActor.Capture(server,slot);
            if(actor is null)
            {
                lock(_gate)if(_players.TryGetValue(slot,out var invalid)&&invalid.Actor.Source==server)invalid.Region.Reset();
                continue;
            }
            var state=State(actor);if(state.Executing)continue;
            var regions=config.Regions.Where(r=>Active(r)&&InWorld(r,server)
                &&(string.IsNullOrWhiteSpace(r.UsePermission)||actor.TS.HasPermission(r.UsePermission)));
            RegionBinding? fire;lock(_gate)fire=state.Region.Advance(regions,actor.Player.Center.X/16,actor.Player.Bottom.Y/16,now);
            if(fire is not null)Execute(state,fire);
        }
        lock(_gate)foreach(var slot in _players.Where(p=>!p.Value.Actor.Connected).Select(p=>p.Key).ToArray())_players.Remove(slot);
    }
    private void Execute(ToolPlayerState state,ToolBinding binding)
    {
        lock(_gate)
        {
            if(!Current(state)||state.Executing
                ||state.Cooldowns.GetValueOrDefault(binding.Id)>Environment.TickCount64)return;
            state.Cooldowns[binding.Id]=Environment.TickCount64+Math.Max(250,binding.CooldownMs);state.Executing=true;
        }
        if(!string.IsNullOrWhiteSpace(binding.UsePermission)&&!state.Actor.TS.HasPermission(binding.UsePermission)) {state.Executing=false;return;}
        RunAction(state,binding,0);
    }
    private void RunAction(ToolPlayerState state,ToolBinding binding,int index)
    {
        if(!Current(state)||index>=binding.Actions.Count) {state.Executing=false;return;}
        try
        {
            var action=binding.Actions[index];var before=state.Actor.Player.position;
            if(!ToolCommandRunner.MatchesGroup(state.Actor.TS.Group,action.RequireGroup)) {RunAction(state,binding,index+1);return;}
            var task=_runner.ExecuteAsync(state.Actor.TS,state.Actor.Source,action,()=>Current(state));
            void Continue(Task<bool> done)
            {
                if(state.Actor.Connected&&UnifiedServerCoordinator.GetClientCurrentlyServer(state.Actor.Slot)!=state.Actor.Source)
                {SuppressRegion(state.Actor,MovedByCommandSuppressMs);state.Executing=false;return;}
                if(!done.IsCompletedSuccessfully||!done.Result||!Current(state)) {state.Executing=false;return;}
                // 指令自身可以移动玩家（例如warp）；此时结束本链，避免在目标区域立即再次触发。
                if(Microsoft.Xna.Framework.Vector2.DistanceSquared(before,state.Actor.Player.position)>16)
                {SuppressRegion(state.Actor,MovedByCommandSuppressMs);state.Executing=false;return;}
                RunAction(state,binding,index+1);
            }
            if(task.IsCompleted)Continue(task);
            else _=task.ContinueWith(done=>
            {
                if(!Current(state))
                {
                    if(state.Actor.Connected&&UnifiedServerCoordinator.GetClientCurrentlyServer(state.Actor.Slot)!=state.Actor.Source)
                        SuppressRegion(state.Actor,MovedByCommandSuppressMs);
                    state.Executing=false;return;
                }
                try {state.Actor.Source.Dispatcher.Post(()=>Continue(done));}
                catch(Exception e){state.Executing=false;UnifierApi.Logger.Warning("[CommandTool] 指令续链取消："+e.Message);}
            },TaskScheduler.Default);
        }
        catch(Exception e){state.Executing=false;UnifierApi.Logger.Warning("[CommandTool] 自动动作异常（不向玩家输出）："+e);}
    }
    private void OnSwitch(ref ReceivePacketEvent<HitSwitch> args)
    {
        if(!_running)return;var actor=ToolActor.Capture(args.Server,args.Who);if(actor is null)return;
        int x=args.Packet.Position.X,y=args.Packet.Position.Y;
        if(!ToolEntities.Near(actor,x,y))return;var entity=ToolEntities.Switch(args.Server,x,y);if(entity is null)return;
        var state=State(actor);
        if(Select(state,"开关",entity.Value)) {args.HandleMode=PacketHandleMode.Cancel;args.StopPropagation=true;return;}
        var binding=Point(_settings.Switches,args.Server,entity.Value);
        if(binding is not null)Execute(state,binding); // 只执行指令；机械行为仍交给原版。
    }
    private void OnSignClick(ref ReceivePacketEvent<RequestReadSign> args)
    {
        if(!_running)return;var actor=ToolActor.Capture(args.Server,args.Who);if(actor is null)return;
        int x=args.Packet.Position.X,y=args.Packet.Position.Y;
        if(!ToolEntities.Near(actor,x,y))return;var entity=ToolEntities.Sign(args.Server,x,y);if(entity is null)return;
        var state=State(actor);
        if(Select(state,"标牌",entity.Value))return;
        var binding=Point(_settings.Signs,args.Server,entity.Value);
        if(binding is null||!_settings.SignClick)return;
        args.HandleMode=PacketHandleMode.Cancel;args.StopPropagation=true;
        state.ConfirmX=entity.Value.X;state.ConfirmY=entity.Value.Y;state.ConfirmSuppressUntil=Environment.TickCount64+30000;
        if(!_settings.StrictSignText||binding.Text is null||binding.Text==entity.Value.Text)Execute(state,binding);
    }
    private void OnSignConfirm(ref ReceivePacketEvent<ReadSign> args)
    {
        if(!_running)return;var actor=ToolActor.Capture(args.Server,args.Who,false);if(actor is null)return;
        int x=args.Packet.Position.X,y=args.Packet.Position.Y;
        int signSlot=args.Packet.SignSlot;
        var indexed=signSlot>=0&&signSlot<args.Server.Main.sign.Length?args.Server.Main.sign[signSlot]:null;
        var indexedBinding=indexed is null?null:Point(_settings.Signs,args.Server,new ToolEntity(indexed.x,indexed.y,2,2,signSlot,indexed.text));
        var entity=ToolEntities.Sign(args.Server,x,y);
        if(indexedBinding is not null&&(entity is null||entity.Value.SignIndex!=signSlot))
        {args.HandleMode=PacketHandleMode.Cancel;args.StopPropagation=true;return;}
        if(entity is null)return;
        var binding=Point(_settings.Signs,args.Server,entity.Value);if(binding is null)return;
        if(entity.Value.SignIndex!=signSlot)
        {args.HandleMode=PacketHandleMode.Cancel;args.StopPropagation=true;return;}
        if(!ToolEntities.Near(actor,x,y)) {args.HandleMode=PacketHandleMode.Cancel;args.StopPropagation=true;return;}
        string incoming=args.Packet.Text??"";
        if(incoming!=entity.Value.Text)
        {
            if(!_settings.ProtectSigns||CanEdit(actor.TS,"标牌")) {args.PacketProcessed+=SignEdited;return;}
            args.HandleMode=PacketHandleMode.Cancel;args.StopPropagation=true;
            actor.TS.SendData(TrProtocol.MessageID.SignNew,"",entity.Value.SignIndex,byte.MaxValue);return;
        }
        args.HandleMode=PacketHandleMode.Cancel;args.StopPropagation=true;
        var state=State(actor);
        if(state.SelectionUntil>Environment.TickCount64&&state.SelectedKind=="标牌"&&state.SelectedX==entity.Value.X&&state.SelectedY==entity.Value.Y)return;
        if(state.ConfirmSuppressUntil>Environment.TickCount64&&state.ConfirmX==entity.Value.X&&state.ConfirmY==entity.Value.Y)return;
        if(_settings.SignConfirm&&(!_settings.StrictSignText||binding.Text is null||binding.Text==entity.Value.Text))Execute(state,binding);
    }
    private void SignEdited(in ReceivePacketEvent<ReadSign> args,PacketHandleMode mode)
    {
        if(mode==PacketHandleMode.Cancel||!_running)return;
        var entity=ToolEntities.Sign(args.Server,args.Packet.Position.X,args.Packet.Position.Y);if(entity is null)return;
        var server=args.Server;
        var copy=ToolValidation.Snapshot(_settings);var bound=copy.Signs.FirstOrDefault(p=>p.Enabled&&InWorld(p,server)&&entity.Value.Contains(p.X,p.Y));
        if(bound is null)return;bound.Text=entity.Value.Text;Save(copy);
    }
    private bool CanEdit(TSPlayer player,string kind)=>player.HasPermission(_settings.EditPermission)
        ||(kind=="标牌"&&!string.IsNullOrWhiteSpace(_settings.LegacySignEditPermission)&&player.HasPermission(_settings.LegacySignEditPermission));
    private void OnTile(ref ReceivePacketEvent<TileChange> args)
    {
        if(!_running||!_settings.Enabled)return;var actor=ToolActor.Capture(args.Server,args.Who,false);if(actor is null)return;
        int x=args.Packet.Position.X,y=args.Packet.Position.Y;
        // 选区期间该玩家不能挖/改图格，防止设置set1/set2时顺手把方块敲掉。
        if(State(actor).Corner is not null)
        {args.HandleMode=PacketHandleMode.Cancel;args.StopPropagation=true;actor.TS.SendTileSquareCentered(x,y,4);return;}
        bool blocked=false;string? kind=null;
        var sign=ToolEntities.Sign(args.Server,x,y);
        if(_settings.ProtectSigns&&sign is { } s&&Point(_settings.Signs,args.Server,s) is not null){kind="标牌";blocked=!CanEdit(actor.TS,kind);}
        var lever=ToolEntities.Switch(args.Server,x,y,true);
        if(_settings.ProtectSwitches&&lever is { } l&&Point(_settings.Switches,args.Server,l) is not null){kind="开关";blocked|=!CanEdit(actor.TS,kind);}
        // 区域本身及向外配置格数不可破坏；管理员仍需commandtool.edit。
        if(!blocked&&_settings.ProtectRegions&&!CanEdit(actor.TS,"区域")&&RegionProtectsAt(args.Server,x,y))
        {kind="区域";blocked=true;}
        if(blocked) {args.HandleMode=PacketHandleMode.Cancel;args.StopPropagation=true;actor.TS.SendTileSquareCentered(x,y,4);}
        else if(kind is not null)args.PacketProcessed+=EntityEdited;
    }
    private void EntityEdited(in ReceivePacketEvent<TileChange> args,PacketHandleMode mode)
    {
        if(mode==PacketHandleMode.Cancel||!_running)return;
        int x=args.Packet.Position.X,y=args.Packet.Position.Y;var copy=ToolValidation.Snapshot(_settings);bool changed=false;
        var server=args.Server;
        foreach(var item in copy.Signs.Concat(copy.Switches).Where(p=>p.Enabled&&InWorld(p,server)&&Math.Abs(p.X-x)<=1&&Math.Abs(p.Y-y)<=1))
        {
            bool isSign=copy.Signs.Contains(item);var entity=isSign?ToolEntities.Sign(args.Server,item.X,item.Y):ToolEntities.Switch(args.Server,item.X,item.Y);
            if(entity is null){item.Enabled=false;changed=true;}
        }
        if(changed)Save(copy);
    }
    private static ServerContext? Resolve(string name)
    {
        var matches=UnifiedServerCoordinator.Servers.Where(s=>s.Name.Equals(name,StringComparison.OrdinalIgnoreCase)
            ||string.Equals(s.worldDataProvider?.WorldFileName,name,StringComparison.OrdinalIgnoreCase)).Distinct().ToArray();
        return matches.Length==1?matches[0]:null;
    }
}
