using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace CommandTool;

/// <summary>纯内存测试，无测试框架/文件/实服依赖。由父测试入口调用 RunAll()。</summary>
internal static class ToolMigrationTests
{
    internal static void RunAll()
    {
        RegionEnglishAndPlaceholder();
        SwitchChineseAndMissingWorld();
        SignNullableAdminAndOptions();
        DeepCopyAndIdempotence();
        ConflictsAndInvalidInput();
        JsonResourceLimits();
    }

    private static void RegionEnglishAndPlaceholder()
    {
        var config = ToolMigration.Import(new ToolConfig(), """
            {"Enable":true,"Rectangles":[
              {"XMin":0,"XMax":0,"YMin":0,"YMax":0,"Commands":[{"Text":" "}]},
              {"XMin":10,"XMax":20,"YMin":30,"YMax":40,"Time":1.25,
               "Commands":[{"Text":" /say $name ","Permission":"say.use","Group":"vip"}],
               "Sign":{"Enable":true,"OnEnter":"进入","Loop":"剩余","Color":{"R":255}}}
            ]}
            """, "commandtool", "world-one");
        Equal(1, config.Regions.Count, "跳过空占位区域");
        var region = config.Regions.Single();
        Check(region.Enabled && region.World == "world-one", "显式分配世界");
        Equal(1250, region.DwellMs, "秒转毫秒");
        Equal("进入", region.EnterText, "迁移进入提示");
        Equal("剩余", region.CountdownText, "迁移循环提示");
        var action = region.Actions.Single();
        Equal("/say $name", action.Command, "只 trim，保留前缀和 $name");
        Equal("say.use", action.GrantPermission, "权限是临时授予，不是要求使用权限");
        Equal("", region.UsePermission, "不得误设绑定使用权限");
        Equal("vip", action.RequireGroup, "迁移组限制");
        Check(config.MigrationNotes.Any(n => n.Contains("颜色")), "颜色不支持必须记录");
    }

    private static void SwitchChineseAndMissingWorld()
    {
        const string json = """
            {"启用":false,"区域表":[{"X最小":1,"X最大":2,"Y最小":3,"Y最大":4,"指令":[{"文本":"say region"}]}],
             "开关表":{"X: 5660, Y: 204":{"指令列表":[" /say $name "," "],"冷却秒":0.125,"忽略权限":true}}}
            """;
        var assigned = ToolMigration.Import(new ToolConfig(), json, "commandtool", "world");
        Check(!assigned.Regions.Single().Enabled && assigned.Switches.Single().Enabled, "旧 Enable 不影响开关");
        var point = assigned.Switches.Single();
        Equal(5660, point.X, "读取 SwitchPos 键 X"); Equal(204, point.Y, "读取 SwitchPos 键 Y");
        Equal(125, point.CooldownMs, "开关小数冷却");
        Check(point.Actions.Single().AsAdmin, "忽略权限映射 AsAdmin");
        var missing = ToolMigration.Import(new ToolConfig(), json, "commandtool");
        Check(missing.Switches.All(p => !p.Enabled && p.World == ""), "缺世界必须禁用");
        Check(missing.MigrationNotes.Any(n => n.Contains("缺少世界")), "缺世界有记录");
        var empty = ToolMigration.Import(new ToolConfig(), """{"SwitchCommandList":{"X: 1, Y: 2":{"commandList":[" "]}}}""", "commandtool", "w");
        Check(!empty.Switches.Single().Enabled && empty.Switches.Single().Actions.Count == 0, "空动作开关不能启用");
    }

    private static void SignNullableAdminAndOptions()
    {
        const string json = """
            {"启用标牌保护":false,"修改标牌权限":"legacy.edit","绑定指令权限":"legacy.bind",
             "点击确定执行指令":true,"点击标牌就执行":false,"默认以管理员身份执行":true,"校验标牌文字":false,
             "绑定":[{"世界":"original","X":1,"Y":2,"指令":"say $name","标牌文字":"hello","以管理员身份执行":null},
                     {"X":3,"Y":4,"指令":"say noadmin","以管理员身份执行":false}]}
            """;
        var result = ToolMigration.Import(new ToolConfig(), json, "signcommand", "assigned");
        Equal("original", result.Signs[0].World, "不替换原世界");
        Equal("assigned", result.Signs[1].World, "仅补缺失世界");
        Check(result.Signs[0].Actions.Single().AsAdmin, "null 继承全局 true");
        Check(!result.Signs[1].Actions.Single().AsAdmin, "显式 false 不继承");
        Check(!result.ProtectSigns && result.SignConfirm && !result.SignClick && !result.StrictSignText, "旧全局标牌选项");
        Check(result.Signs.All(s=>!s.Enabled),"旧标牌总停用不能因合并而恢复执行");
        Equal("legacy.edit",result.LegacySignEditPermission,"保留自定义旧标牌编辑权限");
        Equal("legacy.bind",result.LegacySignBindPermission,"保留自定义旧标牌绑定权限");
        Equal("commandtool.admin", result.AdminPermission, "不改 v2 管理权限");
        Equal("commandtool.edit", result.EditPermission, "不改 v2 编辑权限");
        Check(result.MigrationNotes.Any(n => n.Contains("legacy.edit") && n.Contains("legacy.bind")), "权限 alias 记录");
        var defaultAdmin = ToolMigration.Import(new ToolConfig(), """{"Bindings":[{"World":"w","X":7,"Y":8,"Command":"say ok"}]}""", "signcommand");
        Check(defaultAdmin.Signs.Single().Actions.Single().AsAdmin, "缺旧全局值默认 true，缺绑定值继承");
        var missing = ToolMigration.Import(new ToolConfig(), """{"绑定":[{"X":1,"Y":2,"指令":"say ok"}]}""", "signcommand");
        Check(!missing.Signs.Single().Enabled && missing.Signs.Single().World == "", "无世界标牌禁用");
    }

