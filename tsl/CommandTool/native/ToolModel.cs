using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace CommandTool;

public sealed class ToolConfig
{
    [JsonProperty("配置版本"), JsonPropertyName("配置版本")] public int Version { get; set; } = 2;
    [JsonProperty("启用"), JsonPropertyName("启用")] public bool Enabled { get; set; } = true;
    [JsonProperty("区域检测间隔_毫秒"), JsonPropertyName("区域检测间隔_毫秒")] public int PollMs { get; set; } = 100;
    [JsonProperty("管理权限"), JsonPropertyName("管理权限")] public string AdminPermission { get; set; } = "commandtool.admin";
    [JsonProperty("编辑保护实体权限"), JsonPropertyName("编辑保护实体权限")] public string EditPermission { get; set; } = "commandtool.edit";
    [JsonProperty("旧标牌编辑兼容权限"), JsonPropertyName("旧标牌编辑兼容权限")] public string LegacySignEditPermission { get; set; } = "signcommand.edit";
    [JsonProperty("旧标牌绑定兼容权限"), JsonPropertyName("旧标牌绑定兼容权限")] public string LegacySignBindPermission { get; set; } = "signcommand.bind";
    [JsonProperty("保护指令标牌"), JsonPropertyName("保护指令标牌")] public bool ProtectSigns { get; set; } = true;
    [JsonProperty("保护指令开关"), JsonPropertyName("保护指令开关")] public bool ProtectSwitches { get; set; } = true;
    [JsonProperty("保护区域"), JsonPropertyName("保护区域")] public bool ProtectRegions { get; set; } = true;
    [JsonProperty("区域保护外扩_格"), JsonPropertyName("区域保护外扩_格")] public int RegionProtectMargin { get; set; } = 5;
    [JsonProperty("点击标牌执行"), JsonPropertyName("点击标牌执行")] public bool SignClick { get; set; } = true;
    [JsonProperty("确认标牌执行"), JsonPropertyName("确认标牌执行")] public bool SignConfirm { get; set; }
    [JsonProperty("校验绑定标牌文字"), JsonPropertyName("校验绑定标牌文字")] public bool StrictSignText { get; set; } = true;
    [JsonProperty("标牌"), JsonPropertyName("标牌")] public List<PointBinding> Signs { get; set; } = [];
    [JsonProperty("开关"), JsonPropertyName("开关")] public List<PointBinding> Switches { get; set; } = [];
    [JsonProperty("区域"), JsonPropertyName("区域")] public List<RegionBinding> Regions { get; set; } = [];
    [JsonProperty("迁移记录"), JsonPropertyName("迁移记录")] public List<string> MigrationNotes { get; set; } = [];
}

public class ToolBinding
{
    [JsonProperty("ID"), JsonPropertyName("ID")] public string Id { get; set; } = "";
    [JsonProperty("启用"), JsonPropertyName("启用")] public bool Enabled { get; set; } = true;
    [JsonProperty("世界"), JsonPropertyName("世界")] public string World { get; set; } = "";
    [JsonProperty("冷却_毫秒"), JsonPropertyName("冷却_毫秒")] public int CooldownMs { get; set; } = 1000;
    [JsonProperty("使用权限"), JsonPropertyName("使用权限")] public string UsePermission { get; set; } = "";
    [JsonProperty("动作"), JsonPropertyName("动作")] public List<ToolAction> Actions { get; set; } = [];
}

public sealed class PointBinding : ToolBinding
{
    [JsonProperty("X"), JsonPropertyName("X")] public int X { get; set; }
    [JsonProperty("Y"), JsonPropertyName("Y")] public int Y { get; set; }
    [JsonProperty("绑定时文字"), JsonPropertyName("绑定时文字")] public string? Text { get; set; }
}

