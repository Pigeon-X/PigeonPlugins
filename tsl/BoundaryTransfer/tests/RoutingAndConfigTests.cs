using System.Text.Json;
using Newtonsoft.Json;

namespace BoundaryTransfer.Tests;

internal static class RoutingAndConfigTests
{
    public static void Add(List<AlgorithmCase> cases)
    {
        BoundarySettings chain = BoundarySettings.FromConfig(new() { WorldOrder = ["左界", "中界", "右界"] });
        cases.Add("route.middle.left", "routing", () => Check.Equal("左界", BoundaryPlanner.Neighbor(chain, "中界", BoundarySide.Left)));
        cases.Add("route.middle.right", "routing", () => Check.Equal("右界", BoundaryPlanner.Neighbor(chain, "中界", BoundarySide.Right)));
        cases.Add("route.endpoints.no-loop", "routing", () =>
        {
            Check.Equal<string?>(null, BoundaryPlanner.Neighbor(chain, "左界", BoundarySide.Left));
            Check.Equal<string?>(null, BoundaryPlanner.Neighbor(chain, "右界", BoundarySide.Right));
            Check.Equal("中界", BoundaryPlanner.Neighbor(chain, "左界", BoundarySide.Right));
            Check.Equal("中界", BoundaryPlanner.Neighbor(chain, "右界", BoundarySide.Left));
        });
        cases.Add("route.endpoints.loop", "routing", () =>
        {
            Check.Equal("右界", BoundaryPlanner.Neighbor(chain with { Loop = true }, "左界", BoundarySide.Left));
            Check.Equal("左界", BoundaryPlanner.Neighbor(chain with { Loop = true }, "右界", BoundarySide.Right));
        });
        cases.Add("route.case-insensitive", "routing", () =>
        {
            var settings = BoundarySettings.FromConfig(new() { WorldOrder = ["Alpha", "Beta", "Gamma"] });
            Check.Equal("Beta", BoundaryPlanner.Neighbor(settings, "ALPHA", BoundarySide.Right));
            Check.Equal("Beta", BoundaryPlanner.Neighbor(settings, "gAMMa", BoundarySide.Left));
        });
        cases.Add("route.absent-empty-single", "routing", () =>
        {
            Check.Equal<string?>(null, BoundaryPlanner.Neighbor(chain, "不存在", BoundarySide.Right));
            Check.Equal<string?>(null, BoundaryPlanner.Neighbor(BoundarySettings.Default, "不存在", BoundarySide.Left));
            Check.Equal<string?>(null, BoundaryPlanner.Neighbor(chain with { Worlds = ["独界"] }, "独界", BoundarySide.Left));
        });
        cases.Add("route.two-worlds.both-sides-loop", "routing", () =>
        {
            var settings = chain with { Worlds = ["A", "B"], Loop = true };
            foreach (BoundarySide side in Enum.GetValues<BoundarySide>())
            {
                Check.Equal("B", BoundaryPlanner.Neighbor(settings, "A", side));
                Check.Equal("A", BoundaryPlanner.Neighbor(settings, "B", side));
            }
        });
        cases.Add("config.defaults", "configuration", () =>
        {
            var s = BoundarySettings.Default;
            Check.True(s.Enabled); Check.False(s.Loop); Check.Equal(0, s.Worlds.Length);
            Check.Equal(2, s.ContactTolerance); Check.Equal(96, s.LandingInset);
            Check.Equal(320, s.SearchInward); Check.Equal(80, s.SearchVertical); Check.Equal(1, s.HazardPadding);
            Check.Equal(60000, s.MaxCandidates); Check.Equal(50, s.PollMs); Check.Equal(3000, s.CooldownMs);
            Check.Equal(20000, s.TimeoutMs); Check.Equal(2500, s.GuardMs);
        });
        cases.Add("config.worlds.trim-and-copy", "configuration", () =>
        {
            var config = new BoundaryTransferConfig { WorldOrder = ["  Left  ", "\tRight\r\n"] };
            var settings = BoundarySettings.FromConfig(config);
            Check.Sequence(new[] { "Left", "Right" }, settings.Worlds);
            config.WorldOrder[0] = "changed";
            Check.Equal("Left", settings.Worlds[0]);
            var copy = settings.ToConfig(); copy.WorldOrder[1] = "changed";
            Check.Equal("Right", settings.Worlds[1]);
        });
        cases.Add("config.world-count.bounds", "configuration", () =>
        {
            Check.Equal(0, BoundarySettings.FromConfig(new() { WorldOrder = null! }).Worlds.Length);
            Check.Equal(2, BoundarySettings.FromConfig(new() { WorldOrder = ["A", "B"] }).Worlds.Length);
            Check.Equal(64, BoundarySettings.FromConfig(new() { WorldOrder = Enumerable.Range(0, 64).Select(n => $"W{n}").ToList() }).Worlds.Length);
            Check.Throws<ArgumentException>(() => BoundarySettings.FromConfig(new() { WorldOrder = ["A"] }));
            Check.Throws<ArgumentException>(() => BoundarySettings.FromConfig(new() { WorldOrder = Enumerable.Range(0, 65).Select(n => $"W{n}").ToList() }));
        });
        cases.Add("config.world-names.invalid-and-duplicates", "configuration", () =>
        {
            foreach (List<string> names in new List<string>[] { ["A", "A"], ["Alpha", "aLPHA"], ["A", " A "], ["", "B"], [" \t", "B"], [null!, "B"] })
                Check.Throws<ArgumentException>(() => BoundarySettings.FromConfig(new() { WorldOrder = names }), "世界名");
        });

        (string Id, Action<BoundaryTransferConfig, int> Set, int Min, int Max)[] ranges =
        [
            ("contact-tolerance", (c,v) => c.ContactTolerancePixels = v, 0, 8),
            ("landing", (c,v) => c.LandingInsetTiles = v, 64, 600),
            ("inward", (c,v) => c.InwardSearchTiles = v, 0, 1000),
            ("vertical", (c,v) => c.VerticalSearchTiles = v, 0, 300),
            ("hazard-padding", (c,v) => c.HazardPaddingTiles = v, 0, 3),
            ("candidate-budget", (c,v) => c.MaxSearchCandidates = v, 100, 200000),
            ("poll", (c,v) => c.PollIntervalMs = v, 50, 1000),
            ("cooldown", (c,v) => c.CooldownMs = v, 1000, 60000),
            ("timeout", (c,v) => c.TransferTimeoutMs = v, 5000, 60000),
            ("guard", (c,v) => c.LandingGuardMs = v, 500, 5000)
        ];
        foreach (var range in ranges)
            cases.Add($"config.range.{range.Id}", "configuration", () =>
            {
                foreach (int valid in new[] { range.Min, range.Max })
                {
                    var config = new BoundaryTransferConfig { LandingInsetTiles = 300 };
                    range.Set(config, valid); _ = BoundarySettings.FromConfig(config); Check.True(true);
                }
                foreach (int invalid in new[] { range.Min - 1, range.Max + 1, int.MinValue, int.MaxValue })
                {
                    var config = new BoundaryTransferConfig { LandingInsetTiles = 300 };
                    range.Set(config, invalid); Check.Throws<ArgumentException>(() => BoundarySettings.FromConfig(config));
                }
            });
        cases.Add("config.landing.independent-of-contact-tolerance", "configuration", () =>
        {
            foreach (int tolerance in new[] { 0, 2, 8 })
                foreach (int landing in new[] { 64, 96, 215, 216, 600 })
                {
                    var settings = BoundarySettings.FromConfig(new() { ContactTolerancePixels = tolerance, LandingInsetTiles = landing });
                    Check.Equal(tolerance, settings.ContactTolerance);
                    Check.Equal(landing, settings.LandingInset);
                }
        });
        cases.Add("config.settings.roundtrip.all-fields", "configuration", () =>
        {
            var first = CustomSettings(); var second = BoundarySettings.FromConfig(first.ToConfig());
            EqualSettings(first, second);
        });
        cases.Add("config.json.system-text.roundtrip", "configuration", () =>
        {
            var first = CustomSettings(); string json = System.Text.Json.JsonSerializer.Serialize(first.ToConfig(), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            Check.True(json.Contains("从左到右的世界名", StringComparison.Ordinal));
            Check.True(json.Contains("碰边容差_像素", StringComparison.Ordinal));
            Check.False(json.Contains("WorldOrder", StringComparison.Ordinal));
            Check.False(json.Contains("ContactTolerancePixels", StringComparison.Ordinal));
            Check.False(json.Contains("触发边界距离_格", StringComparison.Ordinal));
            EqualSettings(first, BoundarySettings.FromConfig(System.Text.Json.JsonSerializer.Deserialize<BoundaryTransferConfig>(json)!));
        });
        cases.Add("config.json.newtonsoft.roundtrip", "configuration", () =>
        {
            var first = CustomSettings(); string json = JsonConvert.SerializeObject(first.ToConfig());
            Check.False(json.Contains("进入对侧内缩_格", StringComparison.Ordinal));
            Check.True(json.Contains("碰边容差_像素", StringComparison.Ordinal));
            Check.False(json.Contains("LandingInsetTiles", StringComparison.Ordinal));
            Check.False(json.Contains("ContactTolerancePixels", StringComparison.Ordinal));
            Check.False(json.Contains("触发边界距离_格", StringComparison.Ordinal));
            EqualSettings(first, BoundarySettings.FromConfig(JsonConvert.DeserializeObject<BoundaryTransferConfig>(json)!));
        });
        cases.Add("config.json.cross-serializer.roundtrip", "configuration", () =>
        {
            var first = CustomSettings();
            EqualSettings(first, BoundarySettings.FromConfig(JsonConvert.DeserializeObject<BoundaryTransferConfig>(System.Text.Json.JsonSerializer.Serialize(first.ToConfig()))!));
            EqualSettings(first, BoundarySettings.FromConfig(System.Text.Json.JsonSerializer.Deserialize<BoundaryTransferConfig>(JsonConvert.SerializeObject(first.ToConfig()))!));
        });
        cases.Add("config.json.legacy-trigger.system-text.ignored", "configuration", () =>
        {
            foreach (int legacy in new[] { 48, 0 })
            {
                string json = $$"""{"触发边界距离_格":{{legacy}},"进入对侧内缩_格":64}""";
                var settings = BoundarySettings.FromConfig(System.Text.Json.JsonSerializer.Deserialize<BoundaryTransferConfig>(json)!);
                AssertLegacyIgnored(settings);
                using var document = JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(settings.ToConfig()));
                Check.False(document.RootElement.TryGetProperty("触发边界距离_格", out _));
                Check.Equal(2, document.RootElement.GetProperty("碰边容差_像素").GetInt32());
            }
        });
        cases.Add("config.json.legacy-trigger.newtonsoft.ignored", "configuration", () =>
        {
            foreach (int legacy in new[] { 48, 0 })
            {
                string json = $$"""{"触发边界距离_格":{{legacy}},"进入对侧内缩_格":64}""";
                var settings = BoundarySettings.FromConfig(JsonConvert.DeserializeObject<BoundaryTransferConfig>(json)!);
                AssertLegacyIgnored(settings);
                using var document = JsonDocument.Parse(JsonConvert.SerializeObject(settings.ToConfig()));
                Check.False(document.RootElement.TryGetProperty("触发边界距离_格", out _));
                Check.Equal(2, document.RootElement.GetProperty("碰边容差_像素").GetInt32());
            }
        });
        cases.Add("config.json.legacy-trigger.explicit-contact-wins", "configuration", () =>
        {
            foreach (int legacy in new[] { 48, 0 })
                foreach (int tolerance in new[] { 0, 8 })
                {
                    string json = $$"""{"触发边界距离_格":{{legacy}},"碰边容差_像素":{{tolerance}}}""";
                    var systemText = BoundarySettings.FromConfig(System.Text.Json.JsonSerializer.Deserialize<BoundaryTransferConfig>(json)!);
                    var newtonsoft = BoundarySettings.FromConfig(JsonConvert.DeserializeObject<BoundaryTransferConfig>(json)!);
                    EqualSettings(systemText, newtonsoft);
                    Check.Equal(tolerance, systemText.ContactTolerance);
                    Check.Equal<BoundarySide?>(tolerance == 0 ? null : BoundarySide.Left,
                        BoundaryContact.Side(642, 20, 0, 4200 * 16f, systemText.ContactTolerance));
                    Check.Equal<BoundarySide?>(null, BoundaryContact.Side(48 * 16, 20, 0, 4200 * 16f, systemText.ContactTolerance));
                }
        });
        cases.Add("config.surface.no-legacy-trigger-or-message-members", "configuration", () =>
        {
            Check.Equal<System.Reflection.PropertyInfo?>(null, typeof(BoundaryTransferConfig).GetProperty("TriggerInsetTiles"));
            Check.Equal<System.Reflection.PropertyInfo?>(null, typeof(BoundarySettings).GetProperty("TriggerInset"));
            Check.Equal<System.Reflection.PropertyInfo?>(null, typeof(BoundaryTransferConfig).GetProperty("ShowMessages"));
            Check.Equal<System.Reflection.PropertyInfo?>(null, typeof(BoundarySettings).GetProperty("ShowMessages"));
        });
        cases.Add("config.json.legacy-show-messages.ignored", "configuration", () =>
        {
            foreach (bool legacy in new[] { false, true })
            {
                string json = $$"""{"显示切换提示":{{legacy.ToString().ToLowerInvariant()}}}""";
                var systemText = BoundarySettings.FromConfig(System.Text.Json.JsonSerializer.Deserialize<BoundaryTransferConfig>(json)!);
                var newtonsoft = BoundarySettings.FromConfig(JsonConvert.DeserializeObject<BoundaryTransferConfig>(json)!);
                EqualSettings(BoundarySettings.Default, systemText);
                EqualSettings(BoundarySettings.Default, newtonsoft);
                using var first = JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(systemText.ToConfig()));
                using var second = JsonDocument.Parse(JsonConvert.SerializeObject(newtonsoft.ToConfig()));
                Check.False(first.RootElement.TryGetProperty("显示切换提示", out _));
                Check.False(second.RootElement.TryGetProperty("显示切换提示", out _));
            }
        });
    }

    private static void AssertLegacyIgnored(BoundarySettings settings)
    {
        Check.Equal(2, settings.ContactTolerance);
        Check.Equal(96, settings.LandingInset); // 旧内缩键在1.1.0被忽略，保留默认值
        Check.Equal(50, settings.PollMs);
        const float right = 4200 * 16f;
        Check.Equal<BoundarySide?>(null, BoundaryContact.Side(48 * 16, 20, 0, right, settings.ContactTolerance));
        Check.Equal<BoundarySide?>(null, BoundaryContact.Side(right - 48 * 16 - 20, 20, 0, right, settings.ContactTolerance));
        Check.Equal<BoundarySide?>(BoundarySide.Left, BoundaryContact.Side(640, 20, 0, right, settings.ContactTolerance));
        Check.Equal<BoundarySide?>(BoundarySide.Right, BoundaryContact.Side(right - 640 - 20, 20, 0, right, settings.ContactTolerance));
    }

    private static BoundarySettings CustomSettings() => BoundarySettings.FromConfig(new()
    {
        Enabled = false, WorldOrder = ["左", "Middle", "右"], Loop = true, ContactTolerancePixels = 7,
        PollIntervalMs = 123, CooldownMs = 4321, TransferTimeoutMs = 23456,
        LandingGuardMs = 3456
    });

    private static void EqualSettings(BoundarySettings expected, BoundarySettings actual)
    {
        Check.Sequence(expected.Worlds, actual.Worlds);
        Check.Equal(expected with { Worlds = [] }, actual with { Worlds = [] });
    }
}
