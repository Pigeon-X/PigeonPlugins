using CommandTool;

internal static class CoreModelChecks
{
    internal static void Run(Action<string,bool> check)
    {
        void Reject(string name,Action action){try{action();check(name,false);}catch(ArgumentException){check(name,true);}}
        var config=new ToolConfig();
        check("默认快照版本2",ToolValidation.Snapshot(config).Version==2);
        Reject("未知版本拒绝",()=>ToolValidation.Snapshot(new(){Version=3}));
        Reject("轮询下界拒绝",()=>ToolValidation.Snapshot(new(){PollMs=49}));
        Reject("负冷却拒绝",()=>ToolValidation.Snapshot(new(){Switches=[new(){Id="s",World="w",CooldownMs=-1}]}));
        Reject("启用绑定必须明确世界",()=>ToolValidation.Snapshot(new(){Signs=[new(){Id="s"}]}));
        check("无世界暂存绑定可保持禁用",ToolValidation.Snapshot(new(){Signs=[new(){Id="s",Enabled=false}]}).Signs.Count==1);
        Reject("跨类型重复ID拒绝",()=>ToolValidation.Snapshot(new(){Signs=[new(){Id="same",World="w"}],Switches=[new(){Id="SAME",World="w"}]}));
        Reject("同世界同实体重复绑定拒绝",()=>ToolValidation.Snapshot(new(){Switches=[new(){Id="a",World="w",X=1,Y=2},new(){Id="b",World="W",X=1,Y=2}]}));
        Reject("区域范围倒置拒绝",()=>ToolValidation.Snapshot(new(){Regions=[new(){Id="r",World="w",XMin=2,XMax=1}]}));
        Reject("区域负停留拒绝",()=>ToolValidation.Snapshot(new(){Regions=[new(){Id="r",World="w",DwellMs=-1}]}));
        check("区域保护默认外扩5格",config.ProtectRegions&&config.RegionProtectMargin==5);
        var protectedRegion=new RegionBinding{XMin=10,XMax=20,YMin=30,YMax=40};
        check("区域外扩5格包含边界",protectedRegion.ContainsExpanded(5,30,5)&&protectedRegion.ContainsExpanded(25,45,5));
        check("区域外扩5格不含第6格",!protectedRegion.ContainsExpanded(4,30,5)&&!protectedRegion.ContainsExpanded(26,45,5));
        check("批量图格矩形与保护区域相交",protectedRegion.IntersectsExpanded(24,44,30,50,5)
            &&!protectedRegion.IntersectsExpanded(26,46,30,50,5));

        Reject("管理入口不可递归绑定",()=>ToolValidation.Snapshot(new(){Signs=[new(){Id="a",World="w",Actions=[new(){Command="/工具 重载"}]}]}));
        Reject("跨行指令拒绝",()=>ToolValidation.Snapshot(new(){Signs=[new(){Id="a",World="w",Actions=[new(){Command="home\nwarp w"}]}]}));
        Reject("未知动作类型拒绝",()=>ToolValidation.Snapshot(new(){Signs=[new(){Id="a",World="w",Actions=[new(){Kind="危险"}]}]}));
        var sanitized=ToolValidation.Snapshot(new(){Signs=[new(){Id="a",World="w",Actions=[new(){Command="home"},new(){Kind="传送"}]}]});
        check("旧传送动作被移除",sanitized.Signs[0].Actions.Count==1&&sanitized.Signs[0].Actions[0].Command=="home");
        check("移除传送有迁移记录",sanitized.MigrationNotes.Any(n=>n.Contains("BoundaryTransfer",StringComparison.Ordinal)));
        config.Signs.Add(new(){Id="a",World="w",Actions=[new(){Command="home",AsAdmin=true,GrantPermission="p"}]});
        var clone=ToolValidation.Snapshot(config);clone.Signs[0].Actions[0].Command="warp w";
        check("快照动作深拷贝不改变当前配置",config.Signs[0].Actions[0].Command=="home");
        check("提权与临时权限显式保留",clone.Signs[0].Actions[0].AsAdmin&&clone.Signs[0].Actions[0].GrantPermission=="p");
        check("引号参数不被拆成多个指令",ToolText.TryParse("/warp \"大厅 二楼\"",out var root,out var p)&&root=="warp"&&p.SequenceEqual(new[]{"大厅 二楼"}));
        check("静默点前缀兼容",ToolText.TryParse(".home",out root,out p)&&root=="home"&&p.Count==0);
        check("引号转义往返",ToolText.TryParse("say "+ToolText.Quote("A\"B\\C"),out root,out p)&&p.Single()=="A\"B\\C");
        check("未闭合引号拒绝",!ToolText.TryParse("warp \"bad",out _,out _));
        check("空和NUL拒绝",!ToolText.TryParse(" ",out _,out _)&&!ToolText.TryParse("home\0",out _,out _));
        check("所有管理入口只精确匹配根",ToolText.IsManagement("SIGN")&&!ToolText.IsManagement("signmore"));
        check("ct不占用其他插件指令",!ToolText.IsManagement("ct")&&!ToolText.IsManagement("CT"));
        check("多格实体包含四格而不扩大范围",new ToolEntity(10,20,2,2).Contains(11,21)&&!new ToolEntity(10,20,2,2).Contains(12,21));

        RegionBinding R(string id="r",int dwell=1000,int priority=0)=>new(){Id=id,World="w",XMin=10,XMax=20,YMin=10,YMax=20,DwellMs=dwell,Priority=priority,CooldownMs=2000};
        var r=R();var state=new ToolRegionState();
        check("区域边界包含端点",r.Contains(10,20)&&r.Contains(20,10)&&!r.Contains(20.01f,10));
        check("非有限坐标不在区域",!r.Contains(float.NaN,10)&&!r.Contains(10,float.PositiveInfinity));
        check("进入不提前执行",state.Advance([r],15,15,0) is null&&state.Advance([r],15,15,999) is null);
        check("停留到期执行一次",state.Advance([r],15,15,1000)?.Id=="r");
        check("持续站在原区不重复",state.Advance([r],15,15,99999) is null);
        state.Advance([r],0,0,100000);
        check("离开后重新进入先重计时",state.Advance([r],15,15,100001) is null&&state.Advance([r],15,15,101001)?.Id=="r");
        state=new();state.Advance([r],15,15,0);state.Advance([r],0,0,800);state.Advance([r],15,15,900);
        check("离区取消旧倒计时",state.Advance([r],15,15,1000) is null&&state.Advance([r],15,15,1900)?.Id=="r");
        state=new();check("零停留即时执行",state.Advance([R(dwell:0)],15,15,0)?.Id=="r");
        check("个人冷却和锁存有效",state.Advance([R(dwell:0)],15,15,10) is null);
        state=new();var a=R("a",0,1);var b=R("b",0,2);
        check("重叠区域只执行最高优先级",state.Advance([a,b],15,15,0)?.Id=="b"&&state.Advance([a,b],15,15,5000) is null);
        state=new();a.Priority=b.Priority=1;
        check("同优先级按ID稳定选择",state.Advance([b,a],15,15,0)?.Id=="a");
        state=new(){SuppressUntil=3000};
        check("跨图到达目标区域被抑制",state.Advance([R(dwell:0)],15,15,0) is null&&state.Advance([R(dwell:0)],15,15,3001) is null);
        state.Advance([R(dwell:0)],0,0,3002);
        check("抑制结束后离区再进才触发",state.Advance([R(dwell:0)],15,15,3003)?.Id=="r");
        state.Reset();check("死亡或重载重置等待与锁存",state.Dwelling is null&&state.Latched.Count==0&&state.NextAllowed.Count==0);
    }
}
