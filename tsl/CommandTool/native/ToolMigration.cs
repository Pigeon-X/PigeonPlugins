using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CommandTool;

/// <summary>仅解析调用方提供的旧 JSON；不读取路径、不写文件、不推测当前世界。</summary>
internal static class ToolMigration
{
    private const int MaxJsonLength = 4 * 1024 * 1024;
    private const int MaxItems = 20000;
    private static readonly Regex SwitchPosition = new(@"\AX:\s*(\d+),\s*Y:\s*(\d+)\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static ToolConfig Import(ToolConfig current, string json, string sourceKind, string? assignedWorld = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (sourceKind is not ("commandtool" or "signcommand"))
            throw new ArgumentException("sourceKind 必须为 commandtool 或 signcommand。", nameof(sourceKind));
        if (json is null || json.Length > MaxJsonLength)
            throw new ArgumentException($"旧 JSON 不能为空且不得超过 {MaxJsonLength} 字符。", nameof(json));
        JObject root;
        try
        {
            using var text = new StringReader(json);
            using var reader = new JsonTextReader(text) { MaxDepth = 32, DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Decimal };
            root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new ArgumentException("旧 JSON 后不能包含其他内容。", nameof(json));
            foreach (var array in root.Descendants().OfType<JArray>())
                if (array.Count > MaxItems) throw new ArgumentException($"旧 JSON 单个数组最多 {MaxItems} 项。", nameof(json));
        }
        catch (JsonException ex) { throw new ArgumentException("旧 JSON 无效、属性重复或嵌套过深；未迁移任何内容。", nameof(json), ex); }
        var copy = ToolValidation.Snapshot(current);
        assignedWorld = assignedWorld?.Trim();
        Note(copy, $"已手动导入 {sourceKind}；旧文件只读，迁移器不读取或覆盖旧文件，不自动选择世界。");
        var identities = copy.Signs.Cast<ToolBinding>().Concat(copy.Switches).Concat(copy.Regions)
            .ToDictionary(b => b.Id, StringComparer.OrdinalIgnoreCase);
        if (sourceKind == "commandtool") ImportCommandTool(copy, root, assignedWorld, identities);
        else ImportSigns(copy, root, assignedWorld, identities);
        try { return ToolValidation.Snapshot(copy); }
        catch (ArgumentException ex)
        {
            throw new ArgumentException("迁移结果未通过 v2 验证，current 未改变；迁移记录：" + string.Join("；", copy.MigrationNotes) + "；" + ex.Message, nameof(json), ex);
        }
    }

