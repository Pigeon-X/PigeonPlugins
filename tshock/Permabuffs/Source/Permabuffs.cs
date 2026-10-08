using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Timers;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;

namespace Permabuffs_V2;

[ApiVersion(2, 1)]
public class Permabuffs : TerrariaPlugin
{
	private static bool initialized;

	// 反编译产物在 ImplicitUsings 下会有 System.Timers.Timer 与 System.Threading.Timer 的二义，
	// 这里显式限定类型，保持与原始二进制一致的行为。
	private static System.Timers.Timer? update;

	private static readonly List<int> globalbuffs = new List<int>();

	private static readonly List<RegionBuff> regionbuffs = new List<RegionBuff>();

	private static readonly Dictionary<int, List<string>> hasAnnounced = new Dictionary<int, List<string>>();

	public static string configPath = Path.Combine(TShock.SavePath, "PermabuffsConfig.json");

	public static Config config = Config.Read(configPath);

	public override string Name => "Permabuffs";

	public override string Author => "Zaicon&Cai改";

	public override string Description => "永久Buff插件.";

	public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

	public Permabuffs(Main game)
		: base(game)
	{
		((TerrariaPlugin)this).Order = 1;
	}

	public override void Initialize()
	{
		if (TShock.DB != null)
		{
			OnInitialize(EventArgs.Empty);
		}
		else
		{
			ServerApi.Hooks.GameInitialize.Register((TerrariaPlugin)(object)this, (HookHandler<EventArgs>)OnInitialize);
		}
		ServerApi.Hooks.NetGreetPlayer.Register((TerrariaPlugin)(object)this, (HookHandler<GreetPlayerEventArgs>)OnGreet);
		ServerApi.Hooks.ServerLeave.Register((TerrariaPlugin)(object)this, (HookHandler<LeaveEventArgs>)OnLeave);
		AccountHooks.AccountDelete += OnAccDelete;
		PlayerHooks.PlayerPostLogin += OnPostLogin;
		RegionHooks.RegionEntered += OnRegionEnter;
		GeneralHooks.ReloadEvent += PBReload;
	}

	protected override void Dispose(bool Disposing)
	{
		if (Disposing)
		{
			initialized = false;
			ServerApi.Hooks.GameInitialize.Deregister((TerrariaPlugin)(object)this, (HookHandler<EventArgs>)OnInitialize);
			ServerApi.Hooks.NetGreetPlayer.Deregister((TerrariaPlugin)(object)this, (HookHandler<GreetPlayerEventArgs>)OnGreet);
			ServerApi.Hooks.ServerLeave.Deregister((TerrariaPlugin)(object)this, (HookHandler<LeaveEventArgs>)OnLeave);
			AccountHooks.AccountDelete -= OnAccDelete;
			RegionHooks.RegionEntered -= OnRegionEnter;
			PlayerHooks.PlayerPostLogin -= OnPostLogin;
			GeneralHooks.ReloadEvent -= PBReload;
		}
		// 反编译把基类受保护方法写成了强制转换调用，这里改回直接调用基类实现
		base.Dispose(Disposing);
	}

	public void OnInitialize(EventArgs args)
	{
		if (!initialized)
		{
			initialized = true;
			DB.Connect();
			update = new System.Timers.Timer
			{
				Interval = 1000.0,
				AutoReset = true,
				Enabled = true
			};
			update.Elapsed += OnElapsed;
			Commands.ChatCommands.Add(new Command("pb.use", PBuffs, "permabuff")
			{
				AllowServer = false,
				HelpText = "给你一个永久Buff."
			});
			Commands.ChatCommands.Add(new Command("pb.check", PBCheck, "buffcheck")
			{
				HelpText = "列出玩家有效的永久Buff."
			});
			Commands.ChatCommands.Add(new Command("pb.give", PBGive, "gpermabuff")
			{
				HelpText = "给一个玩家永久Buff."
			});
			Commands.ChatCommands.Add(new Command("pb.region", PBRegion, "regionbuff"));
			Commands.ChatCommands.Add(new Command("pb.global", PBGlobal, "globalbuff"));
			Commands.ChatCommands.Add(new Command("pb.use", PBClear, "clearbuffs")
			{
				HelpText = "清除所有Buff."
			});
		}
	}

