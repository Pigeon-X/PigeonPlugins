using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace BoundaryTransfer;

public sealed class SeamEdges
{
    [JsonProperty("西"), JsonPropertyName("西")] public int? West { get; set; }
    [JsonProperty("东"), JsonPropertyName("东")] public int? East { get; set; }
}

public sealed class SeamConfig
{
    [JsonProperty("启用"), JsonPropertyName("启用")] public bool Enabled { get; set; }
    [JsonProperty("切片间距_格"), JsonPropertyName("切片间距_格")] public int SliceDeltaTiles { get; set; }
    [JsonProperty("边缘触发余量_格"), JsonPropertyName("边缘触发余量_格")] public int TriggerMarginTiles { get; set; } = 24;
    [JsonProperty("允许微调"), JsonPropertyName("允许微调")] public bool AllowNudge { get; set; } = true;
    [JsonProperty("失败后允许独立世界兜底"), JsonPropertyName("失败后允许独立世界兜底")] public bool AllowFallback { get; set; }
    [JsonProperty("地表范围_格"), JsonPropertyName("地表范围_格")] public int SurfaceBandTiles { get; set; } = 80;
    [JsonProperty("出生点机制"), JsonPropertyName("出生点机制")] public string SpawnMechanism { get; set; } = "个人";
    [JsonProperty("陆地边缘"), JsonPropertyName("陆地边缘")] public Dictionary<string, SeamEdges> LandEdges { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BoundaryTransferConfig
{
    [JsonProperty("启用"), JsonPropertyName("启用")]
    public bool Enabled { get; set; } = true;

    [JsonProperty("从左到右的世界名"), JsonPropertyName("从左到右的世界名")]
    public List<string> WorldOrder { get; set; } = [];

    [JsonProperty("首尾循环"), JsonPropertyName("首尾循环")]
    public bool Loop { get; set; }

    // 1.0.2只允许身体碰边；旧JSON的“触发边界距离_格”作为未知键忽略，不能恢复提前触发。
    [JsonProperty("碰边容差_像素"), JsonPropertyName("碰边容差_像素")]
    public int ContactTolerancePixels { get; set; } = 2;

    [JsonProperty("边界同高度"), JsonPropertyName("边界同高度")]
    public bool BoundarySameHeight { get; set; } = true;

    [JsonProperty("海边保护"), JsonPropertyName("海边保护")]
    public bool CoastGuard { get; set; } = true;

    [JsonProperty("海岸水平搜索_格"), JsonPropertyName("海岸水平搜索_格")]
    public int CoastSearchTiles { get; set; } = 400;

    [JsonProperty("海岸上下搜索_格"), JsonPropertyName("海岸上下搜索_格")]
    public int CoastVerticalTiles { get; set; } = 40;

    [JsonProperty("接缝"), JsonPropertyName("接缝")]
    public SeamConfig Seam { get; set; } = new();

    // 以下五项是 1.0.x 安全落点搜索的旧字段：1.1.0 起直接按接触高度落点，不再读取。
    // 保留属性仅为兼容旧测试/旧代码编译；JsonIgnore 保证新配置不再生成这些无效项。
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public int LandingInsetTiles { get; set; } = 96;

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public int InwardSearchTiles { get; set; } = 320;

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public int VerticalSearchTiles { get; set; } = 80;

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public int HazardPaddingTiles { get; set; } = 1;

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public int MaxSearchCandidates { get; set; } = 60000;

    [JsonProperty("位置检测间隔_毫秒"), JsonPropertyName("位置检测间隔_毫秒")]
    public int PollIntervalMs { get; set; } = 50;

    [JsonProperty("切换冷却_毫秒"), JsonPropertyName("切换冷却_毫秒")]
    public int CooldownMs { get; set; } = 3000;

    [JsonProperty("切换超时_毫秒"), JsonPropertyName("切换超时_毫秒")]
    public int TransferTimeoutMs { get; set; } = 20000;

    [JsonProperty("落点确认保护_毫秒"), JsonPropertyName("落点确认保护_毫秒")]
    public int LandingGuardMs { get; set; } = 2500;

    // 自动切换和失败固定静默；旧“显示切换提示”键忽略，不能重新打开玩家聊天提示。
}

internal sealed record SeamEdgeSettings(int? West, int? East);
internal sealed record SeamSettings(bool Enabled, int SliceDelta, int TriggerMargin,
    bool AllowNudge, bool AllowFallback, int SurfaceBand, string SpawnMechanism,
    IReadOnlyDictionary<string, SeamEdgeSettings> Edges)
{
    internal static readonly SeamSettings Disabled = new(false, 0, 24, true, false, 80, "关闭",
        new Dictionary<string, SeamEdgeSettings>(StringComparer.OrdinalIgnoreCase));

    public bool Equals(SeamSettings? other)
    {
        if (other is null || Enabled != other.Enabled || SliceDelta != other.SliceDelta
            || TriggerMargin != other.TriggerMargin || AllowNudge != other.AllowNudge
            || AllowFallback != other.AllowFallback || SurfaceBand != other.SurfaceBand
            || SpawnMechanism != other.SpawnMechanism || Edges.Count != other.Edges.Count) return false;
        foreach (var pair in Edges)
            if (!other.Edges.TryGetValue(pair.Key, out var edge) || edge != pair.Value) return false;
        return true;
    }
    public override int GetHashCode() => HashCode.Combine(Enabled, SliceDelta, TriggerMargin,
        AllowNudge, AllowFallback, SurfaceBand, SpawnMechanism, Edges.Count);
}

internal sealed record BoundarySettings(
    bool Enabled, string[] Worlds, bool Loop, int ContactTolerance, int LandingInset,
    int SearchInward, int SearchVertical, int HazardPadding, int MaxCandidates,
    int PollMs, int CooldownMs, int TimeoutMs, int GuardMs,
    bool BoundarySameHeight, bool CoastGuard, int CoastSearchX, int CoastSearchY, SeamSettings Seam)
{
    public static BoundarySettings Default { get; } = FromConfig(new());

    public static BoundarySettings FromConfig(BoundaryTransferConfig config)
    {
        var worlds = (config.WorldOrder ?? []).Select(n => n?.Trim() ?? "").ToArray();
        if (worlds.Length == 1 || worlds.Length > 64)
            throw new ArgumentException("世界排列应为空或包含 2～64 个世界。");
        if (worlds.Any(string.IsNullOrWhiteSpace) || worlds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != worlds.Length)
            throw new ArgumentException("世界名不能为空或重复。");
        Range(config.ContactTolerancePixels, 0, 8, "碰边容差_像素");
        Range(config.LandingInsetTiles, 64, 600, "进入对侧内缩_格");
        Range(config.InwardSearchTiles, 0, 1000, "向地图内部搜索_格");
        Range(config.VerticalSearchTiles, 0, 300, "上下安全搜索_格");
        Range(config.HazardPaddingTiles, 0, 3, "危险物缓冲_格");
        Range(config.MaxSearchCandidates, 100, 200000, "单次搜索候选上限");
        Range(config.PollIntervalMs, 50, 1000, "位置检测间隔_毫秒");
        Range(config.CooldownMs, 1000, 60000, "切换冷却_毫秒");
        Range(config.TransferTimeoutMs, 5000, 60000, "切换超时_毫秒");
        Range(config.LandingGuardMs, 500, 5000, "落点确认保护_毫秒");
        Range(config.CoastSearchTiles, 0, 1000, "海岸水平搜索_格");
        Range(config.CoastVerticalTiles, 0, 200, "海岸上下搜索_格");
        var seamConfig = config.Seam ?? new SeamConfig();
        Range(seamConfig.SliceDeltaTiles, 0, 10000, "切片间距_格");
        Range(seamConfig.TriggerMarginTiles, 0, 200, "边缘触发余量_格");
        Range(seamConfig.SurfaceBandTiles, 0, 200, "地表范围_格");
        string spawnMechanism = seamConfig.SpawnMechanism?.Trim() ?? "关闭";
        if (spawnMechanism is not ("个人" or "世界" or "关闭"))
            throw new ArgumentException("接缝出生点机制只能是 个人/世界/关闭。");
        var seamEdges = new Dictionary<string, SeamEdgeSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in seamConfig.LandEdges ?? [])
        {
            string world = pair.Key?.Trim() ?? "";
            if (world.Length == 0) throw new ArgumentException("接缝陆地边缘的世界名不能为空。");
            int? west = pair.Value?.West, east = pair.Value?.East;
            if (west is < 0 or > 100000 || east is < 0 or > 100000)
                throw new ArgumentException($"接缝陆地边缘超出范围：{world}");
            seamEdges[world] = new SeamEdgeSettings(west, east);
        }
        var seam = new SeamSettings(seamConfig.Enabled, seamConfig.SliceDeltaTiles,
            seamConfig.TriggerMarginTiles, seamConfig.AllowNudge, seamConfig.AllowFallback,
            seamConfig.SurfaceBandTiles, spawnMechanism, seamEdges);
        return new(config.Enabled, worlds, config.Loop, config.ContactTolerancePixels, config.LandingInsetTiles,
            config.InwardSearchTiles, config.VerticalSearchTiles, config.HazardPaddingTiles,
            config.MaxSearchCandidates, config.PollIntervalMs, config.CooldownMs,
            config.TransferTimeoutMs, config.LandingGuardMs,
            config.BoundarySameHeight, config.CoastGuard, config.CoastSearchTiles, config.CoastVerticalTiles, seam);
    }

    public BoundaryTransferConfig ToConfig() => new()
    {
        Enabled = Enabled, WorldOrder = [.. Worlds], Loop = Loop,
        ContactTolerancePixels = ContactTolerance, LandingInsetTiles = LandingInset,
        InwardSearchTiles = SearchInward, VerticalSearchTiles = SearchVertical,
        HazardPaddingTiles = HazardPadding, MaxSearchCandidates = MaxCandidates,
        PollIntervalMs = PollMs, CooldownMs = CooldownMs, TransferTimeoutMs = TimeoutMs,
        LandingGuardMs = GuardMs, BoundarySameHeight = BoundarySameHeight, CoastGuard = CoastGuard,
        CoastSearchTiles = CoastSearchX, CoastVerticalTiles = CoastSearchY,
        Seam = new SeamConfig
        {
            Enabled = Seam.Enabled, SliceDeltaTiles = Seam.SliceDelta,
            TriggerMarginTiles = Seam.TriggerMargin,
            AllowNudge = Seam.AllowNudge, AllowFallback = Seam.AllowFallback,
            SurfaceBandTiles = Seam.SurfaceBand, SpawnMechanism = Seam.SpawnMechanism,
            LandEdges = Seam.Edges.ToDictionary(p => p.Key,
                p => new SeamEdges { West = p.Value.West, East = p.Value.East },
                StringComparer.OrdinalIgnoreCase)
        }
    };

    private static void Range(int value, int min, int max, string name)
    {
        if (value < min || value > max)
            throw new ArgumentException($"{name} 必须在 {min}～{max} 之间。");
    }
}
