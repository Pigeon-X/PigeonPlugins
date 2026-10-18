using System.Collections.Frozen;

namespace BoundaryTransfer.Tests;

/// <summary>
/// 构造后不可写的地图替身；只暴露真实 ILandingMap 的只读接口，不初始化 Terraria。
/// Water/Lava/Honey/Shimmer 故意都只映射为 Liquid：验证抽象契约，不冒充 tile metadata 测试。
/// </summary>
internal sealed class FrozenMap : ILandingMap
{
    private readonly FrozenDictionary<(int X, int Y), TileCell> _cells;
    private readonly TileCell _defaultCell;
    public int Width { get; }
    public int Height { get; }
    public long Reads { get; private set; }
    public HashSet<(int X, int Y)> Visited { get; } = [];

    public FrozenMap(int width, int height, Dictionary<(int X, int Y), TileCell> cells, TileCell defaultCell)
    {
        Width = width; Height = height; _cells = cells.ToFrozenDictionary(); _defaultCell = defaultCell;
    }

    public TileCell Get(int x, int y)
    {
        Reads++;
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            throw new InvalidOperationException($"越界地图读取 ({x}, {y})，地图 {Width}x{Height}");
        Visited.Add((x, y));
        return _cells.GetValueOrDefault((x, y), _defaultCell);
    }

    public string Snapshot() => $"{Width},{Height}:{_defaultCell}|" + string.Join(";", _cells.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X).Select(p => $"{p.Key.X},{p.Key.Y}:{p.Value}"));
}

internal sealed class MapBuilder(int width = 1000, int height = 1200, TileCell defaultCell = default)
{
    private readonly Dictionary<(int X, int Y), TileCell> _cells = [];
    public int Width { get; } = width;
    public int Height { get; } = height;

    public MapBuilder Put(int x, int y, TileCell cell)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) throw new ArgumentOutOfRangeException(nameof(x));
        _cells[(x, y)] = cell; return this;
    }

    public MapBuilder Floor(int left, int right, int feet, TileCell? cell = null)
    {
        for (int x = left; x <= right; x++) Put(x, feet, cell ?? Tiles.Solid);
        return this;
    }

    public MapBuilder FloorAt(int centerTile, int feet, int playerWidth = 20)
    {
        LandingSpot spot = Fixtures.Spot(centerTile, feet, playerWidth);
        int left = (int)MathF.Floor(spot.X / 16f), right = (int)MathF.Floor((spot.X + playerWidth - 0.01f) / 16f);
        return Floor(left, right, feet);
    }

    public MapBuilder Rect(int left, int right, int top, int bottom, TileCell cell)
    {
        for (int x = left; x <= right; x++)
            for (int y = top; y <= bottom; y++) Put(x, y, cell);
        return this;
    }

    public FrozenMap Freeze() => new(Width, Height, _cells, defaultCell);
}

internal static class Tiles
{
    public static readonly TileCell Air = default;
    public static readonly TileCell Solid = new(BlocksBody: true, SupportsFeet: true);
    public static readonly TileCell Platform = new(SupportsFeet: true);
    public static readonly TileCell BodyObstacle = new(BlocksBody: true);
    public static readonly TileCell HalfBrick = new(BlocksBody: true);
    public static readonly TileCell Slope = new(BlocksBody: true);
    public static readonly TileCell Inactive = default;
    public static readonly TileCell Decorative = default;
    public static readonly TileCell Unknown = new(BlocksBody: true, Hazardous: true);
    public static readonly TileCell HazardousFloor = new(BlocksBody: true, SupportsFeet: true, Hazardous: true);
    public static readonly TileCell Hazard = new(Hazardous: true);
    public static readonly TileCell Water = new(Liquid: true);
    public static readonly TileCell Lava = new(Liquid: true);
    public static readonly TileCell Honey = new(Liquid: true);
    public static readonly TileCell Shimmer = new(Liquid: true);
    public static readonly TileCell WetSolid = new(BlocksBody: true, SupportsFeet: true, Liquid: true);
}

internal static class Fixtures
{
    public static BoundarySettings Settings(int inward = 8, int vertical = 4, int padding = 1, int budget = 60000, int margin = 96) => BoundarySettings.FromConfig(new()
    {
        WorldOrder = ["A", "B"], InwardSearchTiles = inward, VerticalSearchTiles = vertical,
        HazardPaddingTiles = padding, MaxSearchCandidates = budget, LandingInsetTiles = margin
    });

    public static LandingSpot Spot(int centerTile, int feet, int width = 20, int height = 42) => new(centerTile * 16f + 8 - width / 2f, feet * 16f - height, feet);
    public static int CenterTile(LandingSpot spot, int width = 20) => (int)MathF.Floor((spot.X + width / 2f) / 16f);

    public static LandingResult Find(FrozenMap map, BoundarySettings? settings = null, BoundarySide side = BoundarySide.Left, float feet = 200 * 16f, int width = 20, int height = 42) => BoundaryPlanner.Find(map, side, feet, width, height, settings ?? Settings());

    public static void Success(LandingResult result, int centerTile, int feet, int candidates, int width = 20, int height = 42)
    {
        Check.True(result.Success, result.Error); Check.Equal("", result.Error);
        Check.Equal(Spot(centerTile, feet, width, height), result.Spot); Check.Equal(candidates, result.Candidates);
        Check.True(float.IsFinite(result.Spot.X) && float.IsFinite(result.Spot.Y));
    }

    public static void Failure(LandingResult result, int candidates, string? fragment = null)
    {
        Check.False(result.Success); Check.Equal(candidates, result.Candidates);
        Check.True(!string.IsNullOrWhiteSpace(result.Error));
        if (fragment is not null) Check.True(result.Error.Contains(fragment, StringComparison.Ordinal), result.Error);
    }
}
