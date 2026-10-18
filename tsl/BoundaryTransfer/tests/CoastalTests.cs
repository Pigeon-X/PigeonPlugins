namespace BoundaryTransfer.Tests;

internal static class CoastalTests
{
    public static void Add(List<AlgorithmCase> cases)
    {
        cases.Add("coastal.ocean.to.dry.coast", "coastal", () =>
        {
            var map = new MapBuilder(1000, 1200)
                .Floor(0, 999, 220)
                .Rect(0, 200, 200, 219, Tiles.Water)
                .Freeze();
            var result = BoundaryPlanner.Coastal(map, BoundarySide.Left, 20, 42,
                400, 40, surfaceY: 200, rockLayerY: 600, hazardPadding: 1, maxCandidates: 60000);
            Check.True(result.Success, result.Error);
            Check.Equal(203, Fixtures.CenterTile(result.Spot), "海岸应从边界向内找最近的安全干燥列（含1格危险缓冲）");
            Check.Equal(220, result.Spot.FeetY, "海岸应落在地表附近");
            Check.True(!map.Get((int)(result.Spot.X / 16f), result.Spot.FeetY).Liquid, "海岸落点不能是液体");
        });

        cases.Add("coastal.never.underground.or.hell", "coastal", () =>
        {
            var map = new MapBuilder(1000, 1200)
                .Floor(0, 999, 220)
                .Floor(0, 999, 800)
                .Rect(0, 300, 200, 219, Tiles.Water)
                .Freeze();
            var result = BoundaryPlanner.Coastal(map, BoundarySide.Left, 20, 42,
                400, 40, surfaceY: 200, rockLayerY: 600, hazardPadding: 1, maxCandidates: 60000);
            Check.True(result.Success, result.Error);
            Check.True(result.Spot.FeetY <= 240, "只允许在worldSurface附近找海岸，不能去地下/地狱");
        });

        cases.Add("coastal.no.dry.coast.fails", "coastal", () =>
        {
            var map = new MapBuilder(1000, 1200)
                .Floor(0, 999, 220)
                .Rect(0, 999, 200, 219, Tiles.Water)
                .Freeze();
            var result = BoundaryPlanner.Coastal(map, BoundarySide.Left, 20, 42,
                400, 40, surfaceY: 200, rockLayerY: 600, hazardPadding: 1, maxCandidates: 60000);
            Check.False(result.Success);
            Check.True(result.Error.Contains("海岸", StringComparison.Ordinal));
        });
    }
}
