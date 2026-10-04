using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace AntiCheatingTool;

public static class ConfigText
{
	private static readonly Dictionary<string, string> Cn = new Dictionary<string, string>
	{
		["Version"] = "配置版本",
		["Features"] = "功能开关",
		["EnableNoSummon"] = "启用弹幕召唤拦截",
		["EnableAntiItemFlood"] = "启用防物品洪水",
		["EnableMultiChestProtect"] = "启用多箱保护",
		["Protection"] = "保护区",
		["SpawnXRadius"] = "地上半径",
		["SkyToSurface"] = "保护天空到地表",
		["UndergroundDepth"] = "地下深度",
		["UndergroundXRadius"] = "地下半径",
		["ShowEnterTip"] = "显示进区提示",
		["ShowLeaveTip"] = "显示离区提示",
		["EnterTipText"] = "进区提示语",
		["LeaveTipText"] = "离区提示语",
		["TipCooldownSeconds"] = "提示冷却秒",
		["NoSummon"] = "禁止召唤",
		["Projs"] = "拦截弹幕",
		["SummonNpcs"] = "拦截召唤物",
		["Rule"] = "违规计数",
		["Enabled"] = "启用",
		["Threshold"] = "触发次数",
		["WindowSeconds"] = "时间窗秒",
		["KickTip"] = "踢出理由",
		["AntiItemFlood"] = "防物品洪水",
		["PacketSlot"] = "触发槽位",
		["DistanceTiles"] = "距离格数",
		["MultiChestProtect"] = "多箱保护",
		["MaxOpenChests"] = "最大同时开箱数",
		["BombHandling"] = "炸弹处理",
		["Mode"] = "处理方式",
		["ProjToItem"] = "弹幕转物品",
		["Compatibility"] = "兼容",
		["TargetVersion"] = "适配版本",
		["ProjIdFallback"] = "未知弹幕ID不报错"
	};

	private static readonly Dictionary<string, string> En = BuildReverse();

	private static readonly Dictionary<string, Dictionary<string, string>> ValueCnToEn = new Dictionary<string, Dictionary<string, string>> { ["Mode"] = new Dictionary<string, string>
	{
		["恢复掉落物"] = "RevertItem",
		["封印弹幕"] = "Disable"
	} };

	private static readonly string[] RemovedKeys = new string[6] { "NotifyAdmins", "AnnounceToAll", "TipPlayer", "提醒管理员", "公屏提示", "提示玩家" };

	private static readonly Dictionary<string, Dictionary<string, string>> ValueEnToCn = BuildValueReverse();

	private static readonly Dictionary<int, string> ProjNames = new Dictionary<int, string>
	{
		[28] = "炸弹",
		[29] = "雷管",
		[37] = "粘性炸弹",
		[42] = "沙枪弹",
		[136] = "榴弹II",
		[137] = "火箭II",
		[138] = "感应雷II",
		[142] = "榴弹IV",
		[143] = "火箭IV",
		[144] = "感应雷IV",
		[201] = "墓碑",
		[202] = "十字墓碑",
		[203] = "石碑",
		[204] = "墓石",
		[205] = "方尖碑",
		[339] = "雪人火箭II",
		[341] = "雪人火箭IV",
		[470] = "粘性雷管",
		[516] = "弹跳炸弹",
		[519] = "炸弹鱼",
		[637] = "弹跳雷管",
		[714] = "庆典武器",
		[715] = "庆典火箭",
		[716] = "庆典爆破火箭",
		[717] = "庆典大型火箭",
		[718] = "庆典大型爆破火箭",
		[773] = "圣甲虫炸弹",
		[776] = "集束火箭I",
		[777] = "集束榴弹I",
		[778] = "集束地雷I",
		[779] = "集束碎片I",
		[780] = "集束火箭II",
		[781] = "集束榴弹II",
		[782] = "集束地雷II",
		[783] = "集束碎片II",
		[784] = "潮湿火箭",
		[785] = "潮湿榴弹",
		[786] = "潮湿地雷",
		[787] = "熔岩火箭",
		[788] = "熔岩榴弹",
		[789] = "熔岩地雷",
		[790] = "蜂蜜火箭",
		[791] = "蜂蜜榴弹",
		[792] = "蜂蜜地雷",
		[793] = "微型核弹I",
		[796] = "微型核弹II",
		[799] = "干燥火箭",
		[800] = "干燥榴弹",
		[801] = "干燥地雷",
		[802] = "短剑突刺",
		[803] = "雪人集束火箭I",
		[804] = "雪人集束火箭II",
		[805] = "雪人潮湿火箭",
		[806] = "雪人熔岩火箭",
		[807] = "雪人蜂蜜火箭",
		[808] = "雪人微型核弹I",
		[809] = "雪人微型核弹II",
		[810] = "雪人干燥火箭",
		[862] = "雪人集束碎片I",
		[863] = "雪人集束碎片II",
		[903] = "潮湿炸弹",
		[904] = "熔岩炸弹",
		[905] = "蜂蜜炸弹",
		[906] = "干燥炸弹",
		[910] = "土块炸弹",
		[911] = "粘性土块炸弹",
		[930] = "坦克火箭"
	};

