namespace BoundaryTransfer.Tests;

internal static class LandingTests
{
    public static void Add(List<AlgorithmCase> cases)
    {
        AddSafety(cases);
        AddSearch(cases);
        AddProperties(cases);
    }

    private static void AddSafety(List<AlgorithmCase> cases)
    {
        cases.Add("safe.flat.full-body", "body-and-floor", () =>
        {
            var map = new MapBuilder().FloorAt(96, 200).Freeze();
            Check.True(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 1));
            Check.True(map.Reads > 0);
        });
        cases.Add("safe.platform.floor", "body-and-floor", () =>
        {
            var map = new MapBuilder().Floor(94, 98, 200, Tiles.Platform).Freeze();
            Check.True(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 1));
        });
        cases.Add("safe.platform.body-not-blocking", "body-and-floor", () =>
        {
            var map = new MapBuilder().FloorAt(96, 200).Put(96, 199, Tiles.Platform).Freeze();
            Check.True(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 1));
        });
        cases.Add("safe.air-never-supports", "body-and-floor", () =>
        {
            var map = new MapBuilder().Freeze();
            Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 1));
        });
        cases.Add("safe.one-block-floor.insufficient", "body-and-floor", () =>
        {
            var map = new MapBuilder().Put(96, 200, Tiles.Solid).Freeze();
            Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 1));
        });
        for (int missing = 95; missing <= 97; missing++)
        {
            int x = missing;
            cases.Add($"safe.floor-gap.column-{x}", "body-and-floor", () =>
            {
                var map = new MapBuilder().FloorAt(96, 200).Put(x, 200, Tiles.Air).Freeze();
                Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 1));
            });
        }
        cases.Add("safe.floor-no-side-extension-required", "body-and-floor", () =>
        {
            var map = new MapBuilder().FloorAt(96, 200).Freeze();
            Check.True(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 3));
        });
        cases.Add("safe.body.entire-bounding-box", "body-and-floor", () =>
        {
            for (int x = 95; x <= 97; x++)
                for (int y = 197; y < 200; y++)
                {
                    var map = new MapBuilder().FloorAt(96, 200).Put(x, y, Tiles.BodyObstacle).Freeze();
                    Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 1), $"应拒绝身体障碍 ({x},{y})");
                }
        });
        cases.Add("safe.solid-neighbor-not-hazard", "body-and-floor", () =>
        {
            var map = new MapBuilder().FloorAt(96, 200).Put(94, 199, Tiles.Solid).Put(98, 199, Tiles.Solid).Put(96, 196, Tiles.Solid).Freeze();
            Check.True(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 1));
        });
        cases.Add("safe.body.right-edge.exact-exclusive", "body-and-floor", () =>
        {
            var map = new MapBuilder().Put(96, 200, Tiles.Solid).Put(97, 199, Tiles.BodyObstacle).Freeze();
            var aligned = new LandingSpot(96 * 16, 200 * 16 - 32, 200);
            Check.True(BoundaryPlanner.IsSafe(map, aligned, 16, 32, 0));
        });
        cases.Add("safe.body.right-edge.partial-overlap", "body-and-floor", () =>
        {
            var map = new MapBuilder().Floor(96, 97, 200).Put(97, 199, Tiles.BodyObstacle).Freeze();
            var partial = new LandingSpot(96 * 16 + 0.5f, 200 * 16 - 32, 200);
            Check.False(BoundaryPlanner.IsSafe(map, partial, 16, 32, 0));
        });
        cases.Add("safe.body.top-edge.partial-row", "body-and-floor", () =>
        {
            var map = new MapBuilder().FloorAt(96, 200).Put(96, 197, Tiles.BodyObstacle).Freeze();
            Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200, height: 33), 20, 33, 0));
        });
        cases.Add("safe.width-height.extremes", "body-and-floor", () =>
        {
            foreach (int width in new[] { 1, 16, 20, 31, 32, 48, 128 })
                foreach (int height in new[] { 1, 16, 32, 42, 256 })
                {
                    var map = new MapBuilder().FloorAt(96, 200, width).Freeze();
                    Check.True(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200, width, height), width, height, 1));
                }
        });
        cases.Add("safe.invalid-dimensions.no-reads", "input-validation", () =>
        {
            var map = new MapBuilder().Freeze();
            foreach (var (w, h) in new[] { (0, 42), (-1, 42), (20, 0), (20, -1) })
                Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), w, h, 1));
            Check.Equal(0L, map.Reads);
        });
        cases.Add("safe.non-finite-coordinates.no-reads", "input-validation", () =>
        {
            var map = new MapBuilder().Freeze();
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Check.False(BoundaryPlanner.IsSafe(map, new(value, 100, 200), 20, 42, 1));
                Check.False(BoundaryPlanner.IsSafe(map, new(100, value, 200), 20, 42, 1));
            }
            Check.Equal(0L, map.Reads);
        });
        cases.Add("safe.extreme-finite-coordinates.rejected", "input-validation", () =>
        {
            var map = new MapBuilder().Freeze();
            foreach (float value in new[] { float.MaxValue, -float.MaxValue, 1e30f, -1e30f })
            {
                Check.False(BoundaryPlanner.IsSafe(map, new(value, 200 * 16 - 42, 200), 20, 42, 1));
                Check.False(BoundaryPlanner.IsSafe(map, new(96 * 16, value, 200), 20, 42, 1));
            }
            Check.Equal(0L, map.Reads);
        });
        cases.Add("safe.map-bounds-and-padding", "input-validation", () =>
        {
            var map = new MapBuilder().FloorAt(2, 200).FloorAt(996, 200).FloorAt(96, 1197).FloorAt(96, 3).Freeze();
            Check.True(BoundaryPlanner.IsSafe(map, Fixtures.Spot(2, 200), 20, 42, 0));
            Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(2, 200), 20, 42, 1));
            Check.True(BoundaryPlanner.IsSafe(map, Fixtures.Spot(996, 200), 20, 42, 1));
            Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(996, 200), 20, 42, 2));
            Check.True(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 1197), 20, 42, 1));
            Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 1197), 20, 42, 2));
            Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 3), 20, 42, 0));
        });

        foreach (var (id, floor) in new[] { ("half-brick", Tiles.HalfBrick), ("slope", Tiles.Slope), ("inactive", Tiles.Inactive), ("decoration", Tiles.Decorative), ("unknown", Tiles.Unknown), ("hazard-floor", Tiles.HazardousFloor), ("wet-floor", Tiles.WetSolid) })
            cases.Add($"safe.floor.reject-{id}", "abstract-tiles", () =>
            {
                var map = new MapBuilder().Floor(95, 97, 200, floor).Freeze();
                Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 1));
            });
        foreach (var (name, liquid) in new[] { ("water", Tiles.Water), ("lava", Tiles.Lava), ("honey", Tiles.Honey), ("shimmer", Tiles.Shimmer) })
            cases.Add($"safe.liquid.{name}.body-floor-neighbor", "abstract-tiles", () =>
            {
                foreach (var (x, y) in new[] { (96, 199), (94, 199), (96, 196), (98, 201) })
                {
                    var map = new MapBuilder().FloorAt(96, 200).Put(x, y, liquid).Freeze();
                    Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 1));
                }
                var wetFloor = new MapBuilder().FloorAt(96, 200).Put(96, 200, liquid with { SupportsFeet = true }).Freeze();
                Check.False(BoundaryPlanner.IsSafe(wetFloor, Fixtures.Spot(96, 200), 20, 42, 0));
            });
        cases.Add("safe.hazard.neighbor-buffer.inclusive", "hazard-buffer", () =>
        {
            foreach (var (x, y) in new[] { (94, 196), (98, 196), (94, 201), (98, 201), (96, 196), (94, 199) })
            {
                var map = new MapBuilder().FloorAt(96, 200).Put(x, y, Tiles.Hazard).Freeze();
                Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 1));
                Check.True(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 0));
            }
        });
        cases.Add("safe.hazard.outside-buffer.allowed", "hazard-buffer", () =>
        {
            var map = new MapBuilder().FloorAt(96, 200).Put(93, 199, Tiles.Hazard).Put(99, 199, Tiles.Water).Put(96, 195, Tiles.Hazard).Put(96, 202, Tiles.Lava).Freeze();
            Check.True(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 1));
            Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 2));
        });
        cases.Add("safe.padding.maximum-three", "hazard-buffer", () =>
        {
            var map = new MapBuilder().FloorAt(96, 200).Put(92, 199, Tiles.Hazard).Freeze();
            Check.True(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 2));
            Check.False(BoundaryPlanner.IsSafe(map, Fixtures.Spot(96, 200), 20, 42, 3));
        });
    }

    private static void AddSearch(List<AlgorithmCase> cases)
    {
        cases.Add("find.entry.left-exact", "entry-side", () =>
        {
            var map = new MapBuilder().FloorAt(96, 200).Freeze(); Fixtures.Success(Fixtures.Find(map), 96, 200, 1);
        });
        cases.Add("find.entry.right-exact", "entry-side", () =>
        {
            var map = new MapBuilder().FloorAt(904, 200).Freeze(); Fixtures.Success(Fixtures.Find(map, side: BoundarySide.Right), 904, 200, 1);
        });
        cases.Add("find.exit-left.enters-right-and-exit-right.enters-left", "entry-side", () =>
        {
            var settings = Fixtures.Settings(); var map = new MapBuilder().FloorAt(96, 200).FloorAt(904, 200).Freeze();
            foreach (BoundarySide exit in Enum.GetValues<BoundarySide>())
            {
                // 插件 source 中 Opposite(exit) 的纯算法契约；不调用网络或插件生命周期。
                BoundarySide entry = exit == BoundarySide.Left ? BoundarySide.Right : BoundarySide.Left;
                Check.Equal(exit == BoundarySide.Left ? "B" : "B", BoundaryPlanner.Neighbor(settings with { Loop = true }, "A", exit));
                var result = Fixtures.Find(map, settings, entry);
                Fixtures.Success(result, exit == BoundarySide.Left ? 904 : 96, 200, 1);
                Check.True(BoundaryContact.IsInterior(result.Spot.X, 20, 0, map.Width * 16f));
            }
        });
        cases.Add("find.horizontal.left-inward-order", "search-order", () =>
        {
            var map = new MapBuilder().FloorAt(102, 200).Freeze(); Fixtures.Success(Fixtures.Find(map), 102, 200, 7);
        });
        cases.Add("find.horizontal.right-inward-order", "search-order", () =>
        {
            var map = new MapBuilder().FloorAt(898, 200).Freeze(); Fixtures.Success(Fixtures.Find(map, side: BoundarySide.Right), 898, 200, 7);
        });
        cases.Add("find.same-y.before-nearer-offset-y", "search-order", () =>
        {
            var map = new MapBuilder().FloorAt(104, 200).FloorAt(96, 190).Freeze();
            Fixtures.Success(Fixtures.Find(map, Fixtures.Settings(vertical: 10)), 104, 200, 9);
        });
        cases.Add("find.same-y.before-lower-offset-y", "search-order", () =>
        {
            var map = new MapBuilder().FloorAt(104, 200).FloorAt(96, 210).Freeze();
            Fixtures.Success(Fixtures.Find(map, Fixtures.Settings(vertical: 10)), 104, 200, 9);
        });
        cases.Add("find.vertical.up-only", "search-order", () =>
        {
            var map = new MapBuilder().FloorAt(96, 198).Freeze();
            Fixtures.Success(Fixtures.Find(map, Fixtures.Settings(inward: 0)), 96, 198, 4);
        });
        cases.Add("find.vertical.down-only", "search-order", () =>
        {
            var map = new MapBuilder().FloorAt(96, 202).Freeze();
            Fixtures.Success(Fixtures.Find(map, Fixtures.Settings(inward: 0)), 96, 202, 5);
        });
        cases.Add("find.vertical.equal-distance.up-before-down", "search-order", () =>
        {
            // 两个平台水平分离，避免上平台阻挡下平台的身体包围盒。
            var map = new MapBuilder().FloorAt(104, 198).FloorAt(96, 202).Freeze();
            Fixtures.Success(Fixtures.Find(map), 104, 198, 36);
        });
        cases.Add("find.vertical.absolute-distance-first", "search-order", () =>
        {
            var map = new MapBuilder().FloorAt(104, 201).FloorAt(96, 196).Freeze();
            Fixtures.Success(Fixtures.Find(map), 104, 201, 27);
        });
        cases.Add("find.hell.same-y-not-surface", "height-preservation", () =>
        {
            var map = new MapBuilder().Floor(90, 120, 200).FloorAt(100, 1100).Freeze();
            Fixtures.Success(Fixtures.Find(map, feet: 1100 * 16f), 100, 1100, 5);
        });
        cases.Add("find.underground.no-surface-fallback", "height-preservation", () =>
        {
            var map = new MapBuilder().Floor(90, 120, 200).Freeze();
            Fixtures.Failure(Fixtures.Find(map, feet: 800 * 16f), 81);
        });
        cases.Add("find.no-fallback-from-above-world", "height-preservation", () =>
        {
            var map = new MapBuilder().FloorAt(96, 200).Freeze();
            Fixtures.Failure(Fixtures.Find(map, feet: -200 * 16f), 0); Check.Equal(0L, map.Reads);
        });
        cases.Add("find.no-fallback-from-below-world", "height-preservation", () =>
        {
            var map = new MapBuilder().FloorAt(96, 200).Freeze();
            Fixtures.Failure(Fixtures.Find(map, feet: 2000 * 16f), 0); Check.Equal(0L, map.Reads);
        });
        cases.Add("find.feet.round-nearest-even", "height-preservation", () =>
        {
            foreach (var (pixels, expected) in new[] { (200 * 16f + 7, 200), (200 * 16f + 8, 200), (200 * 16f + 9, 201), (201 * 16f + 8, 202) })
            {
                var map = new MapBuilder().FloorAt(96, expected).Freeze();
                Fixtures.Success(Fixtures.Find(map, feet: pixels), 96, expected, 1);
            }
        });
        cases.Add("find.liquid-entire-map.reject", "abstract-tiles", () =>
        {
            var map = new MapBuilder(defaultCell: Tiles.Water).Floor(90, 120, 200).Freeze();
            Fixtures.Failure(Fixtures.Find(map), 81);
        });
        cases.Add("find.hazard-entire-map.reject", "abstract-tiles", () =>
        {
            var map = new MapBuilder(defaultCell: Tiles.Hazard).Floor(90, 120, 200).Freeze();
            Fixtures.Failure(Fixtures.Find(map), 81);
        });
        cases.Add("find.unknown-entire-map.reject", "abstract-tiles", () =>
        {
            var map = new MapBuilder(defaultCell: Tiles.Unknown).Freeze(); Fixtures.Failure(Fixtures.Find(map), 81);
        });
        cases.Add("find.dangerous-floor.skip-for-safe-floor", "abstract-tiles", () =>
        {
            var map = new MapBuilder().Floor(95, 99, 200, Tiles.HazardousFloor).FloorAt(104, 200).Freeze();
            Fixtures.Success(Fixtures.Find(map), 104, 200, 9);
        });
        cases.Add("find.half-brick-and-slope.skip", "abstract-tiles", () =>
        {
            var map = new MapBuilder().Floor(95, 97, 200, Tiles.HalfBrick).Floor(98, 100, 200, Tiles.Slope).FloorAt(104, 200).Freeze();
            Fixtures.Success(Fixtures.Find(map), 104, 200, 9);
        });
        cases.Add("find.search-inward.inclusive-limit", "search-limits", () =>
        {
            var map = new MapBuilder().FloorAt(104, 200).Freeze();
            Fixtures.Success(Fixtures.Find(map, Fixtures.Settings(vertical: 0)), 104, 200, 9);
            Fixtures.Failure(Fixtures.Find(map, Fixtures.Settings(inward: 7, vertical: 0)), 8);
        });
        cases.Add("find.search-vertical.inclusive-limit", "search-limits", () =>
        {
            var map = new MapBuilder().FloorAt(96, 196).Freeze();
            Fixtures.Success(Fixtures.Find(map, Fixtures.Settings(inward: 0, vertical: 4)), 96, 196, 8);
            Fixtures.Failure(Fixtures.Find(map, Fixtures.Settings(inward: 0, vertical: 3)), 7);
        });
        cases.Add("find.zero-search.single-candidate", "search-limits", () =>
        {
            var map = new MapBuilder().Freeze(); Fixtures.Failure(Fixtures.Find(map, Fixtures.Settings(inward: 0, vertical: 0)), 1);
        });
        cases.Add("find.budget.stops-at-exact-limit", "search-limits", () =>
        {
            var map = new MapBuilder().Freeze(); var settings = Fixtures.Settings(inward: 20, vertical: 20, budget: 100);
            Fixtures.Failure(Fixtures.Find(map, settings), 100, "候选上限"); Check.Equal(100L, map.Reads);
        });
        cases.Add("find.budget.last-allowed-can-succeed", "search-limits", () =>
        {
            var map = new MapBuilder().FloorAt(195, 200).Freeze();
            Fixtures.Success(Fixtures.Find(map, Fixtures.Settings(inward: 99, vertical: 0, budget: 100)), 195, 200, 100);
        });
        cases.Add("find.budget.next-candidate-not-read", "search-limits", () =>
        {
            var map = new MapBuilder().FloorAt(196, 200).Freeze();
            Fixtures.Failure(Fixtures.Find(map, Fixtures.Settings(inward: 100, vertical: 0, budget: 100)), 100, "候选上限");
            Check.False(map.Visited.Contains((197, 200)), "不应读取第101个候选的最右脚掌");
        });
        cases.Add("find.half-world.left-search-cap", "search-limits", () =>
        {
            var map = new MapBuilder(width: 400).FloorAt(204, 200).Freeze();
            Fixtures.Failure(Fixtures.Find(map, Fixtures.Settings(inward: 1000, vertical: 0)), 105);
            Check.True(map.Visited.All(p => p.X <= 201));
        });
        cases.Add("find.half-world.right-search-cap", "search-limits", () =>
        {
            var map = new MapBuilder(width: 400).FloorAt(196, 200).Freeze();
            Fixtures.Failure(Fixtures.Find(map, Fixtures.Settings(inward: 1000, vertical: 0), BoundarySide.Right), 105);
            Check.True(map.Visited.All(p => p.X >= 199));
        });
        cases.Add("find.world-size-validation.no-reads", "input-validation", () =>
        {
            foreach (var (w, h) in new[] { (208, 1200), (100, 1200), (1000, 99), (0, 0) })
            {
                var map = new MapBuilder(w, h).Freeze(); Fixtures.Failure(Fixtures.Find(map), 0); Check.Equal(0L, map.Reads);
            }
        });
        cases.Add("find.world-size.exact-minimum-plus-one", "input-validation", () =>
        {
            var map = new MapBuilder(width: 209).FloorAt(96, 200).Freeze(); Fixtures.Success(Fixtures.Find(map), 96, 200, 1);
        });
        cases.Add("find.non-finite-feet.no-reads", "input-validation", () =>
        {
            var map = new MapBuilder().Freeze();
            foreach (float feet in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Fixtures.Failure(Fixtures.Find(map, feet: feet), 0, "无效");
            Check.Equal(0L, map.Reads);
        });
        cases.Add("find.extreme-finite-feet.no-reads", "input-validation", () =>
        {
            var map = new MapBuilder().Freeze();
            foreach (float feet in new[] { float.MaxValue, -float.MaxValue, 1e30f, -1e30f })
                Fixtures.Failure(Fixtures.Find(map, feet: feet), 0);
            Check.Equal(0L, map.Reads);
        });
        cases.Add("find.invalid-player-box.no-reads", "input-validation", () =>
        {
            var map = new MapBuilder().Freeze();
            foreach (var (w, h) in new[] { (0, 42), (-1, 42), (129, 42), (20, 0), (20, -1), (20, 257) })
                Fixtures.Failure(Fixtures.Find(map, width: w, height: h), 0, "无效");
            Check.Equal(0L, map.Reads);
        });
        cases.Add("find.player-box.maximum-supported", "input-validation", () =>
        {
            var map = new MapBuilder().FloorAt(96, 200, 128).Freeze(); Fixtures.Success(Fixtures.Find(map, width: 128, height: 256), 96, 200, 1, 128, 256);
        });
        cases.Add("find.native-top-clearance", "search-limits", () =>
        {
            const int minFeet = 45;
            var map = new MapBuilder().FloorAt(96, minFeet).Freeze();
            Fixtures.Success(Fixtures.Find(map, Fixtures.Settings(inward: 0, vertical: 0), feet: minFeet * 16), 96, minFeet, 1);
            Fixtures.Failure(Fixtures.Find(map, Fixtures.Settings(inward: 0, vertical: 0), feet: (minFeet - 1) * 16), 0);
        });
        cases.Add("find.native-bottom-clearance", "search-limits", () =>
        {
            const int maxFeet = 1200 - 42;
            var map = new MapBuilder().FloorAt(96, maxFeet).Freeze();
            Fixtures.Success(Fixtures.Find(map, Fixtures.Settings(inward: 0, vertical: 0), feet: maxFeet * 16), 96, maxFeet, 1);
            Fixtures.Failure(Fixtures.Find(map, Fixtures.Settings(inward: 0, vertical: 0), feet: (maxFeet + 1) * 16), 0);
        });
        cases.Add("find.outside-clearance.can-search-back-within-range", "search-limits", () =>
        {
            var top = new MapBuilder().FloorAt(96, 45).Freeze();
            Fixtures.Success(Fixtures.Find(top, Fixtures.Settings(inward: 0, vertical: 1), feet: 44 * 16), 96, 45, 1);
            var bottom = new MapBuilder().FloorAt(96, 1158).Freeze();
            Fixtures.Success(Fixtures.Find(bottom, Fixtures.Settings(inward: 0, vertical: 1), feet: 1159 * 16), 96, 1158, 1);
        });
    }

    private static void AddProperties(List<AlgorithmCase> cases)
    {
        cases.Add("property.deterministic-repeat-and-no-map-mutation", "properties", () =>
        {
            var map = new MapBuilder().FloorAt(100, 198).Put(95, 200, Tiles.Lava).Put(101, 201, Tiles.Hazard).Freeze();
            string before = map.Snapshot(); var first = Fixtures.Find(map);
            for (int i = 0; i < 25; i++) Check.Equal(first, Fixtures.Find(map));
            Check.Equal(before, map.Snapshot());
        });
        cases.Add("property.same-height.across-world-heights", "properties", () =>
        {
            foreach (int mapHeight in new[] { 600, 1200, 2400 })
                foreach (int desired in new[] { 100, mapHeight / 2, mapHeight - 60 })
                    foreach (BoundarySide side in Enum.GetValues<BoundarySide>())
                    {
                        int center = side == BoundarySide.Left ? 96 : 904;
                        var map = new MapBuilder(height: mapHeight).FloorAt(center, desired).Freeze();
                        Fixtures.Success(Fixtures.Find(map, side: side, feet: desired * 16), center, desired, 1);
                    }
        });
        cases.Add("property.fixed-seed.vertical-horizontal-oracle", "properties", () =>
        {
            var rng = new Random(314159);
            for (int i = 0; i < 80; i++)
            {
                int feet = rng.Next(100, 1050), offset = rng.Next(-8, 9), dx = rng.Next(0, 13);
                BoundarySide side = i % 2 == 0 ? BoundarySide.Left : BoundarySide.Right;
                int center = side == BoundarySide.Left ? 96 + dx : 904 - dx;
                var map = new MapBuilder().FloorAt(center, feet + offset).Freeze();
                string snapshot = map.Snapshot(); var settings = Fixtures.Settings(inward: 12, vertical: 8);
                var actual = Fixtures.Find(map, settings, side, feet * 16);
                var expected = Oracle(map, settings, side, feet, 20, 42);
                Check.Equal(expected, actual, $"seed=314159 index={i}"); Check.Equal(snapshot, map.Snapshot());
                Check.True(actual.Success); Check.True(Math.Abs(actual.Spot.FeetY - feet) <= settings.SearchVertical);
                Check.True(BoundaryPlanner.IsSafe(map, actual.Spot, 20, 42, 1));
            }
        });
        cases.Add("property.fixed-seed.mixed-map-exhaustive-oracle", "properties", () =>
        {
            var rng = new Random(314159);
            for (int i = 0; i < 40; i++)
            {
                BoundarySide side = i % 2 == 0 ? BoundarySide.Left : BoundarySide.Right;
                var b = new MapBuilder();
                for (int n = 0; n < 35; n++)
                {
                    int center = side == BoundarySide.Left ? 96 + rng.Next(0, 7) : 904 - rng.Next(0, 7);
                    int y = 200 + rng.Next(-3, 4); int kind = rng.Next(0, 7);
                    if (kind == 0) b.FloorAt(center, y);
                    else b.Put(center, y, kind switch { 1 => Tiles.Hazard, 2 => Tiles.Water, 3 => Tiles.HalfBrick, 4 => Tiles.Platform, 5 => Tiles.BodyObstacle, _ => Tiles.Air });
                }
                var map = b.Freeze(); var settings = Fixtures.Settings(inward: 6, vertical: 3, budget: 100);
                string before = map.Snapshot();
                Check.Equal(Oracle(map, settings, side, 200, 20, 42), Fixtures.Find(map, settings, side));
                Check.Equal(before, map.Snapshot());
            }
        });
    }

    private static LandingResult Oracle(FrozenMap map, BoundarySettings settings, BoundarySide side, int desired, int width, int height)
    {
        // 独立声明式枚举并排序，避免复制被测 Find 的三重循环与候选递增逻辑。
        var candidates = Enumerable.Range(Math.Max(42 + (height + 15) / 16, desired - settings.SearchVertical),
                Math.Max(0, Math.Min(map.Height - 42, desired + settings.SearchVertical) - Math.Max(42 + (height + 15) / 16, desired - settings.SearchVertical) + 1))
            .SelectMany(y => Enumerable.Range(0, settings.SearchInward + 1).Select(dx => new { Feet = y, Dx = dx, Center = side == BoundarySide.Left ? settings.LandingInset + dx : map.Width - settings.LandingInset - dx }))
            .Where(c => side == BoundarySide.Left ? c.Center <= map.Width / 2 : c.Center >= map.Width / 2)
            .OrderBy(c => Math.Abs(c.Feet - desired)).ThenBy(c => c.Feet).ThenBy(c => c.Dx).ToArray();
        for (int i = 0; i < Math.Min(settings.MaxCandidates, candidates.Length); i++)
        {
            var c = candidates[i]; var spot = Fixtures.Spot(c.Center, c.Feet, width, height);
            if (BoundaryPlanner.IsSafe(map, spot, width, height, settings.HazardPadding)) return new(true, spot, i + 1, "");
        }
        return new(false, default, Math.Min(settings.MaxCandidates, candidates.Length), candidates.Length > settings.MaxCandidates ? "安全落点搜索达到候选上限，本次不切换。" : "相同高度附近找不到干燥、无障碍且有安全地面的落点，本次不切换。");
    }
}
