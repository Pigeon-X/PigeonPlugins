using System.Globalization;
using TShockAPI;
using UnifierTSL;
using UnifierTSL.Events.Core;
using UnifierTSL.Events.Handlers;
using UnifierTSL.Servers;

namespace CommandTool;

public sealed partial class CommandToolPlugin
{
    private void OnHidden(ref ReadonlyEventArgs<MessageEvent> args)
    {
        if(!_running||args.Handled)return;
        string text=args.Content.Text.Trim();bool allowBare=args.Content.Sender.IsServer;
        bool prefix=(!string.IsNullOrEmpty(Commands.Specifier)&&text.StartsWith(Commands.Specifier,StringComparison.Ordinal))
            ||(!string.IsNullOrEmpty(Commands.SilentSpecifier)&&text.StartsWith(Commands.SilentSpecifier,StringComparison.Ordinal));
        if(!prefix&&!allowBare)return;
        // 先精确识别根命令再解析，非法引号也不落入公开审计路径。
        string body=text;
        foreach(string spec in new[]{Commands.Specifier,Commands.SilentSpecifier}.Where(s=>!string.IsNullOrEmpty(s)).OrderByDescending(s=>s.Length))
            if(body.StartsWith(spec,StringComparison.Ordinal)){body=body[spec.Length..];break;}
        string root=body.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"";
        if(!ToolText.IsManagement(root))return;
        args.Handled=true;args.StopPropagation=true;
        var sender=args.Content.Sender;var executor=new CommandExecutor(sender.SourceServer,sender.UserId);
        ToolActor? actor=sender.IsClient&&sender.SourceServer is not null?ToolActor.Capture(sender.SourceServer,sender.UserId):null;
        if(sender.IsClient&&actor is null)return;
        string normalizedRoot=root.ToLowerInvariant();
        bool signAlias=normalizedRoot is "标牌" or "sign" or "signcommand" or "sc";
        bool switchAlias=normalizedRoot is "开关" or "kg" or "switch";
        bool admin=executor.HasPermission(_settings.AdminPermission);
        bool legacySign=signAlias&&!string.IsNullOrWhiteSpace(_settings.LegacySignBindPermission)&&executor.HasPermission(_settings.LegacySignBindPermission);
        if(!admin&&!legacySign&&!(switchAlias&&executor.HasPermission("开关")))
        {executor.SendErrorMessage("无权管理指令工具。");return;}
        if(!ToolText.TryParse(text,out root,out var p)){executor.SendErrorMessage("参数过长或引号未闭合。");return;}
        try {lock(_gate)Admin(executor,actor,root,p);}
        catch(Exception e) when(e is ArgumentException or IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
        {executor.SendErrorMessage("未保存："+e.Message);}
    }
    private bool Select(ToolPlayerState state,string kind,ToolEntity entity)
    {
        if(state.Selecting!=kind||state.SelectionUntil<Environment.TickCount64)return false;
        state.Selecting=null;state.SelectedKind=kind;state.SelectedX=entity.X;state.SelectedY=entity.Y;state.SelectedText=entity.Text;
        var source=kind=="标牌"?_settings.Signs:_settings.Switches;
        var old=Point(source,state.Actor.Source,entity);
        state.Draft=old is null?new PointBinding {Id=(kind=="标牌"?"sign-":"switch-")+Guid.NewGuid().ToString("N")[..10],World=state.Actor.Source.Name,X=entity.X,Y=entity.Y,Text=entity.Text}:
            Newtonsoft.Json.JsonConvert.DeserializeObject<PointBinding>(Newtonsoft.Json.JsonConvert.SerializeObject(old));
        // 旧拉杆绑定可能用右下角坐标；编辑完成统一为左上锚点，不影响导入原记录。
        state.Draft!.X=entity.X;state.Draft.Y=entity.Y;
        state.Actor.TS.SendSuccessMessage($"已选中{kind} ({entity.X},{entity.Y})，改动暂存，/工具 完成 才保存；/工具 取消 可放弃。");
        return true;
    }
    private void Admin(CommandExecutor e,ToolActor? actor,string root,List<string> p)
    {
        string? kind=root.ToLowerInvariant() switch {"开关" or "kg" or "switch"=>"开关","标牌" or "sign" or "signcommand" or "sc"=>"标牌",_=>null};
        if(root.ToLowerInvariant() is "区域传送" or "ctregion")p.Insert(0,"区域");
        if(kind is null&&p.Count>0&&p[0] is "标牌" or "开关"){kind=p[0];p=p.Skip(1).ToList();}
        string sub=p.FirstOrDefault()??(kind is null?"帮助":"选择");
        bool unifiedAdmin=e.HasPermission(_settings.AdminPermission);
        if(!unifiedAdmin)
        {
            if(kind is null||sub is not ("帮助" or "help" or "选择" or "重选" or "重绑" or "取消" or "列表" or "list" or "添加" or "add" or "冷却" or "权限忽略" or "提权" or "删除" or "完成" or "save"))
                throw new ArgumentException("旧绑定权限只管理对应实体，区域/迁移/世界设置需 commandtool.admin。");
            if(sub=="删除"&&p.Count>1&&!int.TryParse(p[1],out _))throw new ArgumentException("按ID删除需commandtool.admin；请先选择对应实体，再删除绑定。");
            if(actor is not null&&State(actor).SelectedKind is { } selected&&selected!=kind&&sub is not ("选择" or "重选" or "重绑" or "帮助" or "help" or "列表" or "list"))
                throw new ArgumentException("兼容权限不能跨实体编辑草稿，请重新选择。");
        }
        if(sub is "帮助" or "help") {Help(e);return;}
        if(sub is "列表" or "list")
        {
            var all=_settings.Signs.Cast<ToolBinding>().Concat(_settings.Switches).Concat(_settings.Regions).ToArray();
            int page=p.Count>1?Number(p[1],1,10000):1;
            e.SendInfoMessage($"CommandTool2：标牌{_settings.Signs.Count}、开关{_settings.Switches.Count}、区域{_settings.Regions.Count}；第{page}页");
            foreach(var b in all.Skip((page-1)*10).Take(10))e.SendInfoMessage($"{b.Id} [{(b.Enabled?"启用":"待确认/禁用")}] {b.World} 动作{b.Actions.Count}");
            return;
        }
        if(sub is "重载" or "reload") {Reload();e.SendInfoMessage("已请求重载；无效配置会保留上次有效版本，详情见服务端日志。");return;}
        if(sub is "迁移" or "迁移预览")
        {
            if(p.Count<3)throw new ArgumentException("/工具 迁移 <commandtool|signcommand> <旧json完整路径> [归属世界]");
            string path=Path.GetFullPath(p[2]);if(!path.EndsWith(".json",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("仅能导入JSON配置。");
            var info=new FileInfo(path);if(!info.Exists||info.Length>4*1024*1024)throw new ArgumentException("旧文件不存在或超过4MB。");
            string? world=p.Count>3?p[3]:null;if(world is not null&&Resolve(world) is null)throw new ArgumentException("归属世界必须唯一存在，请使用准确世界名。");
            var imported=ToolMigration.Import(_settings,File.ReadAllText(path),p[1],world);
            if(sub=="迁移预览")
            {
                e.SendInfoMessage($"预览：新增标牌{imported.Signs.Count-_settings.Signs.Count}、开关{imported.Switches.Count-_settings.Switches.Count}、区域{imported.Regions.Count-_settings.Regions.Count}；所有来源文件未修改。");
                foreach(var note in imported.MigrationNotes.Except(_settings.MigrationNotes).Take(8))e.SendInfoMessage(note);
                e.SendInfoMessage("确认后以相同参数执行 /工具 迁移，才写入统一配置并备份上一版。");return;
            }
            imported.MigrationNotes.Add("迁移来源（原文件未改动）："+path);Save(imported);
            e.SendSuccessMessage("已写入新版统一配置并备份上一版，旧文件未修改；无世界归属的项目保持禁用，请指定世界后启用。");return;
        }
        if(sub=="世界") {foreach(var s in UnifiedServerCoordinator.Servers)e.SendInfoMessage($"{s.Name} / {s.worldDataProvider?.WorldFileName} [{(s.IsRunning?"运行":"停止")}]");return;}
        if(sub is "启用" or "禁用")
        {
            if(p.Count<2)throw new ArgumentException("/工具 启用|禁用 <ID> [世界]");
            var copy=ToolValidation.Snapshot(_settings);var b=All(copy).SingleOrDefault(x=>x.Id.Equals(p[1],StringComparison.OrdinalIgnoreCase))??throw new ArgumentException("未找到ID。");
            if(p.Count>2){var s=Resolve(p[2])??throw new ArgumentException("归属世界不存在或不唯一。");b.World=s.Name;}
            b.Enabled=sub=="启用";Save(copy);e.SendSuccessMessage("状态已保存。");return;
        }
        if(sub=="删除"&&p.Count>1&&!int.TryParse(p[1],out _))
        {
            var copy=ToolValidation.Snapshot(_settings);int n=copy.Signs.RemoveAll(x=>x.Id.Equals(p[1],StringComparison.OrdinalIgnoreCase))
                +copy.Switches.RemoveAll(x=>x.Id.Equals(p[1],StringComparison.OrdinalIgnoreCase))+copy.Regions.RemoveAll(x=>x.Id.Equals(p[1],StringComparison.OrdinalIgnoreCase));
            if(n==0)throw new ArgumentException("未找到ID。");Save(copy);e.SendSuccessMessage("已删除绑定，普通实体不再保护。");return;
        }
        if(actor is null)throw new ArgumentException("选点和绑定编辑请在游戏内执行。");
        var state=State(actor);
        switch(sub)
        {
            case "选择":case "重选":case "重绑":
                if(kind is null)throw new ArgumentException("/工具 标牌 选择 或 /工具 开关 选择");
                state.Selecting=kind;state.Draft=null;state.SelectedKind=null;state.SelectionUntil=Environment.TickCount64+300000;
                e.SendInfoMessage("请点击一个"+kind+"；普通开关不会因进入选点而变成指令开关。");return;
            case "取消":state.Selecting=null;state.Draft=null;state.SelectedKind=null;state.Corner=null;state.Corner2=null;state.SelectionUntil=0;e.SendInfoMessage("未保存的修改已丢弃。");return;
            case "区域":
                if(p.Count<2)throw new ArgumentException("/工具 区域 点1|点2（单格点） | 占位1|占位2 [2x3|3x3]（玩家占位框） | 建立 <ID> [停留秒] | 指令 <ID> <指令>");
                string which=p[1];
                if(which is "点1" or "set1" or "pos1")
                {
                    state.SelectionMode="point";state.Corner=PointBox(actor);state.Corner2=null;
                    e.SendInfoMessage($"点1已设置：单格 ({state.Corner.Value.XMin},{state.Corner.Value.YMin})");return;
                }
                if(which is "点2" or "set2" or "pos2")
                {
                    if(state.SelectionMode!="point"||state.Corner is null)throw new ArgumentException("先 /工具 区域 点1 再点2。");
                    state.Corner2=PointBox(actor);
                    e.SendInfoMessage($"点2已设置：单格 ({state.Corner2.Value.XMin},{state.Corner2.Value.YMin})");return;
                }
                if(which is "占位1" or "area1" or "foot1")
                {
                    state.SelectionMode="footprint";state.Corner=SelectionBox(actor,p.Count>2?p[2]:null);state.Corner2=null;
                    var b=state.Corner.Value;
                    e.SendInfoMessage($"占位1已设置：X {b.XMin}～{b.XMax}，Y {b.YMin}～{b.YMax}（{b.XMax-b.XMin+1}×{b.YMax-b.YMin+1}格）");return;
                }
                if(which is "占位2" or "area2" or "foot2")
                {
                    if(state.SelectionMode!="footprint"||state.Corner is null)throw new ArgumentException("先 /工具 区域 占位1 再占位2。");
                    state.Corner2=SelectionBox(actor,p.Count>2?p[2]:null);
                    var b=state.Corner2.Value;
                    e.SendInfoMessage($"占位2已设置：X {b.XMin}～{b.XMax}，Y {b.YMin}～{b.YMax}（{b.XMax-b.XMin+1}×{b.YMax-b.YMin+1}格）");return;
                }
                if(which=="建立")
                {
                    if(state.Corner is not { } c||p.Count<3)throw new ArgumentException("先 /工具 区域 点1、点2（单格点）或 占位1、占位2（玩家占位框），再 /工具 区域 建立 <ID> [停留秒]");
                    var second=state.Corner2??(state.SelectionMode=="point"?PointBox(actor):SelectionBox(actor,null));
                    var id=p[2].Trim();if(id.Length==0)throw new ArgumentException("区域ID不能为空。");
                    AddRegion(new RegionBinding {Id=id,World=actor.Source.Name,
                        XMin=Math.Min(c.XMin,second.XMin),XMax=Math.Max(c.XMax,second.XMax),
                        YMin=Math.Min(c.YMin,second.YMin),YMax=Math.Max(c.YMax,second.YMax),
                        DwellMs=p.Count>3?Seconds(p[3]):0,Actions=[]});
                    state.Corner=null;state.Corner2=null;
                    e.SendSuccessMessage($"区域 {id} 已建立；向外 {_settings.RegionProtectMargin} 格受保护。用 /工具 区域 指令 <ID> <指令> 添加动作。");return;
                }
                if(which=="指令")
                {
                    if(p.Count<4)throw new ArgumentException("/工具 区域 指令 <ID> <指令>");
                    var id=p[2];string regionCommand=p.Count==4?p[3]:string.Join(" ",p.Skip(3).Select(s=>s.Any(char.IsWhiteSpace)?ToolText.Quote(s):s));
                    if(!ToolText.TryParse(regionCommand,out string regionRoot,out _)||ToolText.IsManagement(regionRoot))throw new ArgumentException("指令无效或为管理指令，禁止自绑定。");
                    var copy=ToolValidation.Snapshot(_settings);var region=copy.Regions.SingleOrDefault(r=>r.Id.Equals(id,StringComparison.OrdinalIgnoreCase))??throw new ArgumentException("未找到区域ID。");
                    if(region.Actions.Count>=32)throw new ArgumentException("单个区域最多32个指令。");
                    region.Actions.Add(new ToolAction {Command=regionCommand});Save(copy);e.SendSuccessMessage("区域指令已保存；CommandTool不处理传送。");return;
                }
                if(which=="删除动作")
                {
                    if(p.Count<4)throw new ArgumentException("/工具 区域 删除动作 <ID> <序号>");
                    var copy=ToolValidation.Snapshot(_settings);var region=copy.Regions.SingleOrDefault(r=>r.Id.Equals(p[2],StringComparison.OrdinalIgnoreCase))??throw new ArgumentException("未找到区域ID。");
                    int regionIndex=Number(p[3],1,region.Actions.Count)-1;region.Actions.RemoveAt(regionIndex);Save(copy);e.SendInfoMessage("区域指令已删除。");return;
                }
                throw new ArgumentException("区域命令支持 点1、建立、中心、中心矩形、指令、删除动作；不做传送或目标坐标。");
            case "添加":case "add":
                var draft=Draft(state);if(p.Count<2)throw new ArgumentException("/工具 添加 <指令>");
                string command=p.Count==2?p[1]:string.Join(" ",p.Skip(1).Select(s=>s.Any(char.IsWhiteSpace)?ToolText.Quote(s):s));
                if(!ToolText.TryParse(command,out string cmd,out _)||ToolText.IsManagement(cmd))throw new ArgumentException("指令无效或为管理指令，禁止自绑定。");
                draft.Actions.Add(new ToolAction {Command=command});e.SendInfoMessage("已暂存指令，输入 /工具 完成 保存。");return;
            case "权限忽略":case "提权":
                if(p.Count<2||!bool.TryParse(p[1],out bool admin))throw new ArgumentException("/工具 提权 true|false");
                foreach(var a in Draft(state).Actions.Where(a=>a.Kind=="指令"))a.AsAdmin=admin;
                e.SendInfoMessage("执行身份已暂存；仅绑定的固定指令获得本次调用的权限，不修改共享用户组。");return;
            case "冷却":
                if(p.Count<2)throw new ArgumentException("/工具 冷却 <秒>");Draft(state).CooldownMs=Seconds(p[1]);e.SendInfoMessage("冷却已暂存。");return;
            case "删除":
                if(p.Count<2) {var d=Draft(state);var copy=ToolValidation.Snapshot(_settings);copy.Signs.RemoveAll(b=>b.Id==d.Id);copy.Switches.RemoveAll(b=>b.Id==d.Id);Save(copy);e.SendSuccessMessage("该实体绑定已删除。");return;}
                var actions=Draft(state).Actions;int index=Number(p[1],1,actions.Count)-1;actions.RemoveAt(index);e.SendInfoMessage("该动作已从草稿移除，输入 /工具 完成 保存。");return;
            case "完成":case "save":
                var point=Draft(state);if(point.Actions.Count==0)throw new ArgumentException("草稿没有动作；如要解除保护，请删除绑定。");
                var current=state.SelectedKind=="标牌"?ToolEntities.Sign(actor.Source,point.X,point.Y):ToolEntities.Switch(actor.Source,point.X,point.Y);
                if(current is null)throw new ArgumentException("选中的实体已不存在。");
                var config=ToolValidation.Snapshot(_settings);var list=state.SelectedKind=="标牌"?config.Signs:config.Switches;
                list.RemoveAll(b=>b.Id==point.Id);list.Add(point);Save(config);e.SendSuccessMessage("绑定已保存。普通未绑定实体不受保护，自动触发全程无聊天提示。");return;
            default:Help(e);return;
        }
    }
    internal static (int XMin,int YMin,int XMax,int YMax) PointBox(ToolActor actor)
    {
        int x=(int)(actor.Player.Center.X/16),y=(int)(actor.Player.Bottom.Y/16);
        x=Math.Clamp(x,0,actor.Source.Main.maxTilesX-1);y=Math.Clamp(y,0,actor.Source.Main.maxTilesY-1);
        return (XMin:x,YMin:y,XMax:x,YMax:y);
    }
    /// <summary>默认取玩家实际占用的图格框（常见2×3）；可传2x3/3x3等尺寸，以脚底为中心向上取高。</summary>
    internal static (int XMin,int YMin,int XMax,int YMax) SelectionBox(ToolActor actor,string? size)
    {
        int left,top,right,bottom;
        if(string.IsNullOrWhiteSpace(size))
        {
            var p=actor.Player;
            left=(int)MathF.Floor(p.position.X/16f);
            right=(int)MathF.Floor((p.position.X+p.width-0.01f)/16f);
            top=(int)MathF.Floor(p.position.Y/16f);
            bottom=(int)MathF.Floor((p.position.Y+p.height-0.01f)/16f);
        }
        else
        {
            var parts=size.Split(['x','X'],StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length!=2)throw new ArgumentException("框选尺寸写成 2x3 或 3x3。");
            int width=Number(parts[0],1,64),height=Number(parts[1],1,64);
            int centerX=(int)(actor.Player.Center.X/16),feet=(int)(actor.Player.Bottom.Y/16);
            left=centerX-(width-1)/2;right=left+width-1;bottom=feet;top=feet-(height-1);
        }
        int maxX=actor.Source.Main.maxTilesX-1,maxY=actor.Source.Main.maxTilesY-1;
        left=Math.Clamp(left,0,maxX);right=Math.Clamp(right,0,maxX);
        top=Math.Clamp(top,0,maxY);bottom=Math.Clamp(bottom,0,maxY);
        if(right<left)(left,right)=(right,left);
        if(bottom<top)(top,bottom)=(bottom,top);
        return (XMin:left,YMin:top,XMax:right,YMax:bottom);
    }
    private void AddRegion(RegionBinding region)
    {
        var copy=ToolValidation.Snapshot(_settings);
        if(copy.Regions.Any(r=>r.Id.Equals(region.Id,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("区域ID已存在。");
        copy.Regions.Add(region);Save(copy);
    }
    private static IEnumerable<ToolBinding> All(ToolConfig c)=>c.Signs.Cast<ToolBinding>().Concat(c.Switches).Concat(c.Regions);
    private static int Number(string s,int min,int max)=>int.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out int n)&&n>=min&&n<=max?n:throw new ArgumentException($"数值应为{min}～{max}。");
    private static int Seconds(string s)=>double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out double d)&&double.IsFinite(d)&&d>=0&&d<=3600?(int)(d*1000):throw new ArgumentException("秒数应为0～3600。");
    private static PointBinding Draft(ToolPlayerState state)=>state.SelectionUntil>=Environment.TickCount64&&state.Draft is { } d?d:throw new ArgumentException("请先 /工具 标牌 选择 或 /工具 开关 选择，再点击实体。");
    private static void Help(CommandExecutor e)
    {
        e.SendInfoMessage("管理入口隐藏，仅本人回复：/工具 标牌 选择 | 开关 选择 → 点击实体 → 添加 <指令> → 完成");
        e.SendInfoMessage("/工具 冷却 <秒> | 提权 true|false | 删除 [ID或草稿动作序号] | 取消 | 列表 [页] | 世界 | 重载");
        e.SendInfoMessage("/工具 区域 点1|点2（单格点）→ 建立 <ID> [停留秒]");
        e.SendInfoMessage("/工具 区域 占位1|占位2 [2x3|3x3]（玩家占位框）→ 建立 <ID> [停留秒]");
        e.SendInfoMessage("/工具 区域 指令 <ID> <指令> → 建立后区域及外扩格不可破坏");
        e.SendInfoMessage("/工具 迁移 <commandtool|signcommand> <旧json完整路径> [归属世界]；/工具 启用 <ID> [归属世界]");
    }
}