	public static void OnGreet(GreetPlayerEventArgs args)
	{
		if (TShock.Players[args.Who] == null)
		{
			return;
		}
		if (globalbuffs.Count > 0)
		{
			TShock.Players[args.Who].SendInfoMessage("此服务器具有以下有效的全局PermaBuffs: {0}", string.Join(", ", globalbuffs.Select((int p) => TShock.Utils.GetBuffName(p))));
		}
		if (!hasAnnounced.ContainsKey(args.Who))
		{
			hasAnnounced.Add(args.Who, new List<string>());
		}
		if (TShock.Players[args.Who].IsLoggedIn)
		{
			_ = TShock.Players[args.Who].Account.ID;
		}
	}

	public static void OnPostLogin(PlayerPostLoginEventArgs args)
	{
		if (!DB.PlayerBuffs.ContainsKey(args.Player.Account.ID))
		{
			if (DB.LoadUserBuffs(args.Player.Account.ID))
			{
				if (DB.PlayerBuffs[args.Player.Account.ID].bufflist.Count > 0)
				{
					args.Player.SendInfoMessage("你上一个会话({0})的PeraBuff仍处于有效状态!", string.Join(", ", DB.PlayerBuffs[args.Player.Account.ID].bufflist.Select((int p) => TShock.Utils.GetBuffName(p))));
				}
			}
			else
			{
				DB.AddNewUser(args.Player.Account.ID);
			}
			return;
		}
		DB.PlayerBuffs.Remove(args.Player.Account.ID);
		DB.LoadUserBuffs(args.Player.Account.ID);
		if (DB.PlayerBuffs[args.Player.Account.ID].bufflist.Count > 0)
		{
			args.Player.SendInfoMessage("你上一个会话({0})的PeraBuff仍处于有效状态!", string.Join(", ", DB.PlayerBuffs[args.Player.Account.ID].bufflist.Select((int p) => TShock.Utils.GetBuffName(p))));
		}
	}

	public static void OnAccDelete(AccountDeleteEventArgs args)
	{
		DB.ClearPlayerBuffs(args.Account.ID);
	}

	public void OnRegionEnter(RegionHooks.RegionEnteredEventArgs args)
	{
		RegionBuff regionBuff = config.regionbuffs.FirstOrDefault((RegionBuff p) => p.regionName == args.Region.Name && p.buffs.Count > 0);
		if (regionBuff != null && hasAnnounced.ContainsKey(args.Player.Index) && !hasAnnounced[args.Player.Index].Contains(args.Region.Name))
		{
			args.Player.SendSuccessMessage("你进入了一个启用了以下buff的区域: {0}", string.Join(", ", regionBuff.buffs.Keys.Select((int p) => TShock.Utils.GetBuffName(p))));
			hasAnnounced[args.Player.Index].Add(args.Region.Name);
		}
	}

	public static void OnLeave(LeaveEventArgs args)
	{
		TSPlayer tSPlayer = TShock.Players[args.Who];
		if (tSPlayer != null)
		{
			if (hasAnnounced.Keys.Contains(args.Who))
			{
				hasAnnounced.Remove(args.Who);
			}
			if (tSPlayer.IsLoggedIn && DB.PlayerBuffs.ContainsKey(tSPlayer.Account.ID))
			{
				DB.PlayerBuffs.Remove(tSPlayer.Account.ID);
			}
		}
	}

