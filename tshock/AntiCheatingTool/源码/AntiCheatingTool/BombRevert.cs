using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TShockAPI;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;

namespace AntiCheatingTool;

public static class BombRevert
{
	public static bool TryGetItemId(Config cfg, int projType, out int itemId)
	{
		itemId = 0;
		Dictionary<string, int> projToItem = cfg.BombHandling.ProjToItem;
		if (projToItem == null)
		{
			return false;
		}
		if (!projToItem.TryGetValue(projType.ToString(), out var value))
		{
			return false;
		}
		if (value <= 0 || value >= ItemID.Count)
		{
			return false;
		}
		itemId = value;
		return true;
	}

	public static void RevertToItem(Projectile proj, int itemId)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Item.NewItem(proj.GetItemSource_DropAsItem(), ((Entity)proj).Center, itemId, 1);
		}
		catch (Exception ex)
		{
			LogQuiet("生成掉落物失败: " + ex.Message);
		}
		RemoveSilently(proj);
	}

	public static void RemoveSilently(Projectile proj)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			int num = (int)(uint)proj.key;
			proj.active = false;
			proj.type = 0;
			NetMessage.SendData(29, -1, -1, (NetworkText)null, num, float.NaN, float.NaN, 0f, 0, 0, 0);
		}
		catch (Exception ex)
		{
			LogQuiet("移除弹幕失败: " + ex.Message);
		}
	}

	private static void LogQuiet(string msg)
	{
		try
		{
			if (TShock.Log != null)
			{
				TShock.Log.ConsoleError("[AntiCheatingTool] " + msg);
			}
		}
		catch
		{
		}
	}
}