    private static void ImportCommandTool(ToolConfig config, JObject root, string? world, Dictionary<string, ToolBinding> identities)
    {
        bool regionsEnabled = Bool(root, false, "启用", "Enable");
        var regions = Array(root, "区域表", "Rectangles");
        var switches = Object(root, "开关表", "SwitchCommandList");
        if ((long)regions.Count + (switches?.Count ?? 0) > MaxItems) throw new ArgumentException($"旧绑定最多 {MaxItems} 项。");
        foreach (var token in regions)
        {
            var old = AsObject(token, "区域");
            var region = new RegionBinding
            {
                Enabled = regionsEnabled,
                World = String(old, "", "世界", "World").Trim(),
                XMin = Int(old, 0, "X最小", "XMin"), XMax = Int(old, 0, "X最大", "XMax"),
                YMin = Int(old, 0, "Y最小", "YMin"), YMax = Int(old, 0, "Y最大", "YMax"),
                DwellMs = Milliseconds(old, "停留秒", "Time"), CooldownMs = 0
            };
            foreach (var command in Array(old, "指令", "Commands"))
            {
                var item = AsObject(command, "区域指令");
                AddCommand(config, region, String(item, "", "文本", "Text"), false,
                    String(item, "", "权限", "Permission"), String(item, "", "用户组", "Group"));
            }
            AssignWorld(config, region, world, "区域");
            region.Id = Id("region", region.World, region.XMin, region.YMin, region.XMax, region.YMax);
            var sign = Object(old, "牌子", "Sign");
            if (sign is not null)
            {
                bool enabled = Bool(sign, false, "启用", "Enable");
                string enter = String(sign, "", "进入提示", "OnEnter");
                string loop = String(sign, "", "循环提示", "Loop");
                // 即使显示关闭也检查别名冲突，不能用中文/英文双字段静默掩盖数据。
                var color = Field(sign, "颜色", "Color");
                if (enabled) { region.EnterText = enter; region.CountdownText = loop; }
                if (color is not null) Note(config, $"{region.Id}：旧提示颜色在 v2 无对应字段，未保留颜色；提示文本按旧 Enable 迁移。");
            }
            if (region.Actions.Count == 0 && region.XMin == 0 && region.XMax == 0 && region.YMin == 0 && region.YMax == 0)
            { Note(config, "已跳过旧区域 (0,0) 空指令占位；未创建空动作启用绑定。"); continue; }
            if (region.Actions.Any(a => a.GrantPermission.Length != 0))
                Note(config, "旧区域 Permission 是临时授予权限，已映射动作 GrantPermission；不会修改共享用户组。");
            if (region.Actions.Any(a => a.RequireGroup.Length != 0))
                Note(config, "旧区域 Group 已映射动作 RequireGroup；需由执行器兼容原用户组/父组判定，不改共享 group。");
            DisableEmpty(config, region);
            Add(config, region, config.Regions, identities);
        }
        if (switches is null) return;
        Note(config, "旧 Enable 仅控制区域；开关按旧语义默认启用，缺世界时仍禁用。旧编辑权限 alias 开关需由宿主兼容，不修改 v2 全局权限。");
        foreach (var property in switches.Properties())
        {
            var match = SwitchPosition.Match(property.Name);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int y))
                throw new ArgumentException($"无法识别旧开关坐标键：{property.Name}；未迁移任何内容。");
            var old = AsObject(property.Value, "开关");
            var binding = new PointBinding { X = x, Y = y, World = String(old, "", "世界", "World").Trim(), CooldownMs = Milliseconds(old, "冷却秒", "cooldown") };
            bool asAdmin = Bool(old, false, "忽略权限", "ignorePerms");
            foreach (var command in Array(old, "指令列表", "commandList"))
                AddCommand(config, binding, AsString(command, "开关指令"), asAdmin);
            AssignWorld(config, binding, world, $"开关 ({x},{y})");
            binding.Id = Id("switch", binding.World, x, y);
            DisableEmpty(config, binding);
            Add(config, binding, config.Switches, identities);
        }
    }

    private static void ImportSigns(ToolConfig config, JObject root, string? world, Dictionary<string, ToolBinding> identities)
    {
        bool protect = Bool(root, true, "启用标牌保护", "Enabled");
        bool confirm = Bool(root, true, "点击确定执行指令", "ExecuteOnConfirm");
        bool click = Bool(root, true, "点击标牌就执行", "ExecuteOnClick");
        bool strict = Bool(root, true, "校验标牌文字", "StrictText");
        bool elevate = Bool(root, true, "默认以管理员身份执行", "ElevateByDefault");
        // 首次导入可以带入旧标牌选项；已有标牌时不覆盖会影响已有执行行为的设置。
        if (config.Signs.Count != 0 && (config.ProtectSigns != protect || config.SignConfirm != confirm || config.SignClick != click || config.StrictSignText != strict))
            throw new ArgumentException("旧标牌全局选项与现有标牌配置冲突；请人工确认后统一设置，迁移不会覆盖现有选项。");
        config.ProtectSigns = protect; config.SignConfirm = confirm; config.SignClick = click; config.StrictSignText = strict;
        if(!protect)Note(config,"旧标牌启用标志为false：对应旧绑定保持禁用，不会因合并到已启用工具而恢复执行。");
        string edit = String(root, "signcommand.edit", "修改标牌权限", "EditPermission");
        string bind = String(root, "signcommand.bind", "绑定指令权限", "BindPermission");
        if(config.Signs.Count!=0&&(!string.Equals(config.LegacySignEditPermission,edit,StringComparison.Ordinal)
            ||!string.Equals(config.LegacySignBindPermission,bind,StringComparison.Ordinal)))
            throw new ArgumentException("旧标牌自定义权限与当前兼容权限冲突，未覆盖现有绑定。");
        config.LegacySignEditPermission=edit;config.LegacySignBindPermission=bind;
        Note(config, $"保留旧标牌权限 alias：编辑={edit}，绑定={bind}；未修改 v2 AdminPermission/EditPermission，旧绑定权限仅管理标牌。");
        foreach (var token in Array(root, "绑定", "Bindings"))
        {
            var old = AsObject(token, "标牌");
            var adminToken = Field(old, "以管理员身份执行", "Elevate");
            bool admin = adminToken is null || adminToken.Type == JTokenType.Null ? elevate : AsBool(adminToken, "以管理员身份执行");
            var binding = new PointBinding
            {
                X = Int(old, 0, "X"), Y = Int(old, 0, "Y"), World = String(old, "", "世界", "World").Trim(),
                Text = NullableString(old, "标牌文字", "SignText"), CooldownMs = 0, Enabled = protect
            };
            AddCommand(config, binding, String(old, "", "指令", "Command"), admin);
            AssignWorld(config, binding, world, $"标牌 ({binding.X},{binding.Y})");
            binding.Id = Id("sign", binding.World, binding.X, binding.Y);
            DisableEmpty(config, binding);
            Add(config, binding, config.Signs, identities);
        }
    }

    private static void AssignWorld(ToolConfig config, ToolBinding binding, string? assigned, string description)
    {
        if (binding.World.Length == 0) binding.World = assigned ?? "";
        if (binding.World.Length != 0) return;
        binding.Enabled = false;
        Note(config, $"{description}：旧绑定缺少世界且未显式 assignedWorld；已设 Enabled=false、World=空，禁止自动猜测世界。");
    }

    private static void AddCommand(ToolConfig config, ToolBinding binding, string command, bool admin, string permission = "", string group = "")
    {
        command = command.Trim();
        if (command.Length == 0) { Note(config, "已跳过旧空指令；不会生成可启用的空动作。"); return; }
        if (!ToolText.TryParse(command, out var root, out _) || ToolText.IsManagement(root))
            throw new ArgumentException($"迁移记录：发现无效或危险管理自绑定指令 [{command}]。禁止静默丢弃或仅禁用后保留危险动作；请先在旧文件副本中删除该动作或人工禁用/改写，再重新手动导入。current 未改变。");
        binding.Actions.Add(new ToolAction { Command = command, AsAdmin = admin, GrantPermission = permission.Trim(), RequireGroup = group.Trim() });
        if (binding.Actions.Count > 32) throw new ArgumentException("单个旧绑定超过32个非空动作，不能静默截断，请人工拆分。");
    }

    private static void DisableEmpty(ToolConfig config, ToolBinding binding)
    {
        if (binding.Actions.Count != 0) return;
        binding.Enabled = false;
        Note(config, $"{binding.Id}：没有非空动作，保留位置但已禁用。");
    }

    private static void Add<T>(ToolConfig config, T binding, List<T> destination, Dictionary<string, ToolBinding> identities) where T : ToolBinding
    {
        identities.TryGetValue(binding.Id, out var existing);
        if (existing is not null)
        {
            if (existing.GetType() != binding.GetType() || !JToken.DeepEquals(JToken.FromObject(existing), JToken.FromObject(binding)))
                throw new ArgumentException($"迁移 ID {binding.Id} 已存在但内容不同；禁止覆盖，请人工解决冲突。");
            Note(config, $"{binding.Id}：相同内容已迁移，跳过重复绑定。");
            return;
        }
        destination.Add(binding);
        identities.Add(binding.Id,binding);
    }

    private static string Id(string kind, string world, params int[] coordinates)
    {
        // 位置身份不含指令内容：同位置旧数据被更改后再导入会显式冲突，不会多执行一次。
        string identity = JsonConvert.SerializeObject(new object[] { kind, world, coordinates });
        return "legacy-" + kind + "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    private static void Note(ToolConfig config, string note)
    {
        if (!config.MigrationNotes.Contains(note, StringComparer.Ordinal)) config.MigrationNotes.Add(note);
    }

    private static JToken? Field(JObject obj, params string[] names)
    {
        JToken? result = null; string? first = null;
        foreach (string name in names)
        {
            var property = obj.Property(name, StringComparison.Ordinal);
            if (property is null) continue;
            if (first is not null && !JToken.DeepEquals(result, property.Value))
                throw new ArgumentException($"旧字段 {obj.Path}.{first}/{name} 同时存在但不完全等价，禁止选择其中一个覆盖。");
            first ??= name; result = property.Value;
        }
        return result;
    }

    private static JObject AsObject(JToken token, string name) => token as JObject ?? throw new ArgumentException($"{name}必须是 JSON 对象。");
    private static JObject? Object(JObject obj, params string[] names)
    {
        var token = Field(obj, names);
        return token is null ? null : AsObject(token, string.Join("/", names));
    }
    private static JArray Array(JObject obj, params string[] names)
    {
        var token = Field(obj, names);
        return token is null ? new JArray() : token as JArray ?? throw new ArgumentException($"{string.Join("/", names)}必须是数组。");
    }
    private static bool AsBool(JToken token, string name) => token.Type == JTokenType.Boolean ? token.Value<bool>() : throw new ArgumentException($"{name}必须是布尔值。");
    private static bool Bool(JObject obj, bool fallback, params string[] names)
    {
        var token = Field(obj, names); return token is null ? fallback : AsBool(token, string.Join("/", names));
    }
    private static string AsString(JToken token, string name) => token.Type == JTokenType.String ? token.Value<string>()! : throw new ArgumentException($"{name}必须是字符串。");
    private static string String(JObject obj, string fallback, params string[] names)
    {
        var token = Field(obj, names); return token is null ? fallback : AsString(token, string.Join("/", names));
    }
    private static string? NullableString(JObject obj, params string[] names)
    {
        var token = Field(obj, names); return token is null || token.Type == JTokenType.Null ? null : AsString(token, string.Join("/", names));
    }
    private static int Int(JObject obj, int fallback, params string[] names)
    {
        var token = Field(obj, names);
        if (token is null) return fallback;
        if (token.Type != JTokenType.Integer || !int.TryParse(token.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
            throw new ArgumentException($"{string.Join("/", names)}必须是32位整数。");
        return value;
    }
    private static int Milliseconds(JObject obj, params string[] names)
    {
        var token = Field(obj, names);
        if (token is null) return 0;
        if (token.Type is not (JTokenType.Integer or JTokenType.Float)
            || !decimal.TryParse(token.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            || seconds < 0 || seconds > 3600)
            throw new ArgumentException($"{string.Join("/", names)}必须是0～3600秒。");
        return checked((int)decimal.Ceiling(seconds * 1000m));
    }
}
