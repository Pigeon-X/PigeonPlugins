using Terraria;
using Terraria.ID;
using UnifierTSL.Servers;

namespace BoundaryTransfer;

/// <summary>只在所属世界更新线程读取图格；危险性使用当前 Terraria 的元数据表。</summary>
internal sealed class TerrariaLandingMap(ServerContext server) : ILandingMap
{
    public int Width => server.Main.maxTilesX;
    public int Height => server.Main.maxTilesY;

    public TileCell Get(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return new(true, false, true);
        var tile = server.Main.tile[x, y];
        // UTSL TileData 未分配图格是未知，不等于已验证的空气。
        if (!tile.IsNotNull)
            return new(true, false, true);
        bool liquid = tile.liquid > 0;
        if (!tile.nactive())
            return new(false, false, false, liquid);
        int type = tile.type;
        if (type >= server.Main.tileSolid.Length || type >= Main.tileSolidTop.Length)
            return new(true, false, true, liquid);
        bool platform = Main.tileSolidTop[type];
        bool solid = server.Main.tileSolid[type];
        bool hazard = IsHazardousType(type);
        bool support = (solid || platform) && tile.slope() == 0 && !tile.halfBrick()
            && (!platform || tile.frameY == 0) && !hazard;
        return new(solid && !platform, support, hazard, liquid);
    }

    internal static bool IsHazardousType(int type) =>
        type < 0 || type >= TileID.Count
        || TileID.Sets.TouchDamageImmediate[type] > 0
        || TileID.Sets.TouchDamageHot[type]
        || TileID.Sets.TouchDamageBleeding[type]
        || TileID.Sets.Suffocate[type]
        // 即使当前未触发，也不选择在机关、易坠物或易碎薄冰旁着陆。
        || type is 80 or 137 or 138 or 141 or 162 or 210 or 484 or 664 or 665 or 711 or 712 or 713 or 714 or 715 or 716;
}
