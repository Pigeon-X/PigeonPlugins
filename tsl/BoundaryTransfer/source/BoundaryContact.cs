namespace BoundaryTransfer;

/// <summary>
/// 对照 Terraria 1.4.5.8 Player.Update 的原生左右 clamp：
/// position.X >= Main.leftWorld + 640，position.X + width <= Main.rightWorld - 640。
/// 检测碰撞框而非人物中心；不是离原始世界坐标0提前若干格传送。
/// </summary>
internal static class BoundaryContact
{
    internal const int NativeEdgePaddingPixels = 640;
    internal const int RearmDistancePixels = 8 * 16;

    internal static BoundarySide? Side(float playerX, int playerWidth, float worldLeft, float worldRight,
        int tolerancePixels)
    {
        if (!ValidBody(playerX, playerWidth, worldLeft, worldRight)
            || tolerancePixels is < 0 or > 8) return null;
        // 这里只判断身体几何接触；调用者另用IsOutward校验向外意图，顶边时速度可为0。
        if (playerX <= worldLeft + NativeEdgePaddingPixels + tolerancePixels)
            return BoundarySide.Left;
        if ((double)playerX + playerWidth >= (double)worldRight - NativeEdgePaddingPixels - tolerancePixels)
            return BoundarySide.Right;
        return null;
    }

    internal static bool IsOutward(BoundarySide side, float velocityX, bool controlLeft, bool controlRight)
    {
        if (!float.IsFinite(velocityX)) return false;
        // 朝内按键优先于残留惯性；双键同时按不猜测方向。
        return side switch
        {
            BoundarySide.Left => !controlRight && (controlLeft || velocityX < -0.01f),
            BoundarySide.Right => !controlLeft && (controlRight || velocityX > 0.01f),
            _ => false
        };
    }

    internal static bool IsInterior(float playerX, int playerWidth, float worldLeft, float worldRight) =>
        ValidBody(playerX, playerWidth, worldLeft, worldRight)
        && playerX > worldLeft + NativeEdgePaddingPixels + RearmDistancePixels
        && (double)playerX + playerWidth < (double)worldRight - NativeEdgePaddingPixels - RearmDistancePixels;

    private static bool ValidBody(float x, int width, float left, float right) =>
        float.IsFinite(x) && float.IsFinite(left) && float.IsFinite(right)
        && width is >= 1 and <= 128
        && (double)right - left > 2 * (NativeEdgePaddingPixels + RearmDistancePixels) + width
        && x >= left && (double)x + width <= right;
}