    private static void DeepCopyAndIdempotence()
    {
        const string json = """{"SwitchCommandList":{"X: 10, Y: 20":{"commandList":["say hello"]}}}""";
        var current = new ToolConfig();
        string before = JsonConvert.SerializeObject(current);
        var once = ToolMigration.Import(current, json, "commandtool", "w");
        Equal(before, JsonConvert.SerializeObject(current), "不得改 current");
        var twice = ToolMigration.Import(once, json, "commandtool", "w");
        Equal(1, twice.Switches.Count, "重复导入不增加执行绑定");
        Equal(once.Switches.Single().Id, twice.Switches.Single().Id, "ID 稳定");
        Check(twice.Switches.Single().Id.StartsWith("legacy-switch-", StringComparison.Ordinal), "ID 前缀");
        twice.Switches[0].Actions[0].Command = "say changed";
        twice.Switches[0].CooldownMs = 9999;
        Equal("say hello", once.Switches[0].Actions[0].Command, "动作深拷贝");
        Equal(0, once.Switches[0].CooldownMs, "选项深拷贝");
        string saved = JsonConvert.SerializeObject(once);
        Throws(() => ToolMigration.Import(once, json.Replace("say hello", "say changed"), "commandtool", "w"), "ID 同位置内容冲突");
        Equal(saved, JsonConvert.SerializeObject(once), "冲突仍不能改变 current");
        var orderA = ToolMigration.Import(new ToolConfig(), """{"开关表":{"X: 1, Y: 2":{"指令列表":["say a"]},"X: 3, Y: 4":{"指令列表":["say b"]}}}""", "commandtool", "w");
        var orderB = ToolMigration.Import(orderA, """{"开关表":{"X: 3, Y: 4":{"指令列表":["say b"]},"X: 1, Y: 2":{"指令列表":["say a"]}}}""", "commandtool", "w");
        Equal(2, orderB.Switches.Count, "字典顺序不影响去重");
    }

    private static void ConflictsAndInvalidInput()
    {
        Throws(() => ToolMigration.Import(new ToolConfig(), """{"Enable":true,"启用":false}""", "commandtool"), "中英冲突必须抛错");
        ToolMigration.Import(new ToolConfig(), """{"Enable":true,"启用":true}""", "commandtool");
        Throws(() => ToolMigration.Import(new ToolConfig(), """{"Enable":true,"Enable":true}""", "commandtool"), "重复 JSON 属性禁止");
        Throws(() => ToolMigration.Import(new ToolConfig(), "{} {}", "commandtool"), "拒绝尾随 JSON");
        Throws(() => ToolMigration.Import(new ToolConfig(), "{}", "invalid"), "sourceKind 白名单");
        Throws(() => ToolMigration.Import(new ToolConfig(), """{"开关表":{"garbage":{}}}""", "commandtool"), "拒绝不识别坐标键");
        Throws(() => ToolMigration.Import(new ToolConfig(), """{"开关表":{"X: 1, Y: 2":{"指令列表":["/工具 bind"]}}}""", "commandtool", "w"), "拒绝管理自绑定而非丢整文件");
        Throws(() => ToolMigration.Import(new ToolConfig(), """{"开关表":{"X: 1, Y: 2":{"指令列表":["say ok"],"冷却秒":3601}}}""", "commandtool", "w"), "冷却范围");
        Throws(() => ToolMigration.Import(new ToolConfig(), """{"区域表":[{"X最小":4,"X最大":1,"指令":[{"文本":"say ok"}]}]}""", "commandtool", "w"), "必须通过 Snapshot");
        var old = ToolMigration.Import(new ToolConfig(), """{"绑定":[{"世界":"w","X":1,"Y":2,"指令":"say ok"}]}""", "signcommand");
        Throws(() => ToolMigration.Import(old, """{"点击标牌就执行":false}""", "signcommand"), "不能覆盖影响已有标牌的旧全局选项");
    }

    private static void JsonResourceLimits()
    {
        Throws(() => ToolMigration.Import(new ToolConfig(), new string(' ', 4 * 1024 * 1024 + 1), "commandtool"), "JSON 长度限制");
        string deep = "{\"extra\":" + new string('[', 40) + "0" + new string(']', 40) + "}";
        Throws(() => ToolMigration.Import(new ToolConfig(), deep, "commandtool"), "恶意深度限制");
        string many = "{\"extra\":[" + string.Join(",", Enumerable.Repeat("0", 20001)) + "]}";
        Throws(() => ToolMigration.Import(new ToolConfig(), many, "commandtool"), "即使未知字段也限制数组项数");
        string commands = "{\"开关表\":{\"X: 1, Y: 2\":{\"指令列表\":[" + string.Join(",", Enumerable.Repeat("\"say ok\"", 33)) + "]}}}";
        Throws(() => ToolMigration.Import(new ToolConfig(), commands, "commandtool", "w"), "动作超过上限不截断");
    }

    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception("迁移测试失败：" + name); }
    private static void Equal<T>(T expected, T actual, string name)
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"迁移测试失败：{name}，expected={expected}，actual={actual}"); }
    private static void Throws(Action action, string name)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new Exception("迁移测试失败，预期 ArgumentException：" + name);
    }
}
