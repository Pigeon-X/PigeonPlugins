using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace BoundaryTransfer;

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

    [JsonProperty("海边保护"), JsonPropertyName("海边保护")]
    public bool CoastGuard { get; set; } = true;

    [JsonProperty("海岸水平搜索_格"), JsonPropertyName("海岸水平搜索_格")]
    public int CoastSearchTiles { get; set; } = 400;

    [JsonProperty("海岸上下搜索_格"), JsonPropertyName("海岸上下搜索_格")]
    public int CoastVerticalTiles { get; set; } = 40;

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

internal sealed record BoundarySettings(
    bool Enabled, string[] Worlds, bool Loop, int ContactTolerance, int LandingInset,
    int SearchInward, int SearchVertical, int HazardPadding, int MaxCandidates,
    int PollMs, int CooldownMs, int TimeoutMs, int GuardMs,
    bool CoastGuard, int CoastSearchX, int CoastSearchY)
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
        return new(config.Enabled, worlds, config.Loop, config.ContactTolerancePixels, config.LandingInsetTiles,
            config.InwardSearchTiles, config.VerticalSearchTiles, config.HazardPaddingTiles,
            config.MaxSearchCandidates, config.PollIntervalMs, config.CooldownMs,
            config.TransferTimeoutMs, config.LandingGuardMs,
            config.CoastGuard, config.CoastSearchTiles, config.CoastVerticalTiles);
    }

    public BoundaryTransferConfig ToConfig() => new()
    {
        Enabled = Enabled, WorldOrder = [.. Worlds], Loop = Loop,
        ContactTolerancePixels = ContactTolerance, LandingInsetTiles = LandingInset,
        InwardSearchTiles = SearchInward, VerticalSearchTiles = SearchVertical,
        HazardPaddingTiles = HazardPadding, MaxSearchCandidates = MaxCandidates,
        PollIntervalMs = PollMs, CooldownMs = CooldownMs, TransferTimeoutMs = TimeoutMs,
        LandingGuardMs = GuardMs, CoastGuard = CoastGuard,
        CoastSearchTiles = CoastSearchX, CoastVerticalTiles = CoastSearchY
    };

    private static void Range(int value, int min, int max, string name)
    {
        if (value < min || value > max)
            throw new ArgumentException($"{name} 必须在 {min}～{max} 之间。");
    }
}
