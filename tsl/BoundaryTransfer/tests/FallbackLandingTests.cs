using System.Text.Json;

namespace BoundaryTransfer.Tests;

internal static class FallbackLandingTests
{
    public static void Add(List<AlgorithmCase> cases)
    {
        AddTerrain(cases);
        AddSafety(cases);
        AddOrderAndBody(cases);
        AddBudgetsAndValidation(cases);
        AddProperties(cases);
    }

    private static void AddTerrain(List<AlgorithmCase> cases)
    {
        cases.Add("fallback.ocean.shore-beyond-old-inward", "fallback-terrain", () =>
        {
            var map = new MapBuilder(240, 160).Rect(0, 109, 0, 159, Tiles.Water)
                .Rect(110, 124, 68, 159, Tiles.Solid).Freeze();
            var settings = Fixtures.Settings(inward: 4, vertical: 2);
            Check.False(Fixtures.Find(map, settings, feet: 80 * 16f).Success);
            LandingResult result = Run(map, settings, feet: 80 * 16f);
            Success(result, map, 112, 68, settings, desired: 80 * 16f);
            Check.True(Fixtures.CenterTile(result.Spot) > settings.LandingInset + settings.SearchInward);
        });
        cases.Add("fallback.sky.high-existing-ground", "fallback-terrain", () =>
        {
            var map = new MapBuilder(240, 160).FloorAt(96, 50).Freeze();
            var settings = Fixtures.Settings(inward: 2, vertical: 2);
            Check.False(Fixtures.Find(map, settings, feet: 110 * 16f).Success);
            Success(Run(map, settings, feet: 110 * 16f), map, 96, 50, settings, desired: 110 * 16f);
        });
        cases.Add("fallback.ocean.deep-dry-cave-far-y", "fallback-terrain", () =>
        {
            var map = new MapBuilder(240, 160, Tiles.Water).Rect(94, 98, 98, 106, Tiles.Air)
                .FloorAt(96, 105).Freeze();
            var settings = Fixtures.Settings(inward: 2, vertical: 2);
            Check.False(Fixtures.Find(map, settings, feet: 50 * 16f).Success);
            Success(Run(map, settings), map, 96, 105, settings);
        });
        cases.Add("fallback.solid-terrain.underground-dry-cave", "fallback-terrain", () =>
        {
            var map = new MapBuilder(240, 160, Tiles.Solid).Rect(95, 97, 102, 104, Tiles.Air).Freeze();
            var settings = Fixtures.Settings(inward: 2, vertical: 2);
            Check.False(Fixtures.Find(map, settings, feet: 50 * 16f).Success);
            Success(Run(map, settings), map, 96, 105, settings);
        });
        cases.Add("fallback.ocean.submerged-support-still-rejected", "fallback-terrain", () =>
        {
            var map = new MapBuilder(209, 100, Tiles.Water).Floor(0, 208, 50).Freeze();
            var settings = Fixtures.Settings();
            Check.False(Fixtures.Find(map, settings, feet: 50 * 16f).Success);
            Fixtures.Failure(Run(map, settings), FullCount(map, settings));
        });
    }

