using System.Reflection;
using BoundaryTransfer;
using Terraria;
using TShockAPI;
using UnifierTSL;
using UnifierTSL.Events.Core;
using UnifierTSL.Events.Handlers;
using UnifierTSL.Servers;

static partial class Program
{
    static void HiddenCommandChecks(BoundaryTransferPlugin plugin, ServerContext server, ServerContext other, QuietPlayer issuer, Action<string,bool> check)
    {
        int forwarded = 0;
        void Observe(ref ReadonlyEventArgs<MessageEvent> args) => forwarded++;
        UnifierApi.EventHub.Chat.MessageEvent.Register(Observe, HandlerPriority.Higher + 1);
        bool Send(string text, ServerContext? from, byte who)
        {
            var message = new MessageEvent(new MessageSender(from,who),text,text);
            UnifierApi.EventHub.Chat.MessageEvent.Invoke(in message,out bool handled);
            return handled;
        }
        const int observerSlot = 1;
        var observer = new QuietPlayer(observerSlot) { FinishedHandshake=true, Group=new SuperAdminGroup(), DisplayLogs=true };
        var observerPlayer = new Player { whoAmI=observerSlot,name="另一个管理员",active=true };
        typeof(TSPlayer).GetField("FakePlayer",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(observer,observerPlayer);
        TShock.Players[observerSlot]=observer;
        issuer.Messages.Clear();
        issuer.Group=new Group("普通玩家");
        issuer.FinishedHandshake=true;
        try
        {
            check("无权限玩家的隐藏指令被私密消费",Send("/边界 排列 左 中 右",server,0));
            check("无权限仍被拒绝且仅本人收到错误",issuer.Messages.Count==1 && issuer.Messages[0].Contains("无权"));
            check("拒绝执行也不进入公共广播路径",forwarded==0 && observer.Messages.Count==0);
            issuer.Group=new SuperAdminGroup();
            issuer.Messages.Clear();
            check("管理员手动输入隐藏指令仍可用",Send("/边界 列表",server,0) && issuer.Messages.Any(s=>s.Contains("从左到右")));
            check("执行回执只发本人其他管理员无消息",forwarded==0 && observer.Messages.Count==0);
            issuer.Messages.Clear();
            check("英文大写别名仍私密可用",Send("/BOUNDARYTRANSFER list",server,0) && issuer.Messages.Count==2 && forwarded==0);
            issuer.Messages.Clear();
            check("静默前缀别名仍私密可用",Send(".borders list",server,0) && issuer.Messages.Count==2 && forwarded==0);
            issuer.Messages.Clear(); issuer.FinishedHandshake=false;
            check("未完成握手不能借私密入口执行",Send("/边界 列表",server,0) && issuer.Messages.Count==0 && forwarded==0);
            issuer.FinishedHandshake=true;
            check("世界路由不一致不能执行",Send("/边界 列表",other,0) && issuer.Messages.Count==0 && forwarded==0);
            issuer.Messages.Clear();
            check("过长隐藏指令拒绝执行",Send("/边界 "+new string('x',2100),server,0) && issuer.Messages.Count==1 && issuer.Messages[0].Contains("过长"));
            issuer.Messages.Clear();
            check("参数引号不完整仅本人报错",Send("/边界 排列 \"未闭合",server,0) && issuer.Messages.Count==1 && issuer.Messages[0].Contains("未闭合"));
            check("普通聊天保持原广播路径",!Send("正常聊天",server,0) && forwarded==1);
            check("相似指令名不会被边界插件误吞",!Send("/边界其他",server,0) && forwarded==2);
            check("所有官方指令目录均无边界别名",Commands.GetRegisteredCommands().All(c=>!c.HasAlias("边界") && !c.HasAlias("boundarytransfer") && !c.HasAlias("borders")));
            check("控制台可不带斜线使用隐藏命令",Send("边界 列表",null,byte.MaxValue) && forwarded==2);
        }
        finally
        {
            UnifierApi.EventHub.Chat.MessageEvent.UnRegister(Observe);
            TShock.Players[observerSlot]=null!;
            issuer.FinishedHandshake=true;
            issuer.Messages.Clear();
        }
    }
}
