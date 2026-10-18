using UnifierTSL.Events.Core;
using UnifierTSL.Events.Handlers;

namespace BoundaryTransfer.Tests;

internal static class HiddenCommandTests
{
    public static void Add(List<AlgorithmCase> cases)
    {
        cases.Add("hidden.match.three-aliases.normal-prefix", "hidden-command-parser", () =>
        {
            foreach (string alias in new[] { "边界", "boundarytransfer", "borders" })
                Match("/" + alias, false, "/", ".", alias, string.Empty);
        });
        cases.Add("hidden.match.three-aliases.silent-prefix", "hidden-command-parser", () =>
        {
            foreach (string alias in new[] { "边界", "boundarytransfer", "borders" })
                Match("." + alias + " 列表", false, "/", ".", alias + " 列表", " 列表");
        });
        cases.Add("hidden.match.english-case-insensitive", "hidden-command-parser", () =>
        {
            Match("/BOUNDARYTRANSFER worlds", false, "/", ".", "BOUNDARYTRANSFER worlds", " worlds");
            Match(".BoRdErS reload", false, "/", ".", "BoRdErS reload", " reload");
        });
        cases.Add("hidden.match.trim-and-root-whitespace", "hidden-command-parser", () =>
        {
            Match(" \t/边界\t排列 左界 中界 右界 \r\n", false, "/", ".", "边界\t排列 左界 中界 右界", "\t排列 左界 中界 右界");
            Match("/borders\nlist", false, "/", ".", "borders\nlist", "\nlist");
        });
        cases.Add("hidden.match.only-exact-root", "hidden-command-parser", () =>
        {
            foreach (string input in new[]
            {
                "/边界线", "/边界/列表", "/boundarytransfer2", "/boundarytransfer.list",
                "/bordersx list", "/borders:list", "/borders/list", "/borders\"list\"", "/borders\\ list"
            })
                NoMatch(input, false);
        });
        cases.Add("hidden.match.client-chat-unrelated-not-intercepted", "hidden-command-parser", () =>
        {
            foreach (string input in new[]
            {
                "", " \t\r\n", "边界", "边界 列表", "borders list", "boundarytransfer",
                "讨论 /边界 列表", "hello borders", "/help", "/help 边界", ".help borders", "/b orders", "/"
            })
                NoMatch(input, false);
        });
        cases.Add("hidden.match.space-after-prefix-and-double-prefix-rejected", "hidden-command-parser", () =>
        {
            foreach (string input in new[] { "/ 边界", "/\tborders", ". borders", "//borders", "..边界", "/.边界", "./borders" })
                NoMatch(input, false);
        });
        cases.Add("hidden.match.console-bare-aliases", "hidden-command-parser", () =>
        {
            Match("边界 世界", true, "/", ".", "边界 世界", " 世界");
            Match(" BOUNDARYTRANSFER list ", true, "/", ".", "BOUNDARYTRANSFER list", " list");
            Match("borders", true, "/", ".", "borders", string.Empty);
        });
        cases.Add("hidden.match.console-explicit-prefixes", "hidden-command-parser", () =>
        {
            Match("/边界 重载", true, "/", ".", "边界 重载", " 重载");
            Match(".borders list", true, "/", ".", "borders list", " list");
        });
        cases.Add("hidden.match.console-unrelated-not-intercepted", "hidden-command-parser", () =>
        {
            foreach (string input in new[] { "help borders", "边界线", "bordersx", "worlds", "say /边界", "/help 边界", "", " " })
                NoMatch(input, true);
        });
        cases.Add("hidden.match.custom-multichar-prefixes", "hidden-command-parser", () =>
        {
            Match("!!边界 列表", false, "!!", "~~", "边界 列表", " 列表");
            Match("~~BoRdErS list", false, "!!", "~~", "BoRdErS list", " list");
            foreach (string input in new[] { "/边界", ".borders", "!边界", "~borders", "borders" })
                NoMatch(input, false, "!!", "~~");
        });
        cases.Add("hidden.match.overlapping-prefixes-use-longest", "hidden-command-parser", () =>
        {
            Match("!borders list", false, "!", "!!", "borders list", " list");
            Match("!!边界 世界", false, "!", "!!", "边界 世界", " 世界");
            Match("!!borders", false, "!!", "!", "borders", string.Empty);
            NoMatch("!!!borders", false, "!", "!!");
        });
        cases.Add("hidden.match.prefixes-ordinal-not-case-folded", "hidden-command-parser", () =>
        {
            Match("CMD:BoRdErS", false, "CMD:", "SIL:", "BoRdErS", string.Empty);
            NoMatch("cmd:borders", false, "CMD:", "SIL:");
        });
        cases.Add("hidden.match.empty-prefixes-do-not-enable-player-bare-input", "hidden-command-parser", () =>
        {
            NoMatch("borders list", false, string.Empty, string.Empty);
            Match("borders list", true, string.Empty, string.Empty, "borders list", " list");
        });
        cases.Add("hidden.arguments.empty-and-whitespace", "hidden-command-parser", () =>
        {
            Parse(string.Empty, []);
            Parse(" \t\r\n", []);
        });
        cases.Add("hidden.arguments.subcommands-and-whitespace", "hidden-command-parser", () =>
        {
            Parse(" 列表", ["列表"]);
            Parse("\torder  Left\r\nMiddle\tRight ", ["order", "Left", "Middle", "Right"]);
            Parse("排列\u3000左界\u00a0右界", ["排列", "左界", "右界"]);
        });
        cases.Add("hidden.arguments.quoted-world-names-with-spaces", "hidden-command-parser", () =>
        {
            Parse(" 排列 \"Left World\" \"中间 世界\" \"Right World\"", ["排列", "Left World", "中间 世界", "Right World"]);
            Parse("order \"Left\tWorld\" \"Right\nWorld\"", ["order", "Left\tWorld", "Right\nWorld"]);
        });
        cases.Add("hidden.arguments.escaped-quotes-spaces-and-backslashes", "hidden-command-parser", () =>
        {
            Parse("""排列 Left\ World "Mid \"Quote\"" C:\\World \q""", ["排列", "Left World", "Mid \"Quote\"", @"C:\World", @"\q"]);
            Parse("order A\\", ["order", "A\\"]);
        });
        cases.Add("hidden.arguments.empty-quotes-preserved", "hidden-command-parser", () =>
        {
            Parse("排列 \"\" \"Right World\"", ["排列", string.Empty, "Right World"]);
            Parse("\"\" \"\"", [string.Empty, string.Empty]);
        });
        cases.Add("hidden.arguments.quote-boundaries-match-tshock", "hidden-command-parser", () =>
        {
            Parse("abc\"def ghi\"jkl", ["abc", "def ghi", "jkl"]);
        });
        cases.Add("hidden.arguments.unclosed-quotes-reject-and-clear", "hidden-command-parser", () =>
        {
            foreach (string input in new[] { "\"", "排列 \"Left World Right", "order A \"B", "order \"A\\\"", "order \"A\" \"B" })
            {
                Check.False(BoundaryCommandInput.TryParseArguments(input, out var actual), "未闭合引号必须拒绝");
                Check.Equal(0, actual.Count, "拒绝后不得保留可误用的部分参数");
            }
        });
        cases.Add("hidden.arguments.match-then-parse-keeps-world-names", "hidden-command-parser", () =>
        {
            Check.True(BoundaryCommandInput.TryMatch(" /BOUNDARYTRANSFER order \"Left World\" \"中间 世界\" Right\\ World ",
                false, "/", ".", out var body, out var arguments));
            Check.Equal("BOUNDARYTRANSFER order \"Left World\" \"中间 世界\" Right\\ World", body);
            Parse(arguments, ["order", "Left World", "中间 世界", "Right World"]);
        });
        cases.Add("hidden.priority.baseline-plugin-zero-before-core-zero", "hidden-command-priority", () =>
        {
            var provider = new ReadonlyEventProvider<MessageEvent>();
            var order = new List<string>();
            void Core(ref ReadonlyEventArgs<MessageEvent> args) => order.Add("core");
            void TShock(ref ReadonlyEventArgs<MessageEvent> args) => order.Add("tshock");
            void Plugin(ref ReadonlyEventArgs<MessageEvent> args)
            {
                order.Add("plugin");
                args.Handled = true;
                args.StopPropagation = true;
            }
            provider.Register(Core, HandlerPriority.Highest);
            provider.Register(TShock, HandlerPriority.Higher + 1);
            provider.Register(Plugin, HandlerPriority.Highest);
            provider.Invoke(new MessageEvent(new MessageSender(null, byte.MaxValue), "边界", "边界"), out bool handled);
            Check.True(handled);
            Check.Sequence(new[] { "plugin" }, order);
        });
        cases.Add("hidden.priority.negative-one-wraps-to-byte-and-runs-last", "hidden-command-priority", () =>
        {
            Check.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(HandlerPriority)));
            HandlerPriority wrapped = unchecked(HandlerPriority.Highest - 1);
            Check.Equal((byte)255, (byte)wrapped);
            var provider = new ReadonlyEventProvider<MessageEvent>();
            var order = new List<string>();
            void Core(ref ReadonlyEventArgs<MessageEvent> args) => order.Add("core");
            void TShock(ref ReadonlyEventArgs<MessageEvent> args) => order.Add("tshock");
            void Wrapped(ref ReadonlyEventArgs<MessageEvent> args) => order.Add("wrapped");
            provider.Register(Core, HandlerPriority.Highest);
            provider.Register(TShock, HandlerPriority.Higher + 1);
            provider.Register(Wrapped, wrapped);
            provider.Invoke(new MessageEvent(new MessageSender(null, byte.MaxValue), "边界", "边界"), out bool handled);
            Check.False(handled);
            Check.Sequence(new[] { "core", "tshock", "wrapped" }, order);
        });
    }

    private static void Match(string input, bool allowBare, string specifier, string silentSpecifier, string expectedBody, string expectedArguments)
    {
        Check.True(BoundaryCommandInput.TryMatch(input, allowBare, specifier, silentSpecifier, out var body, out var arguments), $"应识别隐藏指令：{input}");
        Check.Equal(expectedBody, body);
        Check.Equal(expectedArguments, arguments);
    }

    private static void NoMatch(string input, bool allowBare, string specifier = "/", string silentSpecifier = ".")
    {
        Check.False(BoundaryCommandInput.TryMatch(input, allowBare, specifier, silentSpecifier, out var body, out var arguments), $"不应截获：{input}");
        Check.Equal(string.Empty, body);
        Check.Equal(string.Empty, arguments);
    }

    private static void Parse(string input, string[] expected)
    {
        Check.True(BoundaryCommandInput.TryParseArguments(input, out var actual), $"应接受参数：{input}");
        Check.Sequence(expected, actual);
    }
}