	private void OnElapsed(object sender, ElapsedEventArgs args)
	{
		int i;
		for (i = 0; i < TShock.Players.Length; i++)
		{
			if (TShock.Players[i] == null)
			{
				continue;
			}
			foreach (int globalbuff in globalbuffs)
			{
				TShock.Players[i].SetBuff(globalbuff, 18000);
			}
			if (TShock.Players[i].CurrentRegion != null)
			{
				RegionBuff regionBuff = config.regionbuffs.FirstOrDefault((RegionBuff p) => TShock.Players[i].CurrentRegion.Name == p.regionName && p.buffs.Count > 0);
				if (regionBuff != null)
				{
					foreach (KeyValuePair<int, int> buff in regionBuff.buffs)
					{
						TShock.Players[i].SetBuff(buff.Key, buff.Value * 60);
					}
				}
			}
			if (!TShock.Players[i].IsLoggedIn || !DB.PlayerBuffs.ContainsKey(TShock.Players[i].Account.ID))
			{
				continue;
			}
			foreach (int item in DB.PlayerBuffs[TShock.Players[i].Account.ID].bufflist)
			{
				TShock.Players[i].SetBuff(item, 18000);
			}
		}
	}

	private void PBuffs(CommandArgs args)
	{
		if (config.buffgroups.Length == 0)
		{
			args.Player.SendErrorMessage("服务器管理员尚未定义任何Buff组.请联系管理员以解决此问题.");
			return;
		}
		List<BuffGroup> list = config.buffgroups.Where((BuffGroup e) => args.Player.HasPermission("pb." + e.groupPerm) || args.Player.HasPermission("pb.useall")).ToList();
		int bufftype = -1;
		if (args.Parameters.Count == 0)
		{
			args.Player.SendErrorMessage("参数无效: {0}permabuff <Buff名/BuffID>", args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier);
			return;
		}
		string name = string.Join(" ", args.Parameters);
		if (!int.TryParse(args.Parameters[0], out bufftype))
		{
			List<int> buffByName = TShock.Utils.GetBuffByName(name);
			if (buffByName.Count < 1)
			{
				args.Player.SendErrorMessage("没有找到这个Buff.");
				return;
			}
			if (buffByName.Count > 1)
			{
				args.Player.SendMultipleMatchError(buffByName.Select((int p) => TShock.Utils.GetBuffName(p)));
				return;
			}
			bufftype = buffByName[0];
		}
		else if (bufftype > BuffID.Count || bufftype < 1)
		{
			args.Player.SendErrorMessage("BuffID无效!");
		}
		int iD = args.Player.Account.ID;
		list.RemoveAll((BuffGroup e) => !e.buffIDs.Contains(bufftype));
		if (list.Count == 0)
		{
			args.Player.SendErrorMessage("你没有权限使用这个PermaBuff!");
		}
		else if (DB.PlayerBuffs[iD].bufflist.Contains(bufftype))
		{
			DB.PlayerBuffs[iD].bufflist.Remove(bufftype);
			DB.UpdatePlayerBuffs(iD, DB.PlayerBuffs[iD].bufflist);
			args.Player.SendInfoMessage("你移除了 " + TShock.Utils.GetBuffName(bufftype) + " PermaBuff.");
		}
		else
		{
			DB.PlayerBuffs[iD].bufflist.Add(bufftype);
			DB.UpdatePlayerBuffs(iD, DB.PlayerBuffs[iD].bufflist);
			args.Player.SendSuccessMessage("你已经被赋予了Buff " + TShock.Utils.GetBuffName(bufftype) + "! 重新输入该指令移除这个Buf.");
		}
	}

	private void PBCheck(CommandArgs args)
	{
		if (args.Parameters.Count == 0)
		{
			args.Player.SendErrorMessage("参数无效: {0}buffcheck <玩家名>", args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier);
			return;
		}
		List<TSPlayer> list = TSPlayer.FindByNameOrID(string.Join(" ", args.Parameters));
		if (list.Count < 1)
		{
			args.Player.SendErrorMessage("没有找到该玩家.");
			return;
		}
		if (list.Count > 1)
		{
			args.Player.SendMultipleMatchError(list.Select((TSPlayer p) => p.Name));
			return;
		}
		if (!list[0].IsLoggedIn)
		{
			args.Player.SendErrorMessage("{0} 没有有效的PermaBuff.", list[0].Name);
			return;
		}
		if (DB.PlayerBuffs[list[0].Account.ID].bufflist.Count == 0)
		{
			args.Player.SendInfoMessage("{0} 没有有效的PermaBuff.", list[0].Name);
			return;
		}
		args.Player.SendInfoMessage("{0} 有如下Permabuff: {1}", list[0].Name, string.Join(", ", DB.PlayerBuffs[list[0].Account.ID].bufflist.Select((int p) => TShock.Utils.GetBuffName(p))));
	}