    private static void AddSafety(List<AlgorithmCase> cases)
    {
        cases.Add("fallback.safe.incomplete-footprint-rejected", "fallback-safety", () =>
        {
            var map = new MapBuilder(209, 100).Put(96, 50, Tiles.Solid).Freeze();
            var settings = Fixtures.Settings();
            Fixtures.Failure(Run(map, settings), FullCount(map, settings));
        });
        cases.Add("fallback.safe.entire-body-obstructions-rejected", "fallback-safety", () =>
        {
            var settings = Fixtures.Settings();
            for (int x = 95; x <= 97; x++)
                for (int y = 47; y < 50; y++)
                {
                    var map = new MapBuilder(209, 100).FloorAt(96, 50).Put(x, y, Tiles.BodyObstacle).Freeze();
                    Fixtures.Failure(Run(map, settings), FullCount(map, settings));
                }
        });
        cases.Add("fallback.safe.liquids-in-body-floor-and-buffer", "fallback-safety", () =>
        {
            var settings = Fixtures.Settings();
            foreach (TileCell liquid in new[] { Tiles.Water, Tiles.Lava, Tiles.Honey, Tiles.Shimmer })
            {
                foreach (var (x, y) in new[] { (96, 49), (94, 49), (96, 46), (98, 51) })
                {
                    var map = new MapBuilder(209, 100).FloorAt(96, 50).Put(x, y, liquid).Freeze();
                    Fixtures.Failure(Run(map, settings), FullCount(map, settings));
                }
                var wetFloor = new MapBuilder(209, 100).FloorAt(96, 50)
                    .Put(96, 50, liquid with { SupportsFeet = true }).Freeze();
                Fixtures.Failure(Run(wetFloor, settings), FullCount(wetFloor, settings));
            }
        });
        cases.Add("fallback.safe.hazard-buffer-still-enforced", "fallback-safety", () =>
        {
            var settings = Fixtures.Settings();
            foreach (var (x, y) in new[] { (94, 46), (98, 46), (94, 51), (98, 51), (96, 46), (94, 49) })
            {
                var map = new MapBuilder(209, 100).FloorAt(96, 50).Put(x, y, Tiles.Hazard).Freeze();
                Fixtures.Failure(Run(map, settings), FullCount(map, settings));
                var noPadding = settings with { HazardPadding = 0 };
                Success(Run(map, noPadding), map, 96, 50, noPadding);
            }
        });
        cases.Add("fallback.safe.nonflat-hazardous-or-wet-floor-rejected", "fallback-safety", () =>
        {
            var settings = Fixtures.Settings();
            foreach (TileCell floor in new[] { Tiles.HalfBrick, Tiles.Slope, Tiles.Inactive, Tiles.Decorative,
                Tiles.Unknown, Tiles.HazardousFloor, Tiles.WetSolid })
            {
                var map = new MapBuilder(209, 100).Floor(95, 97, 50, floor).Freeze();
                Fixtures.Failure(Run(map, settings), FullCount(map, settings));
            }
        });
    }

    private static void AddOrderAndBody(List<AlgorithmCase> cases)
    {
        cases.Add("fallback.order.nearest-x-before-nearest-y", "fallback-order", () =>
        {
            var map = new MapBuilder(240, 160).FloorAt(96, 100).FloorAt(100, 80).Freeze();
            var settings = Fixtures.Settings();
            Success(Run(map, settings, feet: 80 * 16f), map, 96, 100, settings, desired: 80 * 16f);
        });
        cases.Add("fallback.order.nearest-y-within-column", "fallback-order", () =>
        {
            var map = new MapBuilder(240, 160).FloorAt(96, 70).FloorAt(96, 100).Freeze();
            var settings = Fixtures.Settings();
            Success(Run(map, settings, feet: 80 * 16f), map, 96, 70, settings, desired: 80 * 16f);
        });
        cases.Add("fallback.order.equal-y-distance-up-before-down", "fallback-order", () =>
        {
            var map = new MapBuilder(240, 160).FloorAt(96, 70).FloorAt(96, 90).Freeze();
            var settings = Fixtures.Settings();
            Success(Run(map, settings, feet: 80 * 16f), map, 96, 70, settings, desired: 80 * 16f);
        });
        cases.Add("fallback.order.left-right-mirror", "fallback-order", () =>
        {
            var settings = Fixtures.Settings();
            var left = new MapBuilder(240, 140).FloorAt(100, 60).Freeze();
            var right = new MapBuilder(240, 140).FloorAt(140, 60).Freeze();
            LandingResult a = Run(left, settings, feet: 90 * 16f, budget: 13);
            LandingResult b = Run(right, settings, BoundarySide.Right, 90 * 16f, budget: 17);
            Success(a, left, 100, 60, settings, desired: 90 * 16f);
            Success(b, right, 140, 60, settings, BoundarySide.Right, 90 * 16f);
            Check.Equal(a.Candidates, b.Candidates);
            Check.Equal(left.Width * 16f + 16 - 20, a.Spot.X + b.Spot.X);
            Check.Equal(a.Spot.Y, b.Spot.Y);
        });
        cases.Add("fallback.body.dynamic-width-height-and-clearance", "fallback-body", () =>
        {
            var settings = Fixtures.Settings();
            foreach (int width in new[] { 1, 16, 20, 31, 32, 48, 128 })
                foreach (int height in new[] { 1, 16, 32, 42, 256 })
                {
                    var map = new MapBuilder(240, 140).FloorAt(96, 80, width).Freeze();
                    Success(Run(map, settings, feet: 80 * 16f, width: width, height: height), map,
                        96, 80, settings, desired: 80 * 16f, width: width, height: height);
                }
            var lowCeiling = new MapBuilder(209, 100).FloorAt(96, 50).Put(96, 47, Tiles.BodyObstacle).Freeze();
            Success(Run(lowCeiling, settings, height: 16), lowCeiling, 96, 50, settings, height: 16);
            Fixtures.Failure(Run(lowCeiling, settings, height: 42), FullCount(lowCeiling, settings));
        });
        cases.Add("fallback.body.native-guards-and-world-range-endpoints", "fallback-body", () =>
        {
            var settings = Fixtures.Settings();
            var bounds = new MapBuilder(209, 100).FloorAt(96, 45).FloorAt(96, 58).Freeze();
            Success(Run(bounds, settings, feet: 0), bounds, 96, 45, settings, desired: 0);
            Success(Run(bounds, settings, feet: 100 * 16f), bounds, 96, 58, settings, desired: 100 * 16f);
            var outside = new MapBuilder(209, 100).FloorAt(96, 44).FloorAt(96, 59).Freeze();
            Fixtures.Failure(Run(outside, settings), FullCount(outside, settings));
            var tall = new MapBuilder(209, 100).FloorAt(96, 58).Freeze();
            Success(Run(tall, settings, height: 256), tall, 96, 58, settings, height: 256);
            var aboveTallGuard = new MapBuilder(209, 100).FloorAt(96, 57).Freeze();
            Fixtures.Failure(Run(aboveTallGuard, settings, height: 256), FullCount(aboveTallGuard, settings, height: 256));
        });
    }

