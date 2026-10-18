using System.Diagnostics;

namespace BoundaryTransfer;

internal readonly record struct LandingSearchStep(bool Complete, LandingResult Result);

/// <summary>
/// 1.0.x 的全高度兜底搜索，仅保留给历史算法回归测试；1.1.0 正式运行不再调用。
/// 当前行为是保持接触脚底 Y 直达对侧边界，不做任何安全落点搜索。
/// </summary>
internal sealed class BoundaryFallbackSearch
{
    private readonly int _mapWidth;
    private readonly int _mapHeight;
    private readonly int _playerWidth;
    private readonly int _playerHeight;
    private readonly int _hazardPadding;
    private readonly int _desired;
    private readonly int _minY;
    private readonly int _maxY;
    private readonly int _direction;
    private readonly int _lastCenter;
    private int _center;
    private int _upper;
    private int _lower;
    private int _candidates;
    private LandingSearchStep _terminal;

    public BoundaryFallbackSearch(ILandingMap map, BoundarySide entrySide, float desiredFeetPixels,
        int playerWidth, int playerHeight, BoundarySettings settings)
    {
        _mapWidth = map?.Width ?? 0;
        _mapHeight = map?.Height ?? 0;
        _playerWidth = playerWidth;
        _playerHeight = playerHeight;
        _hazardPadding = settings?.HazardPadding ?? -1;
        _direction = entrySide == BoundarySide.Left ? 1 : -1;

        if (map is null || settings is null
            || entrySide is not (BoundarySide.Left or BoundarySide.Right)
            || !float.IsFinite(desiredFeetPixels) || desiredFeetPixels < 0
            || desiredFeetPixels > _mapHeight * 16d
            || playerWidth is < 1 or > 128 || playerHeight is < 1 or > 256
            || settings.LandingInset is < 64 or > 600 || _hazardPadding is < 0 or > 3)
        {
            Fail("玩家位置、碰撞框或安全边界设置无效。");
            return;
        }

        _minY = 42 + (playerHeight + 15) / 16;
        _maxY = _mapHeight - 42;
        _lastCenter = _mapWidth / 2;
        _center = entrySide == BoundarySide.Left ? settings.LandingInset : _mapWidth - settings.LandingInset;
        if (_mapWidth <= 2L * (settings.LandingInset + 8) || _mapHeight < 100
            || _minY > _maxY
            || (entrySide == BoundarySide.Left ? _center > _lastCenter : _center < _lastCenter))
        {
            Fail("目标世界尺寸不足，不能容纳安全边界落点。");
            return;
        }

        // LandingResult.Candidates 是 int；拒绝连完整候选计数都无法表达的异常地图尺寸，
        // 不使用近 Y 搜索的 SearchVertical、SearchInward 或 MaxCandidates 限制兜底范围。
        long columns = Math.Abs((long)_center - _lastCenter) + 1;
        long rows = (long)_maxY - _minY + 1;
        if (columns * rows > int.MaxValue)
        {
            Fail("目标世界尺寸无效，安全落点候选计数超出可表示范围。");
            return;
        }

        _desired = (int)Math.Round(desiredFeetPixels / 16d);
        ResetVerticalCursor();
    }

    public LandingSearchStep Advance(ILandingMap map, int maxCandidates,
        Func<LandingSpot, bool>? verify = null, int maxMilliseconds = 0)
    {
        // 终态必须幂等：连传入地图的尺寸、预算及 verify 都不再读取或执行。
        if (_terminal.Complete)
            return _terminal;
        if (maxCandidates <= 0)
            return Fail("安全落点搜索的分帧候选预算无效。");
        if (map is null || map.Width != _mapWidth || map.Height != _mapHeight)
            return Fail("目标世界尺寸已改变，不能继续复用安全落点搜索。");

        long started = maxMilliseconds > 0 ? Stopwatch.GetTimestamp() : 0;
        for (int processed = 0; processed < maxCandidates; processed++)
        {
            int feet = NextFeet();
            var spot = new LandingSpot(
                (float)(_center * 16d + 8 - _playerWidth / 2d),
                (float)(feet * 16d - _playerHeight), feet);
            _candidates++;
            if (BoundaryPlanner.IsSafe(map, spot, _playerWidth, _playerHeight, _hazardPadding)
                && (verify is null || verify(spot)))
            {
                _terminal = new(true, new(true, spot, _candidates, ""));
                return _terminal;
            }

            if (_upper < _minY && _lower > _maxY)
            {
                if (_center == _lastCenter)
                    return Fail("进入侧半幅内找不到干燥、无障碍且有安全地面的已有落点，本次不切换。");
                _center += _direction;
                ResetVerticalCursor();
            }

            // 不逐候选读取时钟；超时仅让出当前帧，不丢弃刚刚推进的游标。
            if (maxMilliseconds > 0 && ((processed + 1) & 127) == 0
                && Stopwatch.GetElapsedTime(started).TotalMilliseconds >= maxMilliseconds)
                return Pending();
        }
        return Pending();
    }

    private void ResetVerticalCursor()
    {
        _upper = _desired < _minY ? _minY - 1 : Math.Min(_desired, _maxY);
        _lower = _desired >= _maxY ? _maxY + 1 : Math.Max(_desired + 1, _minY);
    }

    private int NextFeet()
    {
        // 两个单调游标恰好枚举允许的全部 Y；原 Y 在保护高度以外时也无需空转。
        if (_upper < _minY)
            return _lower++;
        if (_lower > _maxY || (long)_desired - _upper <= (long)_lower - _desired)
            return _upper--;
        return _lower++;
    }

    private LandingSearchStep Pending() => new(false, new(false, default, _candidates, ""));

    private LandingSearchStep Fail(string error)
    {
        _terminal = new(true, new(false, default, _candidates, error));
        return _terminal;
    }
}