	private void PBGive(CommandArgs args)
	{
		if (config.buffgroups.Length == 0)
		{
			args.Player.SendErrorMessage("服务器管理员尚未定义任何Buff组.请联系管理员以解决此问题.");
			return;
		}
		List<BuffGroup> list = config.buffgroups.Where((BuffGroup e) => args.Player.HasPermission("pb." + e.groupPerm) || args.Player.HasPermission("pb.useall")).ToList();
		if (args.Parameters.Count == 2)
		{
			if (args.Parameters[0].Equals("-g", StringComparison.CurrentCultureIgnoreCase) && args.Parameters[1].Equals("list", StringComparison.CurrentCultureIgnoreCase))
			{
				args.Player.SendInfoMessage("有效PermaBuff组: " + string.Join(", ", list.Select((BuffGroup e) => e.groupName)));
				return;
			}
			List<TSPlayer> list2 = TSPlayer.FindByNameOrID(args.Parameters[1]);
			if (list2.Count < 1)
			{
				args.Player.SendErrorMessage("没有找到该玩家.");
				return;
			}
			if (list2.Count > 1)
			{
				args.Player.SendMultipleMatchError(list2.Select((TSPlayer p) => p.Name));
				return;
			}
			if (!list2[0].IsLoggedIn)
			{
				args.Player.SendErrorMessage("此玩家无法接收PermaBuff(没有登录)!");
				return;
			}
			int iD = list2[0].Account.ID;
			string name = args.Parameters[0];
			if (!int.TryParse(args.Parameters[0], out var bufftype))
			{
				List<int> list3 = new List<int>();
				list3 = TShock.Utils.GetBuffByName(name);
				if (list3.Count < 1)
				{
					args.Player.SendErrorMessage("没有找到这个Buff.");
					return;
				}
				if (list3.Count > 1)
				{
					args.Player.SendMultipleMatchError(list3.Select((int p) => TShock.Utils.GetBuffName(p)));
					return;
				}
				bufftype = list3[0];
			}
			else if (bufftype > BuffID.Count || bufftype < 1)
			{
				args.Player.SendErrorMessage("BuffID无效!");
			}
			list.RemoveAll((BuffGroup e) => !e.buffIDs.Contains(bufftype));
			if (list.Count == 0)
			{
				args.Player.SendErrorMessage("你没有权限使用这个Permabuff!");
				return;
			}
			DBInfo orCreate = DB.GetOrCreate(iD);
			if (orCreate.bufflist.Contains(bufftype))
			{
				orCreate.bufflist.Remove(bufftype);
				DB.UpdatePlayerBuffs(iD, orCreate.bufflist);
				args.Player.SendInfoMessage($"你移除了 {list2[0].Name} 的PermaBuff {TShock.Utils.GetBuffName(bufftype)}.");
				if (!args.Silent)
				{
					list2[0].SendInfoMessage(args.Player.Name + " 移除了你的PermaBuff " + TShock.Utils.GetBuffName(bufftype) + ".");
				}
			}
			else
			{
				orCreate.bufflist.Add(bufftype);
				DB.UpdatePlayerBuffs(iD, orCreate.bufflist);
				args.Player.SendSuccessMessage($"你赋予了玩家 {list2[0].Name} PermaBuff {TShock.Utils.GetBuffName(bufftype)}!");
				if (!args.Silent)
				{
					list2[0].SendInfoMessage(args.Player.Name + " 赋予了你PermaBuff " + TShock.Utils.GetBuffName(bufftype) + "!");
				}
			}
		}
		else if (args.Parameters.Count == 3)
		{
			if (args.Parameters[0] != "-g")
			{
				args.Player.SendErrorMessage("参数无效:");
				args.Player.SendErrorMessage("{0}gpermabuff <Buff名/BuffID> <玩家名>", TShock.Config.Settings.CommandSpecifier);
				args.Player.SendErrorMessage("{0}gpermabuff -g <Buff组> <玩家名>", TShock.Config.Settings.CommandSpecifier);
			}
			List<TSPlayer> list4 = TSPlayer.FindByNameOrID(args.Parameters[2]);
			if (list4.Count == 0)
			{
				args.Player.SendErrorMessage("没有找到该玩家: " + args.Parameters[2]);
				return;
			}
			if (list4.Count > 1)
			{
				args.Player.SendMultipleMatchError(list4.Select((TSPlayer p) => p.Name));
				return;
			}
			if (!list4[0].IsLoggedIn)
			{
				args.Player.SendErrorMessage("此玩家无法接收PermaBuff!");
				return;
			}
			if (!list.Any((BuffGroup e) => e.groupName.Equals(args.Parameters[1], StringComparison.CurrentCultureIgnoreCase)))
			{
				args.Player.SendErrorMessage("无法查询到指定的Buff组!");
			}
			TSPlayer tSPlayer = list4[0];
			int iD2 = list4[0].Account.ID;
			DBInfo orCreate2 = DB.GetOrCreate(iD2);
			foreach (int buffID in list.First((BuffGroup e) => e.groupName.Equals(args.Parameters[1], StringComparison.CurrentCultureIgnoreCase)).buffIDs)
			{
				if (!orCreate2.bufflist.Contains(buffID))
				{
					orCreate2.bufflist.Add(buffID);
				}
			}
			DB.UpdatePlayerBuffs(iD2, orCreate2.bufflist);
			args.Player.SendSuccessMessage($"成功赋予玩家 {tSPlayer.Name} PermaBuff组 {args.Parameters[1]} 中的所有Permabuff!");
			if (!args.Silent)
			{
				args.Player.SendInfoMessage(args.Player.Name + " 已赋予你PermaBuff组 " + args.Parameters[1] + " 中所有Permabuff!");
			}
		}
		else
		{
			args.Player.SendErrorMessage("参数无效:");
			args.Player.SendErrorMessage("{0}gpermabuff <Buff名/BuffID> <玩家名>", TShock.Config.Settings.CommandSpecifier);
			args.Player.SendErrorMessage("{0}gpermabuff -g <Buff组> <玩家名>", TShock.Config.Settings.CommandSpecifier);
		}
	}

