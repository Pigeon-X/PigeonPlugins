using System;
using Terraria;

namespace AntiCheatingTool;

public static class ProtectionZone
{
	public static int SpawnTileX => Main.spawnTileX;

	public static int SpawnTileY => Main.spawnTileY;

	public static bool ContainsTile(int tileX, int tileY, Config cfg)
	{
		Protection protection = cfg.Protection;
		int num = tileX - SpawnTileX;
		if (num < 0)
		{
			num = -num;
		}
		int spawnTileY = SpawnTileY;
		if (tileY <= spawnTileY)
		{
			if (protection.SkyToSurface)
			{
				return num <= protection.SpawnXRadius;
			}
			return false;
		}
		if (tileY <= spawnTileY + protection.UndergroundDepth)
		{
			return num <= protection.UndergroundXRadius;
		}
		return false;
	}

	public static bool ContainsWorld(float worldX, float worldY, Config cfg)
	{
		return ContainsTile((int)(worldX / 16f), (int)(worldY / 16f), cfg);
	}

	public static string Describe(Config cfg)
	{
		_003C_003Ey__InlineArray6<object> buffer = default;
		buffer[0] = SpawnTileX;
		buffer[1] = SpawnTileY;
		buffer[2] = cfg.Protection.SpawnXRadius;
		buffer[3] = (cfg.Protection.SkyToSurface ? "是" : "否");
		buffer[4] = cfg.Protection.UndergroundXRadius;
		buffer[5] = cfg.Protection.UndergroundDepth;
		return string.Format("出生点({0},{1}) 地上±{2} 天空保护:{3} 地下±{4}深{5}", (ReadOnlySpan<object?>)buffer);
	}
}