public sealed class RegionBinding : ToolBinding
{
    [JsonProperty("X最小"), JsonPropertyName("X最小")] public int XMin { get; set; }
    [JsonProperty("X最大"), JsonPropertyName("X最大")] public int XMax { get; set; }
    [JsonProperty("Y最小"), JsonPropertyName("Y最小")] public int YMin { get; set; }
    [JsonProperty("Y最大"), JsonPropertyName("Y最大")] public int YMax { get; set; }
    [JsonProperty("停留_毫秒"), JsonPropertyName("停留_毫秒")] public int DwellMs { get; set; }
    [JsonProperty("优先级"), JsonPropertyName("优先级")] public int Priority { get; set; }
    [JsonProperty("进入提示"), JsonPropertyName("进入提示")] public string EnterText { get; set; } = "";
    [JsonProperty("倒计时提示"), JsonPropertyName("倒计时提示")] public string CountdownText { get; set; } = "";
    public bool Contains(float tileX, float tileY) => float.IsFinite(tileX) && float.IsFinite(tileY)
        && tileX >= XMin && tileX <= XMax && tileY >= YMin && tileY <= YMax;

    /// <summary>区域本身及向外 margin 格的范围；用于不可破坏保护，不改变触发矩形。</summary>
    public bool ContainsExpanded(int x, int y, int margin)
    {
        long m = Math.Clamp(margin, 0, 20);
        return x >= XMin - m && x <= XMax + m && y >= YMin - m && y <= YMax + m;
    }

    public bool IntersectsExpanded(int left, int top, int right, int bottom, int margin)
    {
        long m = Math.Clamp(margin, 0, 20);
        return left <= XMax + m && right >= XMin - m && top <= YMax + m && bottom >= YMin - m;
    }
}

public sealed class ToolAction
{
    [JsonProperty("类型"), JsonPropertyName("类型")] public string Kind { get; set; } = "指令";
    [JsonProperty("指令"), JsonPropertyName("指令")] public string Command { get; set; } = "";
    [JsonProperty("以管理员身份执行"), JsonPropertyName("以管理员身份执行")] public bool AsAdmin { get; set; }
    [JsonProperty("临时权限"), JsonPropertyName("临时权限")] public string GrantPermission { get; set; } = "";
    [JsonProperty("限定用户组"), JsonPropertyName("限定用户组")] public string RequireGroup { get; set; } = "";
}

