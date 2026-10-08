using Microsoft.Xna.Framework;
using On.OTAPI;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Chat;
using Terraria.Chat.Commands;
using Terraria.GameContent.NetModules;
using Terraria.Net;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using Utils = TShockAPI.Utils;

namespace UserCheck;

[ApiVersion(2, 1)]
public class HelpPlus : TerrariaPlugin
{
    private readonly Command _command = new(Help, "help", "帮助", "?");

    public HelpPlus(Main game)
        : base(game)
    {
    }

    public override string Author => "Cai, 羽学";

    public override string Description => GetString("更好的Help");

    public override string Name => System.Reflection.Assembly.GetExecutingAssembly().GetName().Name!;
    public override Version Version => new (2026, 03, 08, 0);

    public override void Initialize()
    {
        GeneralHooks.ReloadEvent += GeneralHooks_ReloadEvent;
        Commands.ChatCommands.RemoveAll(x => x.Name == "help");
        Commands.ChatCommands.Add(this._command);
        Config.Read();

        // 延迟自检：Initialize 阶段其他插件还没注册完命令，
        // 只有等全部插件就绪后再看，才能反映玩家真正会遇到的命令表。
        _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ => SelfCheck());
    }

    /// <summary>
    /// 自检：确认 <c>/help</c> 确实还注册着，并把第一页列表真实渲染一遍。
    ///
    /// 存在的原因：玩家只输入 <c>/help</c>（不带参数）时曾经什么都看不到，
    /// 而控制台与游戏内都拿不到可诊断的信息。自检把"命令是否注册""列表渲染是否非空"
    /// 两件事落到日志，出问题时能直接看出卡在哪一步。
    ///
    /// 输出走 Debug 级：平时不写日志、不刷屏；需要排查时把 tshock\config.json 的
    /// 「是否输出调试日志」改成 true，或直接看控制台，就能看到这几行。
    /// </summary>
    private static void SelfCheck()
    {
        try
        {
            var registered = Commands.ChatCommands.FindAll(c => c.HasAlias("help"));
            TShock.Log.ConsoleDebug($"[HelpPlus] 自检: 命令表中的 help 条目 = {registered.Count}");

            if (registered.Count == 0)
            {
                // 这一条保持 Error 级：命令真的丢了属于故障，必须让人看见
                TShock.Log.ConsoleError("[HelpPlus] 自检: /help 已不在命令表中——有插件在 HelpPlus 之后删除了它，玩家输入 /help 会提示无效命令。");
                return;
            }

            var rendered = RenderCommandList(TSPlayer.Server, 1, out var pages, out var total);
            var lineCount = rendered.Count(c => c == '\n') + 1;
            TShock.Log.ConsoleDebug($"[HelpPlus] 自检: 可用命令 {total} 个，共 {pages} 页，第 1 页 {lineCount} 行、{rendered.Length} 字符。");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[HelpPlus] 自检异常: {ex}");
        }
    }

    /// <summary>
    /// 渲染某一页命令列表。抽成独立方法是为了让自检能在没有客户端连接的情况下
    /// 复用与 <see cref="Help"/> 完全相同的渲染路径，避免"自检通过的代码"和"玩家走的代码"不是同一段。
    /// </summary>
    private static string RenderCommandList(TSPlayer player, int page, out int pages, out int total)
    {
        var specifier = TShock.Config.Settings.CommandSpecifier;

        var pageSize = Config.Settings.PageSize;
        if (pageSize < 1)
        {
            pageSize = 30;
        }

        var cmdNamesOrder = Commands.ChatCommands
            .Where(cmd => cmd.CanRun(player) && (cmd.Name != "setup" || TShock.SetupToken != 0))
            .ToList();

        if (Config.Settings.OrderByLetter)
        {
            cmdNamesOrder = cmdNamesOrder.OrderBy(cmd => cmd.Name).ToList();
        }

        total = cmdNamesOrder.Count;
        pages = (int)Math.Ceiling(total / (double)pageSize);
        if (pages < 1)
        {
            pages = 1;
        }

        // 页码兜底：原实现只处理 page > pages，page <= 0 会产生负数下标，
        // 结果是一条空消息（玩家什么都看不到），这里统一拉回第一页。
        if (page < 1)
        {
            page = 1;
        }
        if (page > pages)
        {
            page = pages;
        }

        var start = (page - 1) * pageSize;
        var pagedCommands = cmdNamesOrder
            .Skip(start)
            .Take(pageSize)
            .Select(cmd => $"[c/60D6D0:{specifier}][c/F1D06C:{cmd.Name}]{GetShort(cmd.Name)}")
            .ToList();

        var stringBuilder = new StringBuilder();
        var currentLine = new StringBuilder();

        // 用 '\n' 而不是 AppendLine()：AppendLine 在 Windows 上写入 "\r\n"，
        // 而 TSPlayer.SendMessage 只按 '\n' 切分，每行会残留一个 '\r'，
        // 发出去后玩家端可能整条消息都不渲染。
        stringBuilder.Append(GetString($"[c/FE727D:命令列表] ([c/68A7E8:{page}]/[c/EC6AC9:{pages}]):")).Append('\n');

        var wrapWidth = Config.Settings.WithSize;
        if (wrapWidth < 20)
        {
            wrapWidth = 120;
        }

        foreach (var cmdWithSpace in pagedCommands.Select(cmd => $"{cmd} "))
        {
            if (currentLine.Length + cmdWithSpace.Length > wrapWidth)
            {
                stringBuilder.Append(currentLine.ToString().Trim()).Append('\n');
                currentLine.Clear();
            }
            currentLine.Append(cmdWithSpace);
        }

        if (currentLine.Length > 0)
        {
            stringBuilder.Append(currentLine.ToString().Trim()).Append('\n');
        }

        if (page < pages)
        {
            stringBuilder.Append(GetString($"请输入[c/68A7E8:{specifier}help {page + 1}]查看更多")).Append('\n');
        }

        // 兜底：真的没有任何可展示内容时也要给一句话，不能静默返回。
        if (pagedCommands.Count == 0)
        {
            stringBuilder.Append(GetString("[c/FE727D:没有可显示的命令。]")).Append('\n');
        }

        return stringBuilder.ToString();
    }

    private static void GeneralHooks_ReloadEvent(ReloadEventArgs e)
    {
        Config.Read();
        e.Player.SendSuccessMessage(GetString("[HelpPlus]插件配置已重载！"));
    }

    private static void Help(CommandArgs args)
    {
        var specifier = TShock.Config.Settings.CommandSpecifier;
        if (args.Parameters.Count > 1)
        {
            args.Player.SendErrorMessage(GetString("无效用法。正确用法: {0}help <命令/页码>", specifier));
            return;
        }

        if (args.Parameters.Count == 0 || int.TryParse(args.Parameters[0], out var page))
        {
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 0, args.Player, out page))
            {
                return;
            }

            // 控制台/服务器身份调用时 Index 为 -1，原实现会把结果广播给全服；
            // 这里显式挡掉，避免服务器自检或 REST 触发时刷屏。
            if (args.Player == null || args.Player.Index < 0)
            {
                TShock.Log.ConsoleDebug(GetString("[HelpPlus] 控制台不展示命令列表，请在游戏内输入 {0}help。", specifier));
                return;
            }

            // 渲染与自检走同一段代码，保证"自检通过"就等于"玩家能看到"
            var text = RenderCommandList(args.Player, page, out _, out _);
            args.Player.SendMessage(text, 255, 244, 150);
        }
        else
        {
            var commandName = args.Parameters[0].ToLower();
            if (commandName.StartsWith(specifier))
            {
                commandName = commandName[1..];
            }

            var command = Commands.ChatCommands.Find(c => c.Names.Contains(commandName));
            if (command == null)
            {
                args.Player.SendErrorMessage(GetString("无效命令。"));
                return;
            }

            if (!command.CanRun(args.Player))
            {
                args.Player.SendErrorMessage(GetString("你没有权限查询此命令。"));
                return;
            }

            args.Player.SendSuccessMessage(GetString("{0}{1}的帮助:", specifier, command.Name));
            if (command.HelpDesc == null)
            {
                args.Player.SendWarningMessage(command.HelpText);
            }
            else
            {
                foreach (var line in command.HelpDesc)
                {
                    args.Player.SendInfoMessage(line);
                }
            }

            if (command.Names.Count > 1)
            {
                // ReSharper disable once StringLiteralTypo
                args.Player.SendInfoMessage(GetString($"别名: [c/00ffff:{string.Join(',', command.Names)}]"));
            }

            args.Player.SendInfoMessage(
                GetString($"权限: {(command.Permissions.Count == 0 || command.Permissions.Count(i => i == "") == command.Permissions.Count ? GetString("[c/c2ff39:无权限限制]") : "[c/bf0705:" + string.Join(',', command.Permissions) + "]")}"));
            args.Player.SendInfoMessage(
                $"来源插件: [c/8500ff:{command.CommandDelegate.Method.DeclaringType!.Assembly.FullName!.Split(',').First()}]");
            if (!command.AllowServer)
            {
                args.Player.SendInfoMessage(GetString("*此命令只能游戏内执行"));
            }

            if (!command.DoLog)
            {
                args.Player.SendInfoMessage(GetString("*此命令不记录命令参数"));
            }

            args.Player.SendInfoMessage(GetString("*本插件只能查询主命令权限，详细权限请使用/whynot查看!"));
        }
    }

    private static string GetShort(string str)
    {
        return Config.Settings.DisPlayShort && Config.Settings.ShortCommands.TryGetValue(str, out var value)
            ? $"[c/FF5260:@]{value.Color(Utils.BoldHighlight)}"
            : "";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GeneralHooks.ReloadEvent -= GeneralHooks_ReloadEvent;
        }

        base.Dispose(disposing);
    }
}