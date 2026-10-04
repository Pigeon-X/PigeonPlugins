using Microsoft.Xna.Framework;

namespace AntiCheatingTool;

public static class FloodCheck
{
	public static bool ShouldFlag(Config cfg, int packetSlot, Vector2 dropPos, Vector2 playerCenter, Vector2 playerVelocity)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		if (cfg == null || !cfg.Features.EnableAntiItemFlood)
		{
			return false;
		}
		if (packetSlot != cfg.AntiItemFlood.PacketSlot)
		{
			return false;
		}
		float num = (float)cfg.AntiItemFlood.DistanceTiles * 16f;
		return Vector2.Distance(dropPos, playerCenter + playerVelocity) >= num;
	}
}
