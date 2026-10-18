using BoundaryTransfer;

namespace BoundaryTransfer.Tests;

internal static class DirectLandingTests
{
    private static readonly BoundaryBounds Bounds = new(0, 800 * 16, 0, 600 * 16);

    public static void Add(List<AlgorithmCase> cases)
    {
        cases.Add("direct.left.preserves.feet", "direct", () =>
        {
            var r = BoundaryPlanner.Direct(BoundarySide.Left, 300 * 16f, 20, 42, Bounds);
            Check.True(r.Success);
            Check.Equal(640f, r.Spot.X, "进入左侧应贴原生左边界640像素");
            Check.Equal(300 * 16f - 42f, r.Spot.Y, "必须保持接触脚底Y");
            Check.Equal(300, r.Spot.FeetY);
        });

        cases.Add("direct.right.preserves.feet", "direct", () =>
        {
            var r = BoundaryPlanner.Direct(BoundarySide.Right, 300 * 16f, 20, 42, Bounds);
            Check.True(r.Success);
            Check.Equal(800 * 16f - 640f - 20f, r.Spot.X, "进入右侧应贴原生右边界640像素");
            Check.Equal(300 * 16f - 42f, r.Spot.Y, "必须保持接触脚底Y");
            Check.Equal(300, r.Spot.FeetY);
        });

        cases.Add("direct.multiple.heights", "direct", () =>
        {
            foreach (int feet in new[] { 43, 100, 300, 550, 560 })
            {
                var left = BoundaryPlanner.Direct(BoundarySide.Left, feet * 16f, 20, 42, Bounds);
                var right = BoundaryPlanner.Direct(BoundarySide.Right, feet * 16f, 20, 42, Bounds);
                Check.True(left.Success && right.Success);
                Check.Equal(feet * 16f - 42f, left.Spot.Y, $"左侧脚底{feet}格高度不得偏移");
                Check.Equal(feet * 16f - 42f, right.Spot.Y, $"右侧脚底{feet}格高度不得偏移");
                Check.Equal(feet, left.Spot.FeetY);
                Check.Equal(feet, right.Spot.FeetY);
            }
        });

        cases.Add("direct.no.tile.dependency", "direct", () =>
        {
            // 空世界也成功：不再寻找安全地面、不在水中/空中拒绝。
            var r = BoundaryPlanner.Direct(BoundarySide.Left, 222 * 16f, 20, 42, Bounds);
            Check.True(r.Success);
            Check.Equal(222, r.Spot.FeetY);
        });

        cases.Add("direct.clamp.only.outside.native.range", "direct", () =>
        {
            var top = BoundaryPlanner.Direct(BoundarySide.Left, 10f, 20, 42, Bounds);
            Check.True(top.Success);
            Check.Equal(640f, top.Spot.Y, "超出原生上边界时只夹到640像素");
            var bottom = BoundaryPlanner.Direct(BoundarySide.Left, 600 * 16f + 500f, 20, 42, Bounds);
            Check.True(bottom.Success);
            Check.Equal(600 * 16f - 640f - 42f, bottom.Spot.Y, "超出原生下边界时只夹到640像素");
        });

        cases.Add("direct.reject.invalid", "direct", () =>
        {
            Check.False(BoundaryPlanner.Direct(BoundarySide.Left, float.NaN, 20, 42, Bounds).Success);
            Check.False(BoundaryPlanner.Direct(BoundarySide.Left, 100, 0, 42, Bounds).Success);
            Check.False(BoundaryPlanner.Direct(BoundarySide.Left, 100, 20, 257, Bounds).Success);
            Check.False(BoundaryPlanner.Direct(BoundarySide.Left, 100, 20, 42, new(0, 100, 0, 100)).Success);
            Check.False(BoundaryPlanner.Direct(BoundarySide.Left, 100, 20, 42,
                new(float.NaN, 800 * 16, 0, 600 * 16)).Success);
        });

        cases.Add("direct.matches.native.boundary", "direct", () =>
        {
            var left = BoundaryPlanner.Direct(BoundarySide.Left, 200 * 16f, 20, 42, Bounds);
            var right = BoundaryPlanner.Direct(BoundarySide.Right, 200 * 16f, 20, 42, Bounds);
            Check.Equal(BoundarySide.Left, BoundaryContact.Side(left.Spot.X, 20, Bounds.Left, Bounds.Right, 0));
            Check.Equal(BoundarySide.Right, BoundaryContact.Side(right.Spot.X, 20, Bounds.Left, Bounds.Right, 0));
            Check.False(BoundaryContact.IsInterior(left.Spot.X, 20, Bounds.Left, Bounds.Right),
                "刚落到边界不算已回到内部，必须离开边界后才重新武装");
            Check.False(BoundaryContact.IsInterior(right.Spot.X, 20, Bounds.Left, Bounds.Right));
        });

        cases.Add("direct.silent.teleport.style", "direct", () =>
        {
            var field = typeof(BoundaryTransferPlugin).GetField("SilentTeleportStyle",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Check.Equal(6, (int)field!.GetRawConstantValue()!,
                "style=6 在 Terraria.TeleportEffect/UpdateTeleportVisuals 中无粒子、无传送杖特效");
        });

        cases.Add("direct.config.omits.legacy.search", "direct", () =>
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(new BoundaryTransferConfig());
            Check.False(json.Contains("上下安全搜索_格", StringComparison.Ordinal));
            Check.False(json.Contains("进入对侧内缩_格", StringComparison.Ordinal));
            Check.False(json.Contains("向地图内部搜索_格", StringComparison.Ordinal));
            Check.False(json.Contains("危险物缓冲_格", StringComparison.Ordinal));
            Check.False(json.Contains("单次搜索候选上限", StringComparison.Ordinal));
            Check.True(json.Contains("碰边容差_像素", StringComparison.Ordinal));
        });
    }
}