	private void PBReload(ReloadEventArgs args)
	{
		config = Config.Read(configPath);
		args.Player.SendWarningMessage("[Permabuff]:插件配置已重载!");
	}

	private void PBRegion(CommandArgs args)
	{
		if (args.Parameters.Count < 3 || args.Parameters.Count > 4)
		{
			args.Player.SendErrorMessage("参数无效: {0}regionbuff <add/del> <区域名> <Buff名/BuffID> [持续时间]", args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier);
			return;
		}
		if (args.Parameters[0].Equals("add", StringComparison.CurrentCultureIgnoreCase))
		{
			string text = args.Parameters[1];
			Region regionByName = TShock.Regions.GetRegionByName(text);
			string text2 = args.Parameters[2];
			if (args.Parameters.Count != 4)
			{
				args.Player.SendErrorMessage("参数无效: {0}regionbuff <add/del> <区域名> <Buff名/BuffID> [持续时间]", args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier);
				return;
			}
			string s = args.Parameters[3];
			int result = -1;
			if (regionByName == null)
			{
				args.Player.SendErrorMessage("区域无效: {0}", text);
				return;
			}
			if (!int.TryParse(text2, out result))
			{
				List<int> buffByName = TShock.Utils.GetBuffByName(text2);
				if (buffByName.Count == 0)
				{
					args.Player.SendErrorMessage("没有找到与 {0} 匹配的Buff.", text2);
					return;
				}
				if (buffByName.Count > 1)
				{
					args.Player.SendMultipleMatchError(buffByName.Select((int p) => TShock.Utils.GetBuffName(p)));
					return;
				}
				result = buffByName[0];
			}
			if (result < 0 || result > BuffID.Count)
			{
				args.Player.SendErrorMessage("BuffID无效: {0}", result.ToString());
				return;
			}
			int result2 = -1;
			if (!int.TryParse(s, out result2) || result2 < 1 || result2 > 540)
			{
				args.Player.SendErrorMessage("持续时间无效!");
				return;
			}
			bool flag = false;
			for (int num = 0; num < config.regionbuffs.Length; num++)
			{
				if (config.regionbuffs[num].regionName == regionByName.Name)
				{
					flag = true;
					if (config.regionbuffs[num].buffs.Keys.Contains(result))
					{
						args.Player.SendErrorMessage("区域 {0} 已经添加了Buff {1}!", regionByName.Name, TShock.Utils.GetBuffName(result));
						return;
					}
					config.regionbuffs[num].buffs.Add(result, result2);
					args.Player.SendSuccessMessage("成功添加Buff {0} 至区域 {1} 持续时间{2}秒!", TShock.Utils.GetBuffName(result), regionByName.Name, result2.ToString());
					config.Write(configPath);
					return;
				}
			}
			if (!flag)
			{
				List<RegionBuff> list = config.regionbuffs.ToList();
				list.Add(new RegionBuff
				{
					buffs = new Dictionary<int, int> { { result, result2 } },
					regionName = regionByName.Name
				});
				config.regionbuffs = list.ToArray();
				args.Player.SendSuccessMessage("成功添加Buff {0} 至区域 {1} 持续时间{2}秒!", TShock.Utils.GetBuffName(result), regionByName.Name, result2.ToString());
				config.Write(configPath);
				return;
			}
		}
		if (args.Parameters[0].Equals("del", StringComparison.CurrentCultureIgnoreCase) || args.Parameters[0].Equals("delete", StringComparison.CurrentCultureIgnoreCase))
		{
			string text3 = args.Parameters[1];
			Region regionByName2 = TShock.Regions.GetRegionByName(text3);
			string text4 = args.Parameters[2];
			int result3 = -1;
			if (regionByName2 == null)
			{
				args.Player.SendErrorMessage("区域无效: {0}", text3);
				return;
			}
			if (!int.TryParse(text4, out result3))
			{
				List<int> buffByName2 = TShock.Utils.GetBuffByName(text4);
				if (buffByName2.Count == 0)
				{
					args.Player.SendErrorMessage("没有找到与 {0} 匹配的Buff.", text4);
					return;
				}
				if (buffByName2.Count > 1)
				{
					args.Player.SendMultipleMatchError(buffByName2.Select((int p) => TShock.Utils.GetBuffName(p)));
					return;
				}
				result3 = buffByName2[0];
			}
			if (result3 < 0 || result3 > BuffID.Count)
			{
				args.Player.SendErrorMessage("BuffID无效: {0}", result3.ToString());
				return;
			}
			bool flag2 = false;
			for (int num2 = 0; num2 < config.regionbuffs.Length; num2++)
			{
				if (config.regionbuffs[num2].regionName == regionByName2.Name && config.regionbuffs[num2].buffs.ContainsKey(result3))
				{
					config.regionbuffs[num2].buffs.Remove(result3);
					args.Player.SendSuccessMessage("成功从区域 {1} 移除Buff {0}!", TShock.Utils.GetBuffName(result3), regionByName2.Name);
					config.Write(configPath);
					flag2 = true;
					return;
				}
			}
			if (!flag2)
			{
				args.Player.SendSuccessMessage("Buff {0} 不在区域 {1} 里!", TShock.Utils.GetBuffName(result3), regionByName2.Name);
				return;
			}
		}
		args.Player.SendErrorMessage("参数无效: {0}regionbuff <add/del> <区域名> <Buff名/BuffID>", args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier);
	}

