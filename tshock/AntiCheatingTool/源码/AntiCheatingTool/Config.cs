using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TShockAPI;
using Terraria.ID;

namespace AntiCheatingTool;

public class Config
{
	public int Version { get; set; } = 2;

	public Features Features { get; set; } = new Features();

	public Protection Protection { get; set; } = new Protection();

	public NoSummon NoSummon { get; set; } = new NoSummon();

	public AntiItemFlood AntiItemFlood { get; set; } = new AntiItemFlood();

	public MultiChestProtect MultiChestProtect { get; set; } = new MultiChestProtect();

	public BombHandling BombHandling { get; set; } = new BombHandling();

	public Compatibility Compatibility { get; set; } = new Compatibility();

	public static string FilePath
	{
		get
		{
			string text = TShock.SavePath;
			if (string.IsNullOrEmpty(text))
			{
				text = "tshock";
			}
			return Path.Combine(text, "AntiCheatingTool.json");
		}
	}

	public static Config Load()
	{
		try
		{
			if (!File.Exists(FilePath))
			{
				Config config = new Config();
				config.Normalize();
				config.Save();
				return config;
			}
			string json = File.ReadAllText(FilePath);
			Config config2 = ConfigText.TryMigrateLegacy(json);
			if (config2 != null)
			{
				config2.Normalize();
				Log("检测到旧版英文配置，已自动升级为新版中文配置");
				config2.Save();
				return config2;
			}
			Config config3 = ConfigText.Parse(json);
			if (config3 == null)
			{
				Log("配置解析失败(" + ConfigText.LastError + ")，本次先按默认配置运行（原文件未改动）");
				Config config4 = new Config();
				config4.Normalize();
				return config4;
			}
			config3.Normalize();
			if (ConfigText.HasRemovedKeys(json))
			{
				Log("配置里还有已删除的提示项，已清理并回写");
				config3.Save();
			}
			return config3;
		}
		catch (Exception ex)
		{
			Log("读取配置异常: " + ex.Message + "，本次先按默认配置运行");
			Config config5 = new Config();
			config5.Normalize();
			return config5;
		}
	}