    private static void AddBudgetsAndValidation(List<AlgorithmCase> cases)
    {
        cases.Add("fallback.budget.strict-resumable-candidates-no-repeat", "fallback-budget", () =>
        {
            var settings = Fixtures.Settings();
            var map = new MapBuilder(209, 100).Freeze();
            var search = Search(map, settings);
            Pending(search.Advance(map, 7), 7);
            Pending(search.Advance(map, 11), 18);
            Pending(search.Advance(map, 1), 19);
            LandingSearchStep final = Drain(search, map, 9, previous: 19);
            Check.True(final.Complete);
            Fixtures.Failure(final.Result, FullCount(map, settings));
            Check.Equal((long)final.Result.Candidates, map.Reads);
            Check.Equal(final.Result.Candidates, map.Visited.Count);
            long reads = map.Reads;
            Check.Equal(final, search.Advance(new UnreadableMap(), 0, _ => throw new InvalidOperationException("不应验证")));
            Check.Equal(reads, map.Reads);
        });
        cases.Add("fallback.budget.last-allowed-candidate-can-succeed", "fallback-budget", () =>
        {
            var settings = Fixtures.Settings();
            var map = new MapBuilder(209, 100).FloorAt(96, 45).Freeze();
            var search = Search(map, settings);
            int ordinal = ExpectedCount(map, settings, BoundarySide.Left, 50 * 16f, 96, 45, 42);
            Pending(search.Advance(map, ordinal - 1), ordinal - 1);
            LandingSearchStep last = search.Advance(map, 1);
            Check.True(last.Complete);
            Success(last.Result, map, 96, 45, settings);
            Check.Equal(last.Result, Run(map, settings, budget: ordinal));
        });
        cases.Add("fallback.budget.ignores-old-limits-including-zero-y", "fallback-budget", () =>
        {
            var map = new MapBuilder(240, 160).FloorAt(100, 110).Freeze();
            var settings = Fixtures.Settings(inward: 0, vertical: 0, budget: 100);
            Fixtures.Failure(Fixtures.Find(map, settings, feet: 50 * 16f), 1);
            LandingResult result = Run(map, settings, budget: 7);
            Success(result, map, 100, 110, settings);
            Check.True(result.Candidates > settings.MaxCandidates);
            Check.True(Fixtures.CenterTile(result.Spot) - settings.LandingInset > settings.SearchInward);
            Check.True(Math.Abs(result.Spot.FeetY - 50) > settings.SearchVertical);
        });
        cases.Add("fallback.budget.invalid-values-finish-without-map-reads", "fallback-budget", () =>
        {
            var settings = Fixtures.Settings();
            var map = new MapBuilder(209, 100).Freeze();
            foreach (int budget in new[] { 0, -1, int.MinValue })
            {
                var search = Search(map, settings);
                LandingSearchStep invalid = search.Advance(map, budget);
                Check.True(invalid.Complete);
                Fixtures.Failure(invalid.Result, 0, "预算");
                Check.Equal(invalid, search.Advance(new UnreadableMap(), 10));
            }
            Check.Equal(0L, map.Reads);
            var continued = Search(map, settings);
            Pending(continued.Advance(map, 3), 3);
            LandingSearchStep stop = continued.Advance(map, 0);
            Check.True(stop.Complete);
            Fixtures.Failure(stop.Result, 3, "预算");
            Check.Equal(3L, map.Reads);
        });
        cases.Add("fallback.terminal.success-is-stable-without-reading-map", "fallback-terminal", () =>
        {
            var map = new MapBuilder(209, 100).FloorAt(96, 50).Freeze();
            var settings = Fixtures.Settings();
            var search = Search(map, settings);
            LandingSearchStep terminal = search.Advance(map, 1);
            Check.True(terminal.Complete);
            Success(terminal.Result, map, 96, 50, settings);
            long reads = map.Reads;
            for (int i = 0; i < 5; i++)
                Check.Equal(terminal, search.Advance(new UnreadableMap(), -1,
                    _ => throw new InvalidOperationException("终态不应执行 verify"), 1));
            Check.Equal(reads, map.Reads);
        });
        cases.Add("fallback.validation.map-dimension-changes-invalidate-cursor", "fallback-validation", () =>
        {
            var settings = Fixtures.Settings();
            foreach (var (width, height) in new[] { (210, 100), (209, 101) })
            {
                var original = new MapBuilder(209, 100).Freeze();
                var replacement = new MapBuilder(width, height).Freeze();
                var search = Search(original, settings);
                Pending(search.Advance(original, 3), 3);
                LandingSearchStep invalid = search.Advance(replacement, 10);
                Check.True(invalid.Complete);
                Fixtures.Failure(invalid.Result, 3, "尺寸");
                Check.Equal(3L, original.Reads);
                Check.Equal(0L, replacement.Reads);
                Check.Equal(invalid, search.Advance(new UnreadableMap(), 10));
            }
        });
        cases.Add("fallback.validation.finite-feet-and-world-range", "fallback-validation", () =>
        {
            var map = new MapBuilder(209, 100).FloorAt(96, 50).Freeze();
            var settings = Fixtures.Settings();
            foreach (float feet in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity,
                float.MaxValue, -float.MaxValue, 1e30f, -1e30f, -1f, 100 * 16f + 1 })
            {
                var search = Search(map, settings, feet: feet);
                LandingSearchStep invalid = search.Advance(map, 5);
                Check.True(invalid.Complete);
                Fixtures.Failure(invalid.Result, 0, "无效");
                Check.Equal(invalid, search.Advance(new UnreadableMap(), 5));
            }
            Check.Equal(0L, map.Reads);
            foreach (var (pixels, expected) in new[] { (50 * 16f + 7, 50), (50 * 16f + 8, 50),
                (50 * 16f + 9, 51), (51 * 16f + 8, 52) })
            {
                var rounded = new MapBuilder(209, 100).FloorAt(96, expected).Freeze();
                Success(Run(rounded, settings, feet: pixels), rounded, 96, expected, settings, desired: pixels);
            }
        });
        cases.Add("fallback.validation.player-world-side-and-settings", "fallback-validation", () =>
        {
            var settings = Fixtures.Settings();
            foreach (var (width, height) in new[] { (208, 100), (100, 100), (209, 99), (0, 0),
                (-1, 100), (209, -1), (int.MaxValue, int.MaxValue) })
            {
                var map = new DimensionOnlyMap(width, height);
                LandingSearchStep invalid = Search(map, settings).Advance(map, 5);
                Check.True(invalid.Complete);
                Fixtures.Failure(invalid.Result, 0);
                Check.Equal(0, map.Reads);
            }
            var valid = new MapBuilder(209, 100).Freeze();
            foreach (var (width, height) in new[] { (0, 42), (-1, 42), (129, 42), (20, 0), (20, -1), (20, 257) })
            {
                LandingSearchStep invalid = Search(valid, settings, width: width, height: height).Advance(valid, 5);
                Check.True(invalid.Complete);
                Fixtures.Failure(invalid.Result, 0, "无效");
            }
            foreach (BoundarySettings invalidSettings in new[] { settings with { LandingInset = 63 },
                settings with { LandingInset = 601 }, settings with { LandingInset = int.MaxValue },
                settings with { HazardPadding = -1 }, settings with { HazardPadding = 4 } })
            {
                LandingSearchStep invalid = Search(valid, invalidSettings).Advance(valid, 5);
                Check.True(invalid.Complete);
                Fixtures.Failure(invalid.Result, 0, "无效");
            }
            LandingSearchStep side = Search(valid, settings, (BoundarySide)123).Advance(valid, 5);
            Check.True(side.Complete);
            Fixtures.Failure(side.Result, 0, "无效");
            Check.Equal(0L, valid.Reads);
        });
    }

    private static void AddProperties(List<AlgorithmCase> cases)
    {
        cases.Add("fallback.half-world.inclusive-center-never-opposite-or-outside-inset", "fallback-scope", () =>
        {
            var settings = Fixtures.Settings();
            foreach (BoundarySide side in Enum.GetValues<BoundarySide>())
            {
                var atMiddle = new MapBuilder(240, 100).FloorAt(120, 50).Freeze();
                Success(Run(atMiddle, settings, side), atMiddle, 120, 50, settings, side);
                int opposite = side == BoundarySide.Left ? 124 : 116;
                int outsideInset = side == BoundarySide.Left ? 92 : 148;
                var excluded = new MapBuilder(240, 100).FloorAt(opposite, 50).FloorAt(outsideInset, 50).Freeze();
                Fixtures.Failure(Run(excluded, settings, side), FullCount(excluded, settings, side));
                Check.True(side == BoundarySide.Left
                    ? excluded.Visited.All(p => p.X <= 121)
                    : excluded.Visited.All(p => p.X >= 119));
            }
        });
        cases.Add("fallback.property.immutable-map-settings-and-repeatable-result", "fallback-properties", () =>
        {
            var settings = Fixtures.Settings(inward: 0, vertical: 0, padding: 2, budget: 100);
            var map = new MapBuilder(240, 140).FloorAt(101, 70).Put(96, 50, Tiles.Water).Freeze();
            string beforeMap = map.Snapshot();
            string beforeSettings = JsonSerializer.Serialize(settings.ToConfig());
            LandingResult first = Run(map, settings, budget: 1);
            Success(first, map, 101, 70, settings);
            foreach (int budget in new[] { 2, 13, 128, 10000 })
                Check.Equal(first, Run(map, settings, budget: budget));
            Check.Equal(beforeMap, map.Snapshot());
            Check.Equal(beforeSettings, JsonSerializer.Serialize(settings.ToConfig()));
        });
        cases.Add("fallback.verify.rejected-safe-spots-continue-in-same-column", "fallback-verification", () =>
        {
            var settings = Fixtures.Settings();
            var map = new MapBuilder(240, 140).FloorAt(96, 50).FloorAt(96, 70).FloorAt(100, 50).Freeze();
            var verified = new List<LandingSpot>();
            bool Verify(LandingSpot spot)
            {
                Check.True(BoundaryPlanner.IsSafe(map, spot, 20, 42, settings.HazardPadding));
                verified.Add(spot);
                return spot.FeetY == 70;
            }
            var search = Search(map, settings);
            Pending(search.Advance(map, 1, Verify), 1);
            LandingResult result = Drain(search, map, 7, Verify, previous: 1).Result;
            Success(result, map, 96, 70, settings);
            Check.Sequence(new[] { Fixtures.Spot(96, 50), Fixtures.Spot(96, 70) }, verified);
            int calls = 0;
            LandingResult rejected = Run(map, settings, verify: _ => { calls++; return false; });
            Fixtures.Failure(rejected, FullCount(map, settings));
            Check.Equal(3, calls);
        });
        cases.Add("fallback.exhaustion.air-or-liquid-finite-without-platforms", "fallback-exhaustion", () =>
        {
            var settings = Fixtures.Settings();
            foreach (TileCell cell in new[] { Tiles.Air, Tiles.Water, Tiles.WetSolid })
                foreach (BoundarySide side in Enum.GetValues<BoundarySide>())
                {
                    var map = new MapBuilder(209, 100, cell).Freeze();
                    string before = map.Snapshot();
                    LandingResult result = Run(map, settings, side, budget: 13);
                    Fixtures.Failure(result, FullCount(map, settings, side));
                    Check.Equal(before, map.Snapshot());
                    Check.Equal((long)result.Candidates, map.Reads);
                }
        });
        cases.Add("fallback.time-budget.pending-preserves-exact-cursor", "fallback-budget", () =>
        {
            var settings = Fixtures.Settings();
            var underlying = new MapBuilder(240, 140).Freeze();
            var map = new DelayFirstReadMap(underlying);
            var search = Search(map, settings);
            LandingSearchStep timed = search.Advance(map, 1000, maxMilliseconds: 1);
            Pending(timed, 128);
            Check.Equal(128L, underlying.Reads);
            LandingSearchStep final = Drain(search, map, 37, previous: 128);
            Fixtures.Failure(final.Result, FullCount(map, settings));
            Check.Equal((long)final.Result.Candidates, underlying.Reads);
            Check.Equal(final.Result.Candidates, underlying.Visited.Count);
        });
        cases.Add("fallback.property.small-fixed-seed-independent-order-oracle", "fallback-properties", () =>
        {
            var settings = Fixtures.Settings(inward: 0, vertical: 0, budget: 100);
            var random = new Random(314159);
            for (int i = 0; i < 30; i++)
            {
                BoundarySide side = i % 2 == 0 ? BoundarySide.Left : BoundarySide.Right;
                int desired = random.Next(0, 141);
                var builder = new MapBuilder(240, 140);
                for (int n = 0; n < 12; n++)
                {
                    int center = side == BoundarySide.Left ? 96 + random.Next(0, 25) : 144 - random.Next(0, 25);
                    int feet = random.Next(45, 99);
                    builder.FloorAt(center, feet);
                    if (n % 3 == 0) builder.Put(center, feet - 1, Tiles.Water);
                    else if (n % 3 == 1) builder.Put(center - 2, feet, Tiles.Hazard);
                }
                var map = builder.Freeze();
                string before = map.Snapshot();
                LandingResult expected = Oracle(map, settings, side, desired * 16f, 20, 42);
                LandingResult actual = Run(map, settings, side, desired * 16f, budget: 1 + i % 19);
                Check.Equal(expected.Success, actual.Success, $"seed=314159 index={i}");
                Check.Equal(expected.Spot, actual.Spot, $"seed=314159 index={i}");
                Check.Equal(expected.Candidates, actual.Candidates, $"seed=314159 index={i}");
                Check.True(actual.Success ? actual.Error == "" : !string.IsNullOrWhiteSpace(actual.Error));
                Check.Equal(before, map.Snapshot());
            }
        });
    }

    private static BoundaryFallbackSearch Search(ILandingMap map, BoundarySettings settings,
        BoundarySide side = BoundarySide.Left, float feet = 50 * 16f, int width = 20, int height = 42)
        => new(map, side, feet, width, height, settings);

    private static LandingResult Run(ILandingMap map, BoundarySettings settings,
        BoundarySide side = BoundarySide.Left, float feet = 50 * 16f, int width = 20, int height = 42,
        int budget = 37, Func<LandingSpot, bool>? verify = null)
        => Drain(Search(map, settings, side, feet, width, height), map, budget, verify).Result;

    private static LandingSearchStep Drain(BoundaryFallbackSearch search, ILandingMap map, int budget,
        Func<LandingSpot, bool>? verify = null, int previous = 0)
    {
        // 所有测试地图至多 240x160；若游标不能有限耗尽，在固定轮数内明确失败而不是挂起。
        for (int round = 0; round < 10000; round++)
        {
            LandingSearchStep step = search.Advance(map, budget, verify);
            int advanced = step.Result.Candidates - previous;
            Check.True(advanced >= 0 && advanced <= budget, "Advance 不得超出本次候选预算");
            if (step.Complete)
                return step;
            Check.True(advanced > 0, "有效候选预算应推进搜索游标");
            Pending(step, step.Result.Candidates);
            previous = step.Result.Candidates;
        }
        throw new InvalidOperationException("小地图兜底搜索未在有限轮数内结束。");
    }

    private static void Pending(LandingSearchStep step, int candidates)
    {
        Check.False(step.Complete);
        Check.False(step.Result.Success);
        Check.Equal("", step.Result.Error);
        Check.Equal(default(LandingSpot), step.Result.Spot);
        Check.Equal(candidates, step.Result.Candidates);
    }

    private static void Success(LandingResult result, ILandingMap map, int center, int feet,
        BoundarySettings settings, BoundarySide side = BoundarySide.Left, float desired = 50 * 16f,
        int width = 20, int height = 42)
    {
        Fixtures.Success(result, center, feet, ExpectedCount(map, settings, side, desired, center, feet, height), width, height);
        Check.True(BoundaryPlanner.IsSafe(map, result.Spot, width, height, settings.HazardPadding));
    }

    private static int FullCount(ILandingMap map, BoundarySettings settings,
        BoundarySide side = BoundarySide.Left, int height = 42)
    {
        int start = side == BoundarySide.Left ? settings.LandingInset : map.Width - settings.LandingInset;
        int columns = Math.Abs(start - map.Width / 2) + 1;
        return columns * (map.Height - 42 - (42 + (height + 15) / 16) + 1);
    }

    private static int ExpectedCount(ILandingMap map, BoundarySettings settings, BoundarySide side,
        float desiredPixels, int center, int feet, int height)
    {
        int start = side == BoundarySide.Left ? settings.LandingInset : map.Width - settings.LandingInset;
        int min = 42 + (height + 15) / 16;
        int rows = map.Height - 42 - min + 1;
        int desired = (int)Math.Round(desiredPixels / 16d);
        int rank = Enumerable.Range(min, rows).OrderBy(y => Math.Abs((long)y - desired)).ThenBy(y => y)
            .Select((y, i) => (y, i)).Single(pair => pair.y == feet).i + 1;
        return Math.Abs(center - start) * rows + rank;
    }

    private static LandingResult Oracle(ILandingMap map, BoundarySettings settings, BoundarySide side,
        float desiredPixels, int width, int height)
    {
        int min = 42 + (height + 15) / 16;
        int desired = (int)Math.Round(desiredPixels / 16d);
        int start = side == BoundarySide.Left ? settings.LandingInset : map.Width - settings.LandingInset;
        int columns = Math.Abs(start - map.Width / 2) + 1;
        // 声明式独立排序，不复制被测双游标或跨帧推进逻辑。
        var candidates = Enumerable.Range(0, columns).SelectMany(dx =>
            Enumerable.Range(min, map.Height - 42 - min + 1).Select(y => new
            {
                Dx = dx, Feet = y, Center = start + (side == BoundarySide.Left ? dx : -dx)
            })).OrderBy(c => c.Dx).ThenBy(c => Math.Abs((long)c.Feet - desired)).ThenBy(c => c.Feet);
        int count = 0;
        foreach (var candidate in candidates)
        {
            count++;
            LandingSpot spot = Fixtures.Spot(candidate.Center, candidate.Feet, width, height);
            if (BoundaryPlanner.IsSafe(map, spot, width, height, settings.HazardPadding))
                return new(true, spot, count, "");
        }
        return new(false, default, count, "无安全落点");
    }

    private sealed class UnreadableMap : ILandingMap
    {
        public int Width => throw new InvalidOperationException("终态不应读取地图宽度");
        public int Height => throw new InvalidOperationException("终态不应读取地图高度");
        public TileCell Get(int x, int y) => throw new InvalidOperationException("终态不应读取图格");
    }

    private sealed class DimensionOnlyMap(int width, int height) : ILandingMap
    {
        public int Width { get; } = width;
        public int Height { get; } = height;
        public int Reads { get; private set; }
        public TileCell Get(int x, int y)
        {
            Reads++;
            throw new InvalidOperationException("无效尺寸不应读取图格");
        }
    }

    private sealed class DelayFirstReadMap(FrozenMap inner) : ILandingMap
    {
        private bool _first = true;
        public int Width => inner.Width;
        public int Height => inner.Height;
        public TileCell Get(int x, int y)
        {
            if (_first)
            {
                _first = false;
                Thread.Sleep(15); // 确保首次 128 候选检查时超时，不依赖机器处理速度。
            }
            return inner.Get(x, y);
        }
    }
}
