namespace BoundaryTransfer;

internal enum BoundarySide { Left, Right }

internal readonly record struct TileCell(bool BlocksBody = false, bool SupportsFeet = false,
    bool Hazardous = false, bool Liquid = false);

internal interface ILandingMap
{
    int Width { get; }
    int Height { get; }
    TileCell Get(int x, int y);
}

/// <summary>像素坐标表示玩家碰撞框左上角；FeetY 表示脚底所在图格行。</summary>
internal readonly record struct LandingSpot(float X, float Y, int FeetY);
internal readonly record struct LandingResult(bool Success, LandingSpot Spot, int Candidates, string Error);

/// <summary>目标世界的原生可站立范围；边界与 Terraria Player.BordersMovement 的 640 像素限制一致。</summary>
internal readonly record struct BoundaryBounds(float Left, float Right, float Top, float Bottom);

internal static class BoundaryPlanner
{
    /// <summary>
    /// 直接对侧落点：保持玩家接触边界时的脚底 Y，水平放到目标世界对侧的原生边界线上。
    /// 不读取图格、不搜索安全地面、不改变高度；找不到地形也照常切换。
    /// </summary>
    public static LandingResult Direct(BoundarySide entrySide, float feetPixels, int playerWidth,
        int playerHeight, BoundaryBounds bounds)
    {
        const float edge = BoundaryContact.NativeEdgePaddingPixels;
        if (!float.IsFinite(feetPixels) || !float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Right)
            || !float.IsFinite(bounds.Top) || !float.IsFinite(bounds.Bottom)
            || playerWidth is < 1 or > 128 || playerHeight is < 1 or > 256)
            return new(false, default, 0, "玩家位置或碰撞框无效。");
        float minX = bounds.Left + edge;
        float maxX = bounds.Right - edge - playerWidth;
        float minY = bounds.Top + edge;
        float maxY = bounds.Bottom - edge - playerHeight;
        if (!(maxX > minX) || !(maxY > minY))
            return new(false, default, 0, "目标世界尺寸不足，无法容纳边界落点。");
        float x = entrySide == BoundarySide.Left ? minX : maxX;
        float y = Math.Clamp(feetPixels - playerHeight, minY, maxY);
        if (!float.IsFinite(x) || !float.IsFinite(y))
            return new(false, default, 0, "计算后的边界坐标无效。");
        int feet = (int)MathF.Round((y + playerHeight) / 16f);
        return new(true, new LandingSpot(x, y, feet), 0, "");
    }

    /// <summary>
    /// 海边专用：当前同高度点不安全（海水/无地面/墙体）时，沿对侧边界向内找地表海岸。
    /// 只在 worldSurface 附近、rockLayer 以上搜索，绝不选地下洞穴或地狱；找不到则本次不切换。
    /// </summary>
    public static LandingResult Coastal(ILandingMap map, BoundarySide entrySide, int playerWidth, int playerHeight,
        int searchInward, int searchVertical, int surfaceY, int rockLayerY, int hazardPadding, int maxCandidates)
    {
        if (playerWidth is < 1 or > 128 || playerHeight is < 1 or > 256
            || searchInward is < 0 or > 1000 || searchVertical is < 0 or > 200
            || hazardPadding is < 0 or > 3 || maxCandidates < 100)
            return new(false, default, 0, "海岸搜索参数无效。");
        int minFeet = 42 + (playerHeight + 15) / 16;
        int maxFeet = Math.Min(rockLayerY - 1, map.Height - 42);
        if (maxFeet < minFeet) return new(false, default, 0, "目标世界没有可用地表范围。");
        int center = Math.Clamp(surfaceY, minFeet, maxFeet);
        int margin = BoundaryContact.NativeEdgePaddingPixels / 16;
        int checkedCount = 0;
        for (int dx = 0; dx <= searchInward; dx++)
        {
            int centerTile = entrySide == BoundarySide.Left ? margin + dx : map.Width - margin - dx;
            if (entrySide == BoundarySide.Left ? centerTile > map.Width / 2 : centerTile < map.Width / 2) break;
            for (int delta = 0; delta <= searchVertical; delta++)
            {
                for (int direction = 0; direction < (delta == 0 ? 1 : 2); direction++)
                {
                    int feet = center + (direction == 0 ? -delta : delta);
                    if (feet < minFeet || feet > maxFeet) continue;
                    if (++checkedCount > maxCandidates)
                        return new(false, default, checkedCount - 1, "海岸搜索达到候选上限，本次不切换。");
                    float x = centerTile * 16f + 8 - playerWidth / 2f;
                    float y = feet * 16f - playerHeight;
                    var spot = new LandingSpot(x, y, feet);
                    if (IsSafe(map, spot, playerWidth, playerHeight, hazardPadding))
                        return new(true, spot, checkedCount, "");
                }
            }
        }
        return new(false, default, checkedCount, "目标海岸找不到干燥、无障碍的地表落点，本次不切换。");
    }

    public static string? Neighbor(BoundarySettings settings, string source, BoundarySide side)
    {
        int index = Array.FindIndex(settings.Worlds, n => n.Equals(source, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || settings.Worlds.Length < 2)
            return null;
        int target = index + (side == BoundarySide.Left ? -1 : 1);
        if (settings.Loop)
            target = (target + settings.Worlds.Length) % settings.Worlds.Length;
        return target < 0 || target >= settings.Worlds.Length ? null : settings.Worlds[target];
    }

    /// <summary>
    /// 1.0.x 的安全落点搜索，仅保留给历史算法回归测试；1.1.0 正式运行只使用 <see cref="Direct"/>，
    /// 不再调用本方法，也不做海空/地下兜底。
    /// </summary>
    public static LandingResult Find(ILandingMap map, BoundarySide entrySide, float desiredFeetPixels,
        int playerWidth, int playerHeight, BoundarySettings settings)
    {
        if (!float.IsFinite(desiredFeetPixels) || desiredFeetPixels < 0 || desiredFeetPixels > map.Height * 16f
            || playerWidth is < 1 or > 128 || playerHeight is < 1 or > 256)
            return new(false, default, 0, "玩家位置或碰撞框无效。");
        int desired = (int)MathF.Round(desiredFeetPixels / 16f);
        int margin = settings.LandingInset;
        if (map.Width <= 2 * (margin + 8) || map.Height < 100)
            return new(false, default, 0, "目标世界尺寸不足，不能容纳安全边界落点。");

        // 与 TShock 原生 Teleport 的 40 格边缘限制一致，且明确限制在同高度附近。
        int minY = 42 + (playerHeight + 15) / 16;
        int maxY = map.Height - 42;
        int checkedCount = 0;
        for (int delta = 0; delta <= settings.SearchVertical; delta++)
        {
            for (int direction = 0; direction < (delta == 0 ? 1 : 2); direction++)
            {
                int feet = desired + (direction == 0 ? -delta : delta);
                if (feet < minY || feet > maxY)
                    continue;
                for (int dx = 0; dx <= settings.SearchInward; dx++)
                {
                    int centerTile = entrySide == BoundarySide.Left ? margin + dx : map.Width - margin - dx;
                    // 永远留在进入侧半幅，避免搜索穿过整个地图或逼近另一条边界。
                    if (entrySide == BoundarySide.Left ? centerTile > map.Width / 2 : centerTile < map.Width / 2)
                        break;
                    if (++checkedCount > settings.MaxCandidates)
                        return new(false, default, checkedCount - 1, "安全落点搜索达到候选上限，本次不切换。");
                    float x = centerTile * 16f + 8 - playerWidth / 2f;
                    float y = feet * 16f - playerHeight;
                    var spot = new LandingSpot(x, y, feet);
                    if (IsSafe(map, spot, playerWidth, playerHeight, settings.HazardPadding))
                        return new(true, spot, checkedCount, "");
                }
            }
        }
        return new(false, default, checkedCount, "相同高度附近找不到干燥、无障碍且有安全地面的落点，本次不切换。");
    }

    public static bool IsSafe(ILandingMap map, LandingSpot spot, int width, int height, int hazardPadding)
    {
        if (!float.IsFinite(spot.X) || !float.IsFinite(spot.Y)
            || width is < 1 or > 128 || height is < 1 or > 256 || hazardPadding is < 0 or > 3
            || map.Width < 4 || map.Height < 4
            || spot.X < 0 || spot.Y < 0
            || (double)spot.X + width > map.Width * 16d
            || (double)spot.Y + height > map.Height * 16d)
            return false;
        // 先以double检查像素范围，再转int，避免极端有限float转整数及缓冲运算溢出。
        int left = (int)MathF.Floor(spot.X / 16f);
        int right = (int)MathF.Floor((spot.X + width - 0.01f) / 16f);
        int top = (int)MathF.Floor(spot.Y / 16f);
        int foot = (int)MathF.Round((spot.Y + height) / 16f);
        if (left - hazardPadding < 1 || right + hazardPadding >= map.Width - 1 || top - hazardPadding < 1 || foot + hazardPadding >= map.Height - 1)
            return false;

        // 必须整个脚掌下都有平整承重面，不把悬空、斜坡尖角或单个方块边缘当着陆点。
        for (int x = left; x <= right; x++)
        {
            TileCell floor = map.Get(x, foot);
            if (!floor.SupportsFeet || floor.Hazardous || floor.Liquid)
                return false;
        }
        for (int x = left - hazardPadding; x <= right + hazardPadding; x++)
        {
            for (int y = top - hazardPadding; y <= foot + hazardPadding; y++)
            {
                TileCell tile = map.Get(x, y);
                if (tile.Hazardous || tile.Liquid)
                    return false;
                if (x >= left && x <= right && y >= top && y < foot && tile.BlocksBody)
                    return false;
            }
        }
        return true;
    }
}