	public void Save()
	{
		try
		{
			string directoryName = Path.GetDirectoryName(FilePath);
			if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string text = ConfigText.BuildTemplate(this);
			if (ConfigText.Parse(text) == null)
			{
				Log("中文模板自检失败(" + ConfigText.LastError + ")，改写成标准 JSON 落盘");
				text = JsonConvert.SerializeObject((object)this, (Formatting)1);
			}
			File.WriteAllText(FilePath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
		catch (Exception ex)
		{
			Log("保存配置失败: " + ex.Message);
		}
	}

	private static void Log(string msg)
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

	public void Normalize()
	{
		if (Features == null)
		{
			Features features = (Features = new Features());
		}
		if (Protection == null)
		{
			Protection protection = (Protection = new Protection());
		}
		NoSummon noSummon;
		if (NoSummon == null)
		{
			noSummon = (NoSummon = new NoSummon());
		}
		AntiItemFlood antiItemFlood;
		if (AntiItemFlood == null)
		{
			antiItemFlood = (AntiItemFlood = new AntiItemFlood());
		}
		if (MultiChestProtect == null)
		{
			MultiChestProtect multiChestProtect = (MultiChestProtect = new MultiChestProtect());
		}
		BombHandling bombHandling;
		if (BombHandling == null)
		{
			bombHandling = (BombHandling = new BombHandling());
		}
		if (Compatibility == null)
		{
			Compatibility compatibility = (Compatibility = new Compatibility());
		}
		noSummon = NoSummon;
		if (noSummon.Rule == null)
		{
			NoSummon noSummon3 = noSummon;
			ViolationRule violationRule = new ViolationRule
			{
				Threshold = 20,
				WindowSeconds = 10
			};
			ViolationRule violationRule2 = violationRule;
			noSummon3.Rule = violationRule;
		}
		antiItemFlood = AntiItemFlood;
		if (antiItemFlood.Rule == null)
		{
			AntiItemFlood antiItemFlood3 = antiItemFlood;
			ViolationRule violationRule3 = new ViolationRule
			{
				Threshold = 9,
				WindowSeconds = 5
			};
			ViolationRule violationRule2 = violationRule3;
			antiItemFlood3.Rule = violationRule3;
		}
		bombHandling = BombHandling;
		if (bombHandling.ProjToItem == null)
		{
			Dictionary<string, int> dictionary = (bombHandling.ProjToItem = new Dictionary<string, int>());
		}
		if (NoSummon.Projs == null || NoSummon.Projs.Length == 0)
		{
			NoSummon.Projs = DefaultProjs();
		}
		if (NoSummon.SummonNpcs == null)
		{
			NoSummon.SummonNpcs = new int[1] { 661 };
		}
		if (Protection.SpawnXRadius < 0)
		{
			Protection.SpawnXRadius = 0;
		}
		if (Protection.UndergroundXRadius < 0)
		{
			Protection.UndergroundXRadius = 0;
		}
		if (Protection.UndergroundDepth < 0)
		{
			Protection.UndergroundDepth = 0;
		}
		if (Protection.TipCooldownSeconds < 0)
		{
			Protection.TipCooldownSeconds = 0;
		}
		if (string.IsNullOrEmpty(Protection.EnterTipText))
		{
			Protection.EnterTipText = "你已进入出生点保护区";
		}
		if (string.IsNullOrEmpty(Protection.LeaveTipText))
		{
			Protection.LeaveTipText = "你已离开出生点保护区";
		}
		if (string.IsNullOrEmpty(NoSummon.KickTip))
		{
			NoSummon.KickTip = "多次在保护区召唤或使用炸药";
		}
		if (string.IsNullOrEmpty(AntiItemFlood.KickTip))
		{
			AntiItemFlood.KickTip = "疑似使用物品洪水攻击";
		}
		if (NoSummon.Rule.Threshold < 1)
		{
			NoSummon.Rule.Threshold = 1;
		}
		if (NoSummon.Rule.WindowSeconds < 0)
		{
			NoSummon.Rule.WindowSeconds = 0;
		}
		if (AntiItemFlood.Rule.Threshold < 1)
		{
			AntiItemFlood.Rule.Threshold = 1;
		}
		if (AntiItemFlood.Rule.WindowSeconds < 0)
		{
			AntiItemFlood.Rule.WindowSeconds = 0;
		}
		if (AntiItemFlood.DistanceTiles < 0)
		{
			AntiItemFlood.DistanceTiles = 0;
		}
		if (MultiChestProtect.MaxOpenChests < 0)
		{
			MultiChestProtect.MaxOpenChests = 0;
		}
		if (string.Equals(BombHandling.Mode, "Disable", StringComparison.OrdinalIgnoreCase))
		{
			BombHandling.Mode = "Disable";
		}
		else
		{
			BombHandling.Mode = "RevertItem";
		}
		if (string.IsNullOrEmpty(Compatibility.TargetVersion))
		{
			Compatibility.TargetVersion = "1.4.5.8";
		}
		HashSet<int> hashSet = new HashSet<int>();
		List<int> list = new List<int>();
		int[] projs = NoSummon.Projs;
		foreach (int num in projs)
		{
			if (num >= 0 && num < ProjectileID.Count && hashSet.Add(num))
			{
				list.Add(num);
			}
		}
		NoSummon.Projs = list.ToArray();
		HashSet<int> hashSet2 = new HashSet<int>();
		List<int> list2 = new List<int>();
		projs = NoSummon.SummonNpcs;
		foreach (int num2 in projs)
		{
			if (num2 >= 0 && num2 < NPCID.Count && hashSet2.Add(num2))
			{
				list2.Add(num2);
			}
		}
		NoSummon.SummonNpcs = list2.ToArray();
		foreach (string item in new List<string>(BombHandling.ProjToItem.Keys))
		{
			if (!int.TryParse(item, out var result))
			{
				continue;
			}
			if (result < 0 || result >= ProjectileID.Count)
			{
				if (Compatibility.ProjIdFallback)
				{
					BombHandling.ProjToItem.Remove(item);
				}
				continue;
			}
			int num3 = BombHandling.ProjToItem[item];
			if ((num3 < 0 || num3 >= ItemID.Count) && Compatibility.ProjIdFallback)
			{
				BombHandling.ProjToItem.Remove(item);
			}
		}
	}

	public static int[] DefaultProjs()
	{
		return new int[67]
		{
			28, 29, 37, 42, 136, 137, 138, 142, 143, 144,
			201, 202, 203, 204, 205, 339, 341, 470, 516, 519,
			637, 714, 715, 716, 717, 718, 773, 776, 777, 778,
			779, 780, 781, 782, 783, 784, 785, 786, 787, 788,
			789, 790, 791, 792, 793, 796, 799, 800, 801, 802,
			803, 804, 805, 806, 807, 808, 809, 810, 862, 863,
			903, 904, 905, 906, 910, 911, 930
		};
	}

	public static Dictionary<string, int> DefaultBombMap()
	{
		return new Dictionary<string, int>
		{
			[28.ToString() ?? ""] = 166,
			[29.ToString() ?? ""] = 167,
			[37.ToString() ?? ""] = 235,
			[470.ToString() ?? ""] = 2896,
			[516.ToString() ?? ""] = 3115,
			[637.ToString() ?? ""] = 3547,
			[519.ToString() ?? ""] = 3196,
			[773.ToString() ?? ""] = 4423,
			[903.ToString() ?? ""] = 4824,
			[904.ToString() ?? ""] = 4825,
			[905.ToString() ?? ""] = 4826,
			[906.ToString() ?? ""] = 4827,
			[910.ToString() ?? ""] = 4908,
			[911.ToString() ?? ""] = 4909
		};
	}
}


