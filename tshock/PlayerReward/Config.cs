using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlayerReward;

public sealed class PlayerRewardConfig
{
    public string Hint1 { get; set; } = "各个礼包的名字需唯一，不可重复";
    public string Hint2 { get; set; } = "Item格式为 'ID*数量*前缀' ";
    public string Hint3 { get; set; } = "ExecuteCommands 中可以使用 {name} 和 {account} 占位符";
    public string Hint4 { get; set; } = "IsHardMode=true 的礼包只会在困难模式出现";

    public List<RewardPackConfig> PlayerPacks { get; set; } = new();
    public List<RewardPackConfig> SponsorPacks { get; set; } = new();

    [JsonPropertyName("HardModeReplacesNormal")]
    public bool HardModeReplacesNormal { get; set; } = false;
}

public sealed class RewardPackConfig
{
    public string Name { get; set; } = "Unique Player Pack Name";
    public List<string> Groups { get; set; } = new();
    public List<string> Items { get; set; } = new();
    public List<string> ExecuteCommands { get; set; } = new();
    public bool IsHardMode { get; set; }

    public string? Replaces { get; set; }
}

public sealed record RewardItem(int Id, int Stack, int Prefix)
{
    public static RewardItem Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormatException("物品配置为空");
        }

        var parts = text.Split('*', StringSplitOptions.TrimEntries);
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var id) ||
            !int.TryParse(parts[1], out var stack) ||
            !int.TryParse(parts[2], out var prefix))
        {
            throw new FormatException($"物品格式错误：{text}，应为 ID*数量*前缀");
        }

        if (id <= 0 || stack <= 0 || prefix < 0 || prefix > byte.MaxValue)
        {
            throw new FormatException($"物品数值越界：{text}");
        }

        return new RewardItem(id, stack, prefix);
    }
}

public sealed record RuntimePack(
    string Name,
    IReadOnlySet<string> Groups,
    IReadOnlyList<RewardItem> Items,
    IReadOnlyList<string> Commands,
    bool IsHardMode,
    string? Replaces);

public sealed class RewardCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public PlayerRewardConfig Config { get; }
    public IReadOnlyList<RuntimePack> PlayerPacks { get; }
    public IReadOnlyList<RuntimePack> SponsorPacks { get; }

    private RewardCatalog(PlayerRewardConfig config, IReadOnlyList<RuntimePack> playerPacks, IReadOnlyList<RuntimePack> sponsorPacks)
    {
        this.Config = config;
        this.PlayerPacks = playerPacks;
        this.SponsorPacks = sponsorPacks;
    }

    public static RewardCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            WriteDefaultConfig(path);
        }

        PlayerRewardConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<PlayerRewardConfig>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"PlayerReward.json 解析失败：{ex.Message}", ex);
        }

        config ??= new PlayerRewardConfig();
        config.PlayerPacks ??= new List<RewardPackConfig>();
        config.SponsorPacks ??= new List<RewardPackConfig>();

        var playerPacks = BuildPacks(config.PlayerPacks, "PlayerPacks");
        var sponsorPacks = BuildPacks(config.SponsorPacks, "SponsorPacks");
        return new RewardCatalog(config, playerPacks, sponsorPacks);
    }

    private static IReadOnlyList<RuntimePack> BuildPacks(IEnumerable<RewardPackConfig> source, string section)
    {
        var result = new List<RuntimePack>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pack in source)
        {
            if (string.IsNullOrWhiteSpace(pack.Name))
            {
                throw new InvalidDataException($"{section} 中存在空礼包名");
            }

            if (!names.Add(pack.Name.Trim()))
            {
                throw new InvalidDataException($"{section} 中礼包名重复：{pack.Name}");
            }

            var groups = new HashSet<string>(
                (pack.Groups ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase);
            if (groups.Count == 0)
            {
                throw new InvalidDataException($"{section}/{pack.Name} 没有配置 Groups");
            }

            var items = (pack.Items ?? new List<string>()).Select(RewardItem.Parse).ToArray();
            var commands = (pack.ExecuteCommands ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToArray();
            result.Add(new RuntimePack(pack.Name.Trim(), groups, items, commands, pack.IsHardMode, pack.Replaces));
        }

        return result;
    }

    private static void WriteDefaultConfig(string path)
    {
        var assembly = typeof(RewardCatalog).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith("PlayerReward.json", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            return;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        stream.CopyTo(file);
    }

    public static void Save(PlayerRewardConfig config, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
    }

    public static IReadOnlyList<RuntimePack> Filter(
        IReadOnlyList<RuntimePack> packs,
        IReadOnlySet<string> groups,
        bool hardMode,
        bool hardModeReplacesNormal)
    {
        var eligible = packs
            .Where(pack => (!pack.IsHardMode || hardMode) && pack.Groups.Overlaps(groups))
            .ToList();

        if (!hardMode || !hardModeReplacesNormal)
        {
            return eligible;
        }

        var replacedNames = new HashSet<string>(
            eligible.Where(x => x.IsHardMode && !string.IsNullOrWhiteSpace(x.Replaces))
                .Select(x => x.Replaces!),
            StringComparer.OrdinalIgnoreCase);
        return eligible
            .Where(x => x.IsHardMode || !replacedNames.Contains(x.Name))
            .ToList();
    }
}