internal static class ToolValidation
{
    internal static ToolConfig Snapshot(ToolConfig config)
    {
        var copy = JsonConvert.DeserializeObject<ToolConfig>(JsonConvert.SerializeObject(config))
            ?? throw new ArgumentException("配置不能为空。");
        if (copy.Version != 2) throw new ArgumentException("不支持的配置版本，请先迁移旧配置。");
        Range(copy.PollMs, 50, 1000, "区域检测间隔");
        Range(copy.RegionProtectMargin, 0, 20, "区域保护外扩");
        if (string.IsNullOrWhiteSpace(copy.AdminPermission) || string.IsNullOrWhiteSpace(copy.EditPermission))
            throw new ArgumentException("管理/保护编辑权限不能为空。");
        copy.Signs ??= []; copy.Switches ??= []; copy.Regions ??= []; copy.MigrationNotes ??= [];
        bool removedTeleport=false;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var points = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in copy.Signs.Cast<ToolBinding>().Concat(copy.Switches).Concat(copy.Regions))
        {
            if (binding is null) throw new ArgumentException("绑定不能为null。");
            binding.Id = binding.Id?.Trim() ?? ""; binding.World = binding.World?.Trim() ?? "";
            if (binding.Id.Length == 0 || binding.Id.Length > 100 || !ids.Add(binding.Id))
                throw new ArgumentException("绑定ID不能为空、过长或重复。");
            if (binding.Enabled && string.IsNullOrWhiteSpace(binding.World))
                throw new ArgumentException($"{binding.Id} 启用前必须指定世界。");
            Range(binding.CooldownMs, 0, 3600000, "绑定冷却");
            binding.Actions ??= [];
            int before=binding.Actions.Count;
            // 2.1.0起CommandTool不再处理传送；旧配置中的传送动作直接移除并记录，避免整份配置无法加载。
            binding.Actions.RemoveAll(a=>a is null||a.Kind=="传送");
            removedTeleport|=binding.Actions.Count!=before;
            if (binding.Actions.Count > 32) throw new ArgumentException("单个绑定最多32个动作。");
            foreach (var action in binding.Actions)
            {
                if (action.Kind!="指令") throw new ArgumentException("CommandTool只支持指令动作；传送请交给BoundaryTransfer。");
                if (!ToolText.TryParse(action.Command, out var root, out _) || ToolText.IsManagement(root))
                    throw new ArgumentException($"{binding.Id} 指令为空、格式无效或为管理指令，禁止绑定。");
                if ((action.GrantPermission ?? "").Length > 200) throw new ArgumentException("临时权限过长。");
            }
            if (binding is PointBinding p && (p.X < 0 || p.Y < 0)) throw new ArgumentException("实体坐标不能为负数。");
            if (binding is RegionBinding r)
            {
                if (r.XMin < 0 || r.YMin < 0 || r.XMax < r.XMin || r.YMax < r.YMin)
                    throw new ArgumentException($"{r.Id} 区域坐标范围无效。");
                Range(r.DwellMs, 0, 3600000, "停留时间");
            }
        }
        foreach (var pair in copy.Signs.Select(p => (p,"标牌")).Concat(copy.Switches.Select(p => (p,"开关"))))
            if (!points.Add($"{pair.Item2}:{pair.p.World}:{pair.p.X}:{pair.p.Y}")) throw new ArgumentException("同世界同类实体不能重复绑定。");
        if (ids.Count > 20000) throw new ArgumentException("最多20000个绑定。");
        const string teleportNote="CommandTool只执行指令，不处理传送/落点；旧配置中的传送动作已移除，边界传送请使用BoundaryTransfer。";
        if(removedTeleport && !copy.MigrationNotes.Contains(teleportNote))copy.MigrationNotes.Add(teleportNote);
        return copy;
    }
    private static void Range(int v, int min, int max, string name)
    { if (v < min || v > max) throw new ArgumentException($"{name}必须在{min}～{max}之间。"); }
}

internal static class ToolText
{
    // 刻意不含ct：ct常被custom等插件使用，隐藏入口不能抢占别人的指令。
    internal static readonly string[] Aliases = ["工具","commandtool","开关","kg","switch","标牌","sign","signcommand","sc","区域传送","ctregion"];
    internal static bool IsManagement(string root) => Aliases.Contains(root, StringComparer.OrdinalIgnoreCase);
    internal static bool TryParse(string? input, out string root, out List<string> parameters)
    {
        root = ""; parameters = [];
        if (string.IsNullOrWhiteSpace(input) || input.Length > 2048 || input.Any(c => c is '\r' or '\n' or '\0')) return false;
        string s = input.Trim();
        foreach (string prefix in new[] { TShockAPI.Commands.Specifier, TShockAPI.Commands.SilentSpecifier, "/", "." }.Where(p=>!string.IsNullOrEmpty(p)).Distinct().OrderByDescending(p=>p.Length))
            if (s.StartsWith(prefix,StringComparison.Ordinal)) { s=s[prefix.Length..]; break; }
        var list = new List<string>(); var token = new System.Text.StringBuilder(); bool quoted=false, started=false;
        for (int i=0;i<s.Length;i++)
        {
            char c=s[i];
            if (c=='\\' && i+1<s.Length && (s[i+1]=='"' || s[i+1]=='\\')) { token.Append(s[++i]); started=true; }
            else if(c=='"') { quoted=!quoted; started=true; }
            else if(char.IsWhiteSpace(c) && !quoted) { if(started) {list.Add(token.ToString());token.Clear();started=false;} }
            else { token.Append(c);started=true; }
        }
        if(quoted)return false;
        if(started)list.Add(token.ToString());
        if(list.Count==0 || list[0].Length==0)return false;
        root=list[0];parameters=list.Skip(1).ToList();return true;
    }
    internal static string Quote(string value) => "\"" + value.Replace("\\","\\\\").Replace("\"","\\\"") + "\"";
}