	private void PBGlobal(CommandArgs args)
	{
		if (args.Parameters.Count == 0)
		{
			args.Player.SendErrorMessage("参数无效: {0}globalbuff <Buff名>", args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier);
			return;
		}
		string name = string.Join(" ", args.Parameters);
		if (!int.TryParse(args.Parameters[0], out var bufftype))
		{
			List<int> buffByName = TShock.Utils.GetBuffByName(name);
			if (buffByName.Count < 1)
			{
				args.Player.SendErrorMessage("没有找到与之匹配的Buff.");
				return;
			}
			if (buffByName.Count > 1)
			{
				args.Player.SendMultipleMatchError(buffByName.Select((int p) => TShock.Utils.GetBuffName(p)));
				return;
			}
			bufftype = buffByName[0];
		}
		if (bufftype > BuffID.Count || bufftype < 1)
		{
			args.Player.SendErrorMessage("无效的BuffID!");
		}
		if (!config.buffgroups.Any((BuffGroup e) => e.buffIDs.Contains(bufftype)))
		{
			args.Player.SendErrorMessage("此Buff不能作为全局buff使用!");
		}
		else if (globalbuffs.Contains(bufftype))
		{
			globalbuffs.Remove(bufftype);
			args.Player.SendSuccessMessage("Buff {0} 成功从全局PermaBuff被移除 .", TShock.Utils.GetBuffName(bufftype));
		}
		else
		{
			globalbuffs.Add(bufftype);
			args.Player.SendSuccessMessage("Buff {0} 成功被加入全局PermaBuff!", TShock.Utils.GetBuffName(bufftype));
		}
	}