	private static readonly Dictionary<int, string> ItemNames = new Dictionary<int, string>
	{
		[166] = "炸弹",
		[167] = "雷管",
		[235] = "粘性炸弹",
		[2896] = "粘性雷管",
		[3115] = "弹跳炸弹",
		[3547] = "弹跳雷管",
		[3196] = "炸弹鱼",
		[4423] = "圣甲虫炸弹",
		[4824] = "潮湿炸弹",
		[4825] = "熔岩炸弹",
		[4826] = "蜂蜜炸弹",
		[4827] = "干燥炸弹",
		[4908] = "土块炸弹",
		[4909] = "粘性土块炸弹"
	};

	public static string LastError { get; private set; } = "";

	public static bool HasRemovedKeys(string json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return false;
		}
		string[] removedKeys = RemovedKeys;
		foreach (string value in removedKeys)
		{
			if (json.Contains(value))
			{
				return true;
			}
		}
		return false;
	}

	private static Dictionary<string, string> BuildReverse()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (KeyValuePair<string, string> item in Cn)
		{
			dictionary[item.Value] = item.Key;
		}
		return dictionary;
	}

	private static Dictionary<string, Dictionary<string, string>> BuildValueReverse()
	{
		Dictionary<string, Dictionary<string, string>> dictionary = new Dictionary<string, Dictionary<string, string>>();
		foreach (KeyValuePair<string, Dictionary<string, string>> item in ValueCnToEn)
		{
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
			foreach (KeyValuePair<string, string> item2 in item.Value)
			{
				dictionary2[item2.Value] = item2.Key;
			}
			dictionary[item.Key] = dictionary2;
		}
		return dictionary;
	}

	public static Config? Parse(string json)
	{
		try
		{
			LastError = "";
			return Translate(JToken.Parse(Clean(json))).ToObject<Config>();
		}
		catch (Exception ex)
		{
			LastError = ex.GetType().Name + ": " + ex.Message;
			return null;
		}
	}

	public static Config? TryMigrateLegacy(string json)
	{
		try
		{
			JToken val = JToken.Parse(Clean(json));
			JObject val2 = (JObject)(object)((val is JObject) ? val : null);
			if (val2 == null)
			{
				return null;
			}
			JToken val3 = val2["NoSummonArea"];
			JObject val4 = (JObject)(object)((val3 is JObject) ? val3 : null);
			JToken val5 = val2["AntiItemFlood"];
			JObject val6 = (JObject)(object)((val5 is JObject) ? val5 : null);
			if (val4 == null && val2["DisableOpenMultipleChest"] == null && (val6 == null || val6["MultipleCount"] == null))
			{
				return null;
			}
			Config config = new Config();
			if (val4 != null)
			{
				if (val4["Projs"] != null)
				{
					config.NoSummon.Projs = val4["Projs"].ToObject<int[]>() ?? Config.DefaultProjs();
				}
				if (val4["MultipleCount"] != null)
				{
					config.NoSummon.Rule.Threshold = (int)val4["MultipleCount"];
				}
				if (val4["Multiplekickout"] != null)
				{
					config.NoSummon.Rule.Enabled = (bool)val4["Multiplekickout"];
				}
				if (val4["Tip"] != null && (string)val4["Tip"] != null)
				{
					config.NoSummon.KickTip = (string)val4["Tip"];
				}
				if (val4["ShowTipOnEnter"] != null)
				{
					bool flag = (bool)val4["ShowTipOnEnter"];
					config.Protection.ShowEnterTip = flag;
					config.Protection.ShowLeaveTip = flag;
				}
			}
			if (val6 != null)
			{
				if (val6["MultipleCount"] != null)
				{
					config.AntiItemFlood.Rule.Threshold = (int)val6["MultipleCount"];
				}
				if (val6["Multiplekickout"] != null)
				{
					config.AntiItemFlood.Rule.Enabled = (bool)val6["Multiplekickout"];
				}
				if (val6["Tip"] != null && (string)val6["Tip"] != null)
				{
					config.AntiItemFlood.KickTip = (string)val6["Tip"];
				}
			}
			if (val2["DisableOpenMultipleChest"] != null)
			{
				bool flag2 = (bool)val2["DisableOpenMultipleChest"];
				config.Features.EnableMultiChestProtect = flag2;
				config.MultiChestProtect.MaxOpenChests = (flag2 ? 1 : 0);
			}
			return config;
		}
		catch
		{
			return null;
		}
	}

	private static string Clean(string json)
	{
		if (json == null)
		{
			return "";
		}
		return json.TrimStart(new char[5] { '\ufeff', ' ', '\t', '\r', '\n' });
	}

	private static JToken Translate(JToken token)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected Obj, but got Unknown
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Expected Obj, but got Unknown
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Invalid comparison between Unknown and I4
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected Obj, but got Unknown
		JObject val = (JObject)(object)((token is JObject) ? token : null);
		if (val != null)
		{
			JObject val2 = new JObject();
			{
				foreach (JProperty item in val.Properties())
				{
					string text = (En.TryGetValue(item.Name, out string value) ? value : item.Name);
					JToken val3 = Translate(item.Value);
					JValue val4 = (JValue)(object)((val3 is JValue) ? val3 : null);
					if (val4 != null && (int)((JToken)val4).Type == 8 && ValueCnToEn.TryGetValue(text, out Dictionary<string, string> value2) && value2.TryGetValue(val4.Value?.ToString() ?? "", out var value3))
					{
						val3 = (JToken)new JValue(value3);
					}
					val2[text] = val3;
				}
				return (JToken)(object)val2;
			}
		}
		JArray val5 = (JArray)(object)((token is JArray) ? token : null);
		if (val5 != null)
		{
			JArray val6 = new JArray();
			{
				foreach (JToken item2 in val5)
				{
					val6.Add(Translate(item2));
				}
				return (JToken)(object)val6;
			}
		}
		return token.DeepClone();
	}

	public static string BuildTemplate(Config c)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("{");
		stringBuilder.AppendLine("  // ============================================================");
		stringBuilder.AppendLine("  //  AntiCheatingTool 配置文件（中文版）");
		stringBuilder.AppendLine("  //  用法：");
		stringBuilder.AppendLine("  //   1. 本文件允许 // 注释；改完存盘后进游戏执行 /ac reload 即时生效，不用重启服务器。");
		stringBuilder.AppendLine("  //   2. 保护区是「以出生点为中心」自动算出来的，换图 / 改重生点后都不用动这里。");
		stringBuilder.AppendLine("  //   3. 地上的规矩：y <= 出生点Y 的部分按「地上」算（含天空）；再往下按「地下」算。");
		stringBuilder.AppendLine("  // ============================================================");
		stringBuilder.AppendLine("  \"配置版本\": " + c.Version + ",");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("  // ==================== 功能总开关 ====================");
		stringBuilder.AppendLine("  \"功能开关\": {");
		stringBuilder.AppendLine("    // 保护区内的炸药 / 危险弹幕拦截 + 召唤拦截，关掉后这些判定全部不生效");
		stringBuilder.AppendLine("    \"启用弹幕召唤拦截\": " + Bool(c.Features.EnableNoSummon) + ",");
		stringBuilder.AppendLine("    // 玩家把物品丢到离自己很远的地方（物品洪水刷屏）时处理");
		stringBuilder.AppendLine("    \"启用防物品洪水\": " + Bool(c.Features.EnableAntiItemFlood) + ",");
		stringBuilder.AppendLine("    // 同一时间只允许玩家开一个箱子");
		stringBuilder.AppendLine("    \"启用多箱保护\": " + Bool(c.Features.EnableMultiChestProtect));
		stringBuilder.AppendLine("  },");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("  // ==================== 出生点保护区 ====================");
		stringBuilder.AppendLine("  //  地上段：出生点上方一根柱子（宽 2×地上半径，从天空一直到地表）");
		stringBuilder.AppendLine("  //  地下段：地表往下「地下深度」格，宽 2×地下半径");
		stringBuilder.AppendLine("  \"保护区\": {");
		stringBuilder.AppendLine("    // 出生点左右各多少格（地上 + 天空部分）");
		stringBuilder.AppendLine("    \"地上半径\": " + c.Protection.SpawnXRadius + ",");
		stringBuilder.AppendLine("    // true = 出生点上空（天空到地表）整根柱子都保护；false = 只保护地下部分");
		stringBuilder.AppendLine("    \"保护天空到地表\": " + Bool(c.Protection.SkyToSurface) + ",");
		stringBuilder.AppendLine("    // 从地表往下保护多少格；0 = 完全不保护地下");
		stringBuilder.AppendLine("    \"地下深度\": " + c.Protection.UndergroundDepth + ",");
		stringBuilder.AppendLine("    // 地下部分的左右格数（可以和地上不一样）");
		stringBuilder.AppendLine("    \"地下半径\": " + c.Protection.UndergroundXRadius + ",");
		stringBuilder.AppendLine("    // 走进保护区时给玩家本人发一条提示（只有他自己看得见，不走公屏）");
		stringBuilder.AppendLine("    \"显示进区提示\": " + Bool(c.Protection.ShowEnterTip) + ",");
		stringBuilder.AppendLine("    // 走出保护区时是否也提示一次");
		stringBuilder.AppendLine("    \"显示离区提示\": " + Bool(c.Protection.ShowLeaveTip) + ",");
		stringBuilder.AppendLine("    // 进区提示语（可以随便改，支持中文）");
		stringBuilder.AppendLine("    \"进区提示语\": " + Q(c.Protection.EnterTipText) + ",");
		stringBuilder.AppendLine("    // 离区提示语");
		stringBuilder.AppendLine("    \"离区提示语\": " + Q(c.Protection.LeaveTipText) + ",");
		stringBuilder.AppendLine("    // 两次提示之间的最短间隔（秒）；0 = 不限。防止贴着边界来回走刷屏");
		stringBuilder.AppendLine("    \"提示冷却秒\": " + c.Protection.TipCooldownSeconds);
		stringBuilder.AppendLine("  },");
		stringBuilder.AppendLine();
		AppendProjComment(stringBuilder);
		stringBuilder.AppendLine("  \"禁止召唤\": {");
		stringBuilder.AppendLine("    \"拦截弹幕\": " + IntArray(c.NoSummon.Projs) + ",");
		stringBuilder.AppendLine("    // 在保护区内击打这些 NPC 会被取消（661 = 棱彩蝶，打死它会召唤光之女皇）");
		stringBuilder.AppendLine("    \"拦截召唤物\": " + IntArray(c.NoSummon.SummonNpcs) + ",");
		stringBuilder.AppendLine("    // 计数在「时间窗秒」内到达「触发次数」就按「踢出理由」踢人");
		stringBuilder.AppendLine("    \"违规计数\": { \"启用\": " + Bool(c.NoSummon.Rule.Enabled) + ", \"触发次数\": " + c.NoSummon.Rule.Threshold + ", \"时间窗秒\": " + c.NoSummon.Rule.WindowSeconds + " },");
		stringBuilder.AppendLine("    \"踢出理由\": " + Q(c.NoSummon.KickTip));
		stringBuilder.AppendLine("  },");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("  // ==================== 防物品洪水 ====================");
		stringBuilder.AppendLine("  //  判定：客户端报来的物品槽位 = 触发槽位，且落地位置离玩家中心+速度超过「距离格数」格");
		stringBuilder.AppendLine("  \"防物品洪水\": {");
		stringBuilder.AppendLine("    // 原插件写死的判定槽位（400 = 客户端「新生成物品」的槽位标记），一般不用改");
		stringBuilder.AppendLine("    \"触发槽位\": " + c.AntiItemFlood.PacketSlot + ",");
		stringBuilder.AppendLine("    // 落地位置离玩家多远算异常（格）。原插件 48 像素 = 3 格");
		stringBuilder.AppendLine("    \"距离格数\": " + c.AntiItemFlood.DistanceTiles + ",");
		stringBuilder.AppendLine("    \"违规计数\": { \"启用\": " + Bool(c.AntiItemFlood.Rule.Enabled) + ", \"触发次数\": " + c.AntiItemFlood.Rule.Threshold + ", \"时间窗秒\": " + c.AntiItemFlood.Rule.WindowSeconds + " },");
		stringBuilder.AppendLine("    \"踢出理由\": " + Q(c.AntiItemFlood.KickTip));
		stringBuilder.AppendLine("  },");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("  // ==================== 多箱保护 ====================");
		stringBuilder.AppendLine("  \"多箱保护\": {");
		stringBuilder.AppendLine("    // 允许同时打开的箱子数。0 = 完全禁止开箱；1 = 只允许同时开 1 个（默认）");
		stringBuilder.AppendLine("    \"最大同时开箱数\": " + c.MultiChestProtect.MaxOpenChests);
		stringBuilder.AppendLine("  },");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("  // ==================== 炸弹处理 ====================");
		stringBuilder.AppendLine("  //  在保护区内检测到「拦截弹幕」列表里的弹幕时怎么处理：");
		stringBuilder.AppendLine("  //    恢复掉落物 = 不引爆，直接把炸弹变回地上的掉落物（玩家能捡回来）");
		stringBuilder.AppendLine("  //    封印弹幕   = 直接让弹幕消失，不掉落任何东西（原插件的行为）");
		stringBuilder.AppendLine("  \"炸弹处理\": {");
		stringBuilder.AppendLine("    \"启用\": " + Bool(c.BombHandling.Enabled) + ",");
		stringBuilder.AppendLine("    \"处理方式\": " + Q(CnValue("Mode", c.BombHandling.Mode)) + ",");
		stringBuilder.AppendLine("    // 弹幕 ID -> 掉落物 ID。只对「扔出去会被消耗的炸弹」有意义；");
		stringBuilder.AppendLine("    // 表里没有的弹幕一律按「封印弹幕」处理，不会凭空变出物品。");
		AppendBombMap(stringBuilder, c.BombHandling.ProjToItem);
		stringBuilder.AppendLine("  },");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("  // ==================== 兼容 ====================");
		stringBuilder.AppendLine("  \"兼容\": {");
		stringBuilder.AppendLine("    // 记录本配置是按哪个游戏版本调的，方便升级时对照");
		stringBuilder.AppendLine("    \"适配版本\": " + Q(c.Compatibility.TargetVersion) + ",");
		stringBuilder.AppendLine("    // true = 配置里出现当前版本不认识的弹幕 / 物品 ID 时自动丢弃，不报错");
		stringBuilder.AppendLine("    \"未知弹幕ID不报错\": " + Bool(c.Compatibility.ProjIdFallback));
		stringBuilder.AppendLine("  }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}

	public static string ExampleTemplate()
	{
		return BuildTemplate(new Config());
	}

	private static void AppendProjComment(StringBuilder sb)
	{
		sb.AppendLine("  // ==================== 禁止召唤 / 危险弹幕 ====================");
		sb.AppendLine("  //  拦截弹幕：原配置去重后共 " + ProjNames.Count + " 个，格式 [ 弹幕ID(名字) ]");
		List<string> list = new List<string>();
		foreach (KeyValuePair<int, string> projName in ProjNames)
		{
			list.Add(Pad(projName.Key + "=" + projName.Value, 16));
		}
		for (int i = 0; i < list.Count; i += 6)
		{
			int num = Math.Min(i + 6, list.Count);
			sb.AppendLine("  //   " + string.Join(" ", list.GetRange(i, num - i)).TrimEnd());
		}
	}

	private static void AppendBombMap(StringBuilder sb, Dictionary<string, int> map)
	{
		if (map == null || map.Count == 0)
		{
			sb.AppendLine("    \"弹幕转物品\": {}");
			return;
		}
		List<string> list = new List<string>(map.Keys);
		list.Sort(CompareNumericKey);
		sb.AppendLine("    \"弹幕转物品\": {");
		for (int i = 0; i < list.Count; i++)
		{
			string text = list[i];
			string value = (ProjNames.TryGetValue(ToInt(text), out string value2) ? value2 : ("弹幕" + text));
			string value3 = (ItemNames.TryGetValue(map[text], out string value4) ? value4 : ("物品" + map[text]));
			sb.Append("      // ").Append(value).Append(" -> ")
				.Append(value3)
				.AppendLine();
			sb.Append("      \"").Append(text).Append("\": ")
				.Append(map[text]);
			sb.AppendLine((i < list.Count - 1) ? "," : "");
		}
		sb.AppendLine("    }");
	}

	private static string Pad(string s, int width)
	{
		int num = 0;
		foreach (char c in s)
		{
			num += ((c <= '⺀') ? 1 : 2);
		}
		StringBuilder stringBuilder = new StringBuilder(s);
		for (int j = num; j < width; j++)
		{
			stringBuilder.Append(' ');
		}
		return stringBuilder.ToString();
	}

	private static int ToInt(string s)
	{
		if (!int.TryParse(s, out var result))
		{
			return -1;
		}
		return result;
	}

	private static int CompareNumericKey(string a, string b)
	{
		return ToInt(a).CompareTo(ToInt(b));
	}

	private static string Bool(bool b)
	{
		if (!b)
		{
			return "false";
		}
		return "true";
	}

	private static string CnValue(string key, string internalValue)
	{
		if (ValueEnToCn.TryGetValue(key, out Dictionary<string, string> value) && value.TryGetValue(internalValue, out var value2))
		{
			return value2;
		}
		return internalValue;
	}

	private static string IntArray(int[] values)
	{
		if (values == null || values.Length == 0)
		{
			return "[]";
		}
		StringBuilder stringBuilder = new StringBuilder("[ ");
		for (int i = 0; i < values.Length; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(", ");
			}
			stringBuilder.Append(values[i]);
			if (i == values.Length - 1)
			{
				stringBuilder.Append(" ]");
			}
		}
		return stringBuilder.ToString();
	}

	private static string Q(string? s)
	{
		if (s == null)
		{
			return "\"\"";
		}
		StringBuilder stringBuilder = new StringBuilder("\"");
		foreach (char c in s)
		{
			switch (c)
			{
			case '"':
				stringBuilder.Append("\\\"");
				continue;
			case '\\':
				stringBuilder.Append("\\\\");
				continue;
			case '\n':
				stringBuilder.Append("\\n");
				continue;
			case '\r':
				stringBuilder.Append("\\r");
				continue;
			case '\t':
				stringBuilder.Append("\\t");
				continue;
			}
			if (c < ' ')
			{
				StringBuilder stringBuilder2 = stringBuilder.Append("\\u");
				int num = c;
				stringBuilder2.Append(num.ToString("x4"));
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		stringBuilder.Append("\"");
		return stringBuilder.ToString();
	}
}


