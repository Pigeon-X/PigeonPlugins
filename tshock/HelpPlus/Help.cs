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

            // 用捕获型玩家走"玩家侧真实发送路径"：
            // 渲染之后按 Help() 一样逐行发送，把每一条实际会发出去的消息记下来。
            var spy = new CapturingPlayer();
            var lines = RenderCommandListLines(spy, 1, out var pages, out var total);

            TShock.Log.ConsoleDebug($"[HelpPlus] 自检: 可用命令 {total} 个，共 {pages} 页，第 1 页 {lines.Count} 行。");

            foreach (var line in lines)
            {
                if (line.Length > 0)
                {
                    spy.SendMessage(line, 255, 244, 150);
                }
            }

            var messages = spy.Messages;
            var withNewline = messages.Count(m => m.Contains('\n') || m.Contains('\r'));
            TShock.Log.ConsoleDebug(
                $"[HelpPlus] 自检: 玩家侧实际发出 {messages.Count} 条聊天消息，其中含换行的 {withNewline} 条（必须为 0）。");

            // 逐条 dump，前缀带序号；排查时能直接看出第几条开始坏
            for (var i = 0; i < messages.Count; i++)
            {
                var line = messages[i].Replace("\r", "<CR>").Replace("\n", "<LF>");
                if (line.Trim().Length == 0)
                {
                    line = "<空行>";
                }
                TShock.Log.ConsoleDebug($"  [玩家消息 {i + 1}/{messages.Count}] {line}");
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[HelpPlus] 自检异常: {ex}");
        }
    }

    /// <summary>
    /// 只用来捕获消息的玩家替身：不真的发包，把 <see cref="SendMessage"/> 收到的内容记下来。
    ///
    /// Index 取 0（非负）是有意的 —— 这样 <see cref="RenderCommandList"/> 会走"玩家"分支
    /// 而不是"控制台"分支，测到的就是玩家真正会遇到的那条路径。
    /// 两个重载都拦：TShock 的 SendMessage(string,byte,byte,byte) 内部会转调 Color 重载。
    /// </summary>
    private sealed class CapturingPlayer : TSPlayer
    {
        public List<string> Messages { get; } = new();

        public CapturingPlayer() : base(0) { }

        public override void SendMessage(string msg, byte red, byte green, byte blue)
        {
            Messages.Add(msg);
        }

        public override void SendMessage(string msg, Microsoft.Xna.Framework.Color color)
        {
            Messages.Add(msg);
        }
    }

    /// <summary>
    /// 渲染某一页命令列表。抽成独立方法是为了让自检能在没有客户端连接的情况下
    /// 复用与 <see cref="Help"/> 完全相同的渲染路径，避免"自检通过的代码"和"玩家走的代码"不是同一段。
    /// </summary>
    /// <summary>
    /// 把一个逻辑页渲染成"一行一条聊天消息"的列表。
    ///
    /// 为什么不返回一整段带 '\n' 的文本：实测玩家侧只收到 1 条含换行符的消息
    /// （自检计数为 1，且其中 1 条仍含 '\n'），客户端把整段塞进一条聊天记录里解析，
    /// 显示会完全错乱。所以这里自己分行，由调用方逐行发送。
    ///
    /// 玩家侧还要限制"一次最多发几行"：聊天消息是一条一条推的，
    /// 几百条同时推给客户端会被刷屏甚至丢消息。控制台没有这个问题，可以一次列全。
    /// </summary>
    private static List<string> RenderCommandListLines(TSPlayer? player, int page, out int pages, out int total)
    {
        var specifier = TShock.Config.Settings.CommandSpecifier;

        var isConsole = player == null || player.Index < 0;

        // 玩家侧每页最多这么多"行"（不是命令数）。命令是横向拼在一行里的，
        // 一行大约放 4~5 个命令，30 行对应 100 个以上命令。
        const int MaxPlayerLinesPerPage = 30;

        var pageSize = isConsole ? int.MaxValue : Config.Settings.PageSize;
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

        // 命令按每页命令数切片
        var commandPages = (int)Math.Ceiling(total / (double)pageSize);
        if (commandPages < 1)
        {
            commandPages = 1;
        }

        if (page < 1)
        {
            page = 1;
        }
        if (page > commandPages)
        {
            page = commandPages;
        }

        var start = (page - 1) * pageSize;
        var pagedCommands = cmdNamesOrder
            .Skip(start)
            .Take(pageSize)
            // 格式 /warp（传送点）：命令名后紧跟全角括号注释，没有注释就不加括号
            .Select(cmd =>
            {
                var shortText = GetShort(cmd.Name);
                return $"[c/4CB5DE:{specifier}][c/F1D06C:{cmd.Name}]" + shortText;
            })
            .ToList();

        var wrapWidth = Config.Settings.WithSize;
        if (wrapWidth < 20)
        {
            wrapWidth = 120;
        }

        // 先把全部命令折成"行"
        var allLines = new List<string>();
        var currentLine = new StringBuilder();
        foreach (var cmdWithSpace in pagedCommands.Select(cmd => $"{cmd} "))
        {
            if (currentLine.Length + cmdWithSpace.Length > wrapWidth)
            {
                allLines.Add(currentLine.ToString().Trim());
                currentLine.Clear();
            }
            currentLine.Append(cmdWithSpace);
        }
        if (currentLine.Length > 0)
        {
            allLines.Add(currentLine.ToString().Trim());
        }

        // 玩家侧再按"行数"切一次页，避免一次推太多条聊天消息
        var linesPerPage = isConsole ? allLines.Count : Math.Min(allLines.Count, MaxPlayerLinesPerPage);
        if (linesPerPage < 1)
        {
            linesPerPage = 1;
        }

        var linePages = (int)Math.Ceiling(allLines.Count / (double)linesPerPage);
        if (linePages < 1)
        {
            linePages = 1;
        }

        var linePage = page;
        if (linePage > linePages)
        {
            linePage = linePages;
        }

        pages = isConsole ? 1 : linePages;

        var take = isConsole ? allLines.Count : Math.Min(linesPerPage, MaxPlayerLinesPerPage);
        var lineStart = (linePage - 1) * linesPerPage;

        var result = new List<string>
        {
            GetString($"[c/FE727D:命令列表] ([c/68A7E8:{linePage}]/[c/EC6AC9:{linePages}]):"),
        };

        result.AddRange(allLines.Skip(lineStart).Take(take));

        if (linePage < linePages)
        {
            result.Add(GetString($"请输入[c/68A7E8:{specifier}help {linePage + 1}]查看更多"));
        }

        // 兜底：真的没有任何可展示内容时也要给一句话，不能静默返回。
        if (pagedCommands.Count == 0)
        {
            result.Add(GetString("[c/FE727D:没有可显示的命令。]"));
        }

        return result;
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

            // 控制台（Index < 0）不进游戏内消息链路：
            // 原实现会走 SendMessage 广播给全服，等于刷屏。改成直接写服务器控制台。
            var isConsole = args.Player == null || args.Player.Index < 0;

            // 渲染与自检走同一段代码，保证"自检通过"就等于"玩家能看到"
            var lines = RenderCommandListLines(args.Player, page, out _, out _);

            // 诊断（INFO 级，始终写日志）：玩家反馈 /help 无反应时，用来区分
            // 「命令没进来」和「进来了但输出被拦」。
            try
            {
                TShock.Log.Info($"[HelpPlus] /help 调用：玩家={args.Player?.Name ?? "(console)"}"
                    + $" 组={args.Player?.Group?.Name ?? "?"}"
                    + $" Index={args.Player?.Index ?? -99}"
                    + $" 参数={args.Parameters.Count}"
                    + $" 输出={lines.Count} 行");
            }
            catch { }

            if (isConsole)
            {
                // 控制台不吃 Terraria 的 [c/XXXXXX:...] 标记，转成 ANSI 颜色后再打，
                // 这样控制台与游戏内颜色一致（指令青、命令名金、注释浅蓝）。
                foreach (var line in lines)
                {
                    Console.WriteLine(ToAnsiColor(line));
                }
            }
            else
            {
                // 一行一条消息：实测把整段多行文本交给 SendMessage，玩家侧只收到 1 条
                // 含换行符的消息，客户端解析后显示完全错乱。
                // 走到这里说明 isConsole 为 false，args.Player 必然非空
                if (lines.Count == 0)
                {
                    // 兜底：绝不静默返回，否则玩家看到的就是"输入 /help 没反应"
                    args.Player!.SendMessage(GetString("[c/FE727D:/help 没有可显示的内容]"), 255, 244, 150);
                }
                foreach (var line in lines)
                {
                    if (line.Length == 0)
                    {
                        continue;
                    }
                    args.Player!.SendMessage(line, 255, 244, 150);
                }
            }
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

    /// <summary>
    /// 把 Terraria 的聊天颜色标记 <c>[c/RRGGBB:文本]</c> 转成 ANSI 转义序列，
    /// 让服务器控制台也显示出与游戏内一致的颜色（指令青色、命令名金色、注释浅蓝）。
    ///
    /// 之前这里是"把标记剥掉"，结果控制台全是白字，注释看不出层次。
    /// 其余形如 <c>[i:nnnn]</c> 的标签在控制台没有意义，直接去掉。
    /// </summary>
    private static string ToAnsiColor(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        const string AnsiReset = "\u001b[0m";

        // 逐行处理，每行末尾补一个 reset，避免颜色渗到下一行或 TShock 的其它输出上
        var lines = text.Replace("\r", string.Empty).Split('\n');
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            var converted = System.Text.RegularExpressions.Regex.Replace(
                line,
                @"\[c/([0-9A-Fa-f]{6}):([^\]]*)\]",
                match =>
                {
                    var hex = match.Groups[1].Value;
                    var content = match.Groups[2].Value;
                    var r = Convert.ToInt32(hex.Substring(0, 2), 16);
                    var g = Convert.ToInt32(hex.Substring(2, 2), 16);
                    var b = Convert.ToInt32(hex.Substring(4, 2), 16);
                    // 用 24 位真彩色；终端不支持时会自动退化成默认色，不会显示乱码
                    return $"\u001b[38;2;{r};{g};{b}m{content}\u001b[0m";
                });

            // 其余 [xxx:yyy] 标签（如 [i:74]）在控制台无意义，去掉
            converted = System.Text.RegularExpressions.Regex.Replace(converted, @"\[[a-zA-Z]+:[^\]]*\]", "");

            result.Append(converted).Append(AnsiReset).Append('\n');
        }

        return result.ToString();
    }

    private static string GetShort(string str)
    {
        if (!Config.Settings.DisPlayShort ||
            !Config.Settings.ShortCommands.TryGetValue(str, out var value))
        {
            return "";
        }

        // 显示为“/help（帮助）”，括号和注释使用 FixTools 的深蓝；
        // 这里只影响帮助列表的文字渲染，不碰 /help 命令注册、别名和替换逻辑。
        var text = (value ?? "").Trim();
        return text.Length > 0 ? $"[c/508DC8:（{text}）]" : "";
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