	private void PBClear(CommandArgs args)
	{
		if (args.Parameters.Count == 1 && (args.Parameters[0] == "*" || args.Parameters[0].Equals("all", StringComparison.CurrentCultureIgnoreCase)))
		{
			if (!args.Player.HasPermission("pb.clear"))
			{
				args.Player.SendErrorMessage("你没有权限清除所有Buff.");
				return;
			}
			foreach (KeyValuePair<int, DBInfo> playerBuff in DB.PlayerBuffs)
			{
				playerBuff.Value.bufflist.Clear();
				DB.ClearDB();
			}
			args.Player.SendSuccessMessage("所有玩家的所有PermaBuff被成功移除.");
			if (!args.Silent)
			{
				TSPlayer.All.SendInfoMessage("{0} 移除了所有玩家的所有PermaBuff!", args.Player.Account.Name);
			}
		}
		else if (args.Parameters.Count == 1)
		{
			List<TSPlayer> list = TSPlayer.FindByNameOrID(args.Parameters[0]);
			if (list.Count > 1)
			{
				args.Player.SendMultipleMatchError(list.Select((TSPlayer x) => x.Name));
				return;
			}
			if (list.Count == 1)
			{
				DB.PlayerBuffs[list[0].Account.ID].bufflist.Clear();
				DB.ClearPlayerBuffs(list[0].Account.ID);
				args.Player.SendSuccessMessage("玩家" + list[0].Name + "所有的PermaBuff都被清除了.");
				return;
			}
			UserAccount userAccountByName = TShock.UserAccounts.GetUserAccountByName(args.Parameters[0]);
			if (userAccountByName == null)
			{
				args.Player.SendErrorMessage("没有找到该玩家或账户");
				return;
			}
			DB.ClearPlayerBuffs(userAccountByName.ID);
			args.Player.SendSuccessMessage("用户" + userAccountByName.Name + "所有的PermaBuff都被清除了.");
		}
		else if (!args.Player.RealPlayer)
		{
			args.Player.SendErrorMessage("你必须在游戏中使用该指令.");
		}
		else
		{
			DB.PlayerBuffs[args.Player.Account.ID].bufflist.Clear();
			DB.ClearPlayerBuffs(args.Player.Account.ID);
			args.Player.SendSuccessMessage("你所有的PermaBuff都被清除了.");
		}
	}
}
