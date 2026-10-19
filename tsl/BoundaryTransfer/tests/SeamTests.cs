namespace BoundaryTransfer.Tests;

internal static class SeamTests
{
    public static void Add(List<AlgorithmCase> cases)
    {
        cases.Add("seam.land.edge.same.height", "seam", () =>
        {
            var source = new MapBuilder(1000, 1200).Floor(0, 999, 200).Freeze();
            var target = new MapBuilder(1000, 1200).Floor(0, 999, 200).Freeze();
            var result = BoundaryPlanner.Seam(source, target, BoundarySide.Right,
                sourceTileX: 800, sourceFeetY: 200, playerWidth: 20, playerHeight: 42,
                targetEdge: 40, sliceDelta: 0, margin: 24, allowNudge: true,
                sourceSurfaceY: 200, sourceRockLayerY: 600, targetSurfaceY: 200, targetRockLayerY: 600, surfaceBand: 80);
            Check.True(result.Success, result.Error);
            Check.Equal(64, Fixtures.CenterTile(result.Spot), "东出应落到目标西侧陆地边缘+余量");
            Check.Equal(200, result.Spot.FeetY);
        });

        cases.Add("seam.slice.delta.preserves.relative.x", "seam", () =>
        {
            var source = new MapBuilder(1000, 1200).Floor(0, 999, 200).Freeze();
            var target = new MapBuilder(1000, 1200).Floor(0, 999, 210).Freeze();
            var result = BoundaryPlanner.Seam(source, target, BoundarySide.Right,
                sourceTileX: 800, sourceFeetY: 200, playerWidth: 20, playerHeight: 42,
                targetEdge: 40, sliceDelta: 100, margin: 24, allowNudge: true,
                sourceSurfaceY: 200, sourceRockLayerY: 600, targetSurfaceY: 200, targetRockLayerY: 600, surfaceBand: 80);
            Check.True(result.Success, result.Error);
            Check.Equal(700, Fixtures.CenterTile(result.Spot), "切片间距应保持相对X平移");
            Check.Equal(210, result.Spot.FeetY, "应按两侧地表高差修正脚底");
        });

        cases.Add("seam.west.exit.uses.east.edge", "seam", () =>
        {
            var source = new MapBuilder(1000, 1200).Floor(0, 999, 200).Freeze();
            var target = new MapBuilder(1000, 1200).Floor(0, 999, 200).Freeze();
            var result = BoundaryPlanner.Seam(source, target, BoundarySide.Left,
                sourceTileX: 200, sourceFeetY: 200, playerWidth: 20, playerHeight: 42,
                targetEdge: 900, sliceDelta: 0, margin: 24, allowNudge: true,
                sourceSurfaceY: 200, sourceRockLayerY: 600, targetSurfaceY: 200, targetRockLayerY: 600, surfaceBand: 80);
            Check.True(result.Success, result.Error);
            Check.Equal(874, Fixtures.CenterTile(result.Spot), "西出应落到目标东侧陆地边缘内侧并扣玩家宽度");
            Check.Equal(200, result.Spot.FeetY);
        });

        cases.Add("seam.rejects.underground.cave", "seam", () =>
        {
            var source = new MapBuilder(1000, 1200).Floor(0, 999, 200).Freeze();
            var target = new MapBuilder(1000, 1200).Floor(0, 999, 500).Freeze();
            var result = BoundaryPlanner.Seam(source, target, BoundarySide.Right,
                sourceTileX: 800, sourceFeetY: 200, playerWidth: 20, playerHeight: 42,
                targetEdge: 40, sliceDelta: 0, margin: 24, allowNudge: true,
                sourceSurfaceY: 200, sourceRockLayerY: 600,
                targetSurfaceY: 200, targetRockLayerY: 600, surfaceBand: 80);
            Check.False(result.Success, "接缝不能把地下洞穴当海岸");
        });

        cases.Add("seam.no.ground.fails", "seam", () =>
        {
            var source = new MapBuilder(1000, 1200).Floor(0, 999, 200).Freeze();
            var target = new MapBuilder(1000, 1200).Freeze();
            var result = BoundaryPlanner.Seam(source, target, BoundarySide.Right,
                sourceTileX: 800, sourceFeetY: 200, playerWidth: 20, playerHeight: 42,
                targetEdge: 40, sliceDelta: 0, margin: 24, allowNudge: true,
                sourceSurfaceY: 200, sourceRockLayerY: 600, targetSurfaceY: 200, targetRockLayerY: 600, surfaceBand: 80);
            Check.False(result.Success);
        });
    }
}
