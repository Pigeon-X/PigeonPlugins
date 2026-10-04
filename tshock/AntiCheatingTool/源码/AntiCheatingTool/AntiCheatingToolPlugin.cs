using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Microsoft.Xna.Framework;
using TShockAPI;
using Terraria;
using TerrariaApi.Server;

namespace AntiCheatingTool;

[ApiVersion(2, 1)]
public class AntiCheatingToolPlugin : TerrariaPlugin
{
	private const int MaxPlayers = 256;

	private Config _config = new Config();

	private readonly ViolationTracker _summonViolations = new ViolationTracker();

	private readonly ViolationTracker _floodViolations = new ViolationTracker();

	private readonly ZoneTipService _zoneTips = new ZoneTipService();

	private HashSet<int> _projSet = new HashSet<int>();

	private HashSet<int> _npcSet = new HashSet<int>();

	public override string Name => "AntiCheatingTool";

	public override string Author => "yu";

	public override Version Version => new Version(2, 1, 1, 0);

	public override string Description => "出生点保护区拓展版：弹幕/召唤拦截 + 炸弹恢复掉落物 + 防物品洪水 + 多箱保护";

	public Config CurrentConfig => _config;

	public HashSet<int> CurrentProjSet => _projSet;

	public HashSet<int> CurrentNpcSet => _npcSet;

	public ViolationTracker SummonViolations => _summonViolations;

	public ViolationTracker FloodViolations => _floodViolations;

	public AntiCheatingToolPlugin(Main game)
		: base(game)
	{
	}

	public override void Initialize()
	{
		_config = Config.Load();
		RebuildSets();
		ServerApi.Hooks.ServerJoin.Register((TerrariaPlugin)(object)this, (HookHandler<JoinEventArgs>)OnServerJoin);
		ServerApi.Hooks.GamePostInitialize.Register((TerrariaPlugin)(object)this, (HookHandler<EventArgs>)OnGamePostInitialize);
		ServerApi.Hooks.ServerLeave.Register((TerrariaPlugin)(object)this, (HookHandler<LeaveEventArgs>)OnServerLeave);
		ServerApi.Hooks.NetGetData.Register((TerrariaPlugin)(object)this, (HookHandler<GetDataEventArgs>)OnNetGetData);
		ServerApi.Hooks.NpcStrike.Register((TerrariaPlugin)(object)this, (HookHandler<NpcStrikeEventArgs>)OnNpcStrike);
		ServerApi.Hooks.ProjectileAIUpdate.Register((TerrariaPlugin)(object)this, (HookHandler<ProjectileAiUpdateEventArgs>)OnProjectileAIUpdate);
		GetDataHandlers.ItemDrop += new EventHandler<GetDataHandlers.ItemDropEventArgs>(OnItemDrop);
		Commands.ChatCommands.Add(new Command("ac.admin", OnAcCommand, "ac", "anticheat")
		{
			HelpText = "AntiCheatingTool 管理指令",
			HelpDesc = new string[7] { "/ac status —— 看三项开关 + 保护区参数", "/ac reload —— 重载配置（中文配置文件改完不用重启）", "/ac toggle nosummon|antiflood|multichest —— 开关对应功能", "/ac zone test —— 看自己是否在保护区内", "/ac zone radius <格> —— 热改地上半径", "/ac zone depth <格> —— 热改地下深度", "/ac zone sky on|off —— 热改「出生点上空是否全保护」" }
		});
		LogInfo("已加载 v" + ((TerrariaPlugin)this).Version?.ToString() + "，" + ProtectionZone.Describe(_config));
		LogInfo("配置文件: " + Config.FilePath);
		EnsureBoundary();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			ServerApi.Hooks.ServerJoin.Deregister((TerrariaPlugin)(object)this, (HookHandler<JoinEventArgs>)OnServerJoin);
			ServerApi.Hooks.GamePostInitialize.Deregister((TerrariaPlugin)(object)this, (HookHandler<EventArgs>)OnGamePostInitialize);
			ServerApi.Hooks.ServerLeave.Deregister((TerrariaPlugin)(object)this, (HookHandler<LeaveEventArgs>)OnServerLeave);
			ServerApi.Hooks.NetGetData.Deregister((TerrariaPlugin)(object)this, (HookHandler<GetDataEventArgs>)OnNetGetData);
			ServerApi.Hooks.NpcStrike.Deregister((TerrariaPlugin)(object)this, (HookHandler<NpcStrikeEventArgs>)OnNpcStrike);
			ServerApi.Hooks.ProjectileAIUpdate.Deregister((TerrariaPlugin)(object)this, (HookHandler<ProjectileAiUpdateEventArgs>)OnProjectileAIUpdate);
			GetDataHandlers.ItemDrop -= new EventHandler<GetDataHandlers.ItemDropEventArgs>(OnItemDrop);
		}
	}

	public void Reload()
	{
		_config = Config.Load();
		RebuildSets();
		EnsureBoundary();
	}

	private void RebuildSets()
	{
		_projSet = new HashSet<int>(_config.NoSummon.Projs ?? Array.Empty<int>());
		_npcSet = new HashSet<int>(_config.NoSummon.SummonNpcs ?? Array.Empty<int>());
	}

	private void EnsureBoundary()
	{
		if (Main.spawnTileX > 0 && Main.spawnTileY > 0 && BoundaryBuilder.Count == 0)
		{
			BoundaryBuilder.Build(_config);
		}
	}

	private void OnServerJoin(JoinEventArgs args)
	{
		EnsureBoundary();
		int who = args.Who;
		if (who >= 0 && who < 256)
		{
			_summonViolations.Reset(who);
			_floodViolations.Reset(who);
			_zoneTips.Reset(who);
		}
	}

	private void OnServerLeave(LeaveEventArgs args)
	{
		int who = args.Who;
		if (who >= 0 && who < 256)
		{
			_summonViolations.Reset(who);
			_floodViolations.Reset(who);
			_zoneTips.Reset(who);
		}
	}
	private void OnGamePostInitialize(EventArgs args)
	{
		EnsureBoundary();
	}

	private static bool IsBoundaryTileEdit(GetDataEventArgs args)
	{
		if (args.Msg == null)
		{
			return false;
		}
		try
		{
			int payloadLength = Math.Max(args.Length - 1, 0);
			if (args.Index < 0 || payloadLength < 5 || args.Index + payloadLength > args.Msg.readBuffer.Length)
			{
				return false;
			}
			using MemoryStream stream = new MemoryStream(args.Msg.readBuffer, args.Index, payloadLength, writable: false);
			using BinaryReader reader = new BinaryReader(stream);
			reader.ReadByte();
			int x = reader.ReadInt16();
			int y = reader.ReadInt16();
			return BoundaryBuilder.Contains(x, y);
		}
		catch
		{
			return false;
		}
	}
	private void OnNetGetData(GetDataEventArgs args)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Invalid comparison between Unknown and I4
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Invalid comparison between Unknown and I4
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Invalid comparison between Unknown and I4
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Invalid comparison between Unknown and I4
		Config config = _config;
		EnsureBoundary();
		if (args.Msg == null)
		{
			return;
		}
		int whoAmI = args.Msg.whoAmI;
		if (whoAmI < 0 || whoAmI >= TShock.Players.Length)
		{
			return;
		}
		TSPlayer tSPlayer = TShock.Players[whoAmI];
		if (tSPlayer == null || string.IsNullOrEmpty(tSPlayer.Name))
		{
			return;
		}
		if ((int)args.MsgID == 17 && IsBoundaryTileEdit(args))
		{
			((HandledEventArgs)(object)args).Handled = true;
			return;
		}		if ((int)args.MsgID == 13)
		{
			HandleZoneTip(config, tSPlayer);
			return;
		}
		if (config.Features.EnableMultiChestProtect && ((int)args.MsgID == 33 || (int)args.MsgID == 31))
		{
			HandleChestProtection(config, args, tSPlayer);
			if (((HandledEventArgs)(object)args).Handled)
			{
				return;
			}
		}
		if (config.Features.EnableNoSummon && (int)args.MsgID == 61 && ProtectionZone.ContainsTile(tSPlayer.TileX, tSPlayer.TileY, config))
		{
			((HandledEventArgs)(object)args).Handled = true;
			LogWarn(tSPlayer.Name + "试图在出生点保护区内召唤");
			AddViolation(_summonViolations, config.NoSummon.Rule, config.NoSummon.KickTip, tSPlayer.Index);
		}
	}

	private void HandleZoneTip(Config cfg, TSPlayer plr)
	{
		bool inZone = ProtectionZone.ContainsTile(plr.TileX, plr.TileY, cfg);
		if (!_zoneTips.Evaluate(plr.Index, inZone, cfg, out var tipIn))
		{
			return;
		}
		if (tipIn)
		{
			if (cfg.Protection.ShowEnterTip)
			{
				plr.SendInfoMessage(cfg.Protection.EnterTipText);
			}
		}
		else if (cfg.Protection.ShowLeaveTip)
		{
			plr.SendInfoMessage(cfg.Protection.LeaveTipText);
		}
	}

	private static void HandleChestProtection(Config cfg, GetDataEventArgs args, TSPlayer plr)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		int maxOpenChests = cfg.MultiChestProtect.MaxOpenChests;
		if ((int)args.MsgID == 33)
		{
			short num;
			try
			{
				using MemoryStream input = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length);
				using BinaryReader binaryReader = new BinaryReader(input);
				num = binaryReader.ReadInt16();
			}
			catch
			{
				return;
			}
			if (num == -1)
			{
				return;
			}
		}
		if (maxOpenChests <= 0 || (maxOpenChests == 1 && plr.ActiveChest != -1))
		{
			plr.ActiveChest = -1;
			plr.SendData((PacketTypes)33, "", -1);
			((HandledEventArgs)(object)args).Handled = true;
		}
	}

	private void OnNpcStrike(NpcStrikeEventArgs args)
	{
		Config config = _config;
		if (!config.Features.EnableNoSummon || args.Npc == null || !args.Npc.active || !_npcSet.Contains(args.Npc.netID) || args.Player == null)
		{
			return;
		}
		int whoAmI = ((Entity)args.Player).whoAmI;
		if (whoAmI >= 0 && whoAmI < TShock.Players.Length)
		{
			TSPlayer tSPlayer = TShock.Players[whoAmI];
			if (tSPlayer != null && !string.IsNullOrEmpty(tSPlayer.Name) && ProtectionZone.ContainsTile(tSPlayer.TileX, tSPlayer.TileY, config))
			{
				args.Damage = 0;
				((HandledEventArgs)(object)args).Handled = true;
				args.Npc.active = false;
				LogWarn(tSPlayer.Name + "试图在出生点保护区内召唤");
				AddViolation(_summonViolations, config.NoSummon.Rule, config.NoSummon.KickTip, tSPlayer.Index);
			}
		}
	}

	private void OnProjectileAIUpdate(ProjectileAiUpdateEventArgs args)
	{
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		Config config = _config;
		if (!config.Features.EnableNoSummon)
		{
			return;
		}
Projectile projectile = args.Projectile;
		if (projectile == null)
		{
			return;
		}
		int projectileTileX = (int)(((Entity)projectile).Center.X / 16f);
		int projectileTileY = (int)(((Entity)projectile).Center.Y / 16f);
		if (projectile.active && _projSet.Contains(projectile.type) && (ProtectionZone.ContainsWorld(((Entity)projectile).position.X, ((Entity)projectile).position.Y, config) || BoundaryBuilder.IsNearBoundary(projectileTileX, projectileTileY, 12)))
		{
			((HandledEventArgs)(object)args).Handled = true;
			int owner = projectile.owner;
			TSPlayer tSPlayer = ((owner >= 0 && owner < TShock.Players.Length) ? TShock.Players[owner] : null);
			bool num = tSPlayer != null && !string.IsNullOrEmpty(tSPlayer.Name);
			bool flag = false;
			if (config.BombHandling.Enabled && string.Equals(config.BombHandling.Mode, "RevertItem", StringComparison.OrdinalIgnoreCase) && BombRevert.TryGetItemId(config, projectile.type, out var itemId))
			{
				BombRevert.RevertToItem(projectile, itemId);
				flag = true;
			}
			if (!flag)
			{
				BombRevert.RemoveSilently(projectile);
			}
			int num2 = (int)(((Entity)projectile).Center.X / 16f);
			int num3 = (int)(((Entity)projectile).Center.Y / 16f);
			string text = (num ? tSPlayer.Name : "未知");
			string text2 = (flag ? "已把炸弹恢复成掉落物" : "已拦截该弹幕");
			_003C_003Ey__InlineArray4<object> buffer = default;
			buffer[0] = text;
			buffer[1] = num2;
			buffer[2] = num3;
			buffer[3] = text2;
			LogWarn(string.Format("{0} 疑似在保护区内({1},{2})使用炸药，{3}", (ReadOnlySpan<object?>)buffer));
			if (num)
			{
				AddViolation(_summonViolations, config.NoSummon.Rule, config.NoSummon.KickTip, tSPlayer.Index);
			}
		}
	}

	private void OnItemDrop(object sender, GetDataHandlers.ItemDropEventArgs e)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		Config config = _config;
		if (!config.Features.EnableAntiItemFlood)
		{
			return;
		}
		TSPlayer player = e.Player;
		if (player != null && !string.IsNullOrEmpty(player.Name))
		{
			Vector2 position = e.Position;
			Player tPlayer = player.TPlayer;
			if (FloodCheck.ShouldFlag(config, e.ID, position, ((Entity)tPlayer).Center, ((Entity)tPlayer).velocity))
			{
				LogWarn($"玩家{player.Name}疑似使用物品洪水攻击,物品为{e.Type},坐标{position}");
				AddViolation(_floodViolations, config.AntiItemFlood.Rule, config.AntiItemFlood.KickTip, player.Index);
				e.Handled = true;
			}
		}
	}

	private void OnAcCommand(CommandArgs args)
	{
		TSPlayer player = args.Player;
		List<string> parameters = args.Parameters;
		if (parameters.Count == 0)
		{
			ShowHelp(player);
			return;
		}
		switch (parameters[0].ToLowerInvariant())
		{
		case "help":
			ShowHelp(player);
			break;
		case "reload":
			Reload();
			player.SendSuccessMessage("AntiCheatingTool 配置已重载。" + ProtectionZone.Describe(_config));
			break;
		case "status":
			ShowStatus(player);
			break;
		case "toggle":
			ToggleFeature(player, (parameters.Count > 1) ? parameters[1].ToLowerInvariant() : "");
			break;
		case "zone":
			ZoneCommand(player, parameters);
			break;
		default:
			ShowHelp(player);
			break;
		}
	}

	private static void ShowHelp(TSPlayer p)
	{
		p.SendInfoMessage("==== AntiCheatingTool（出生点保护区） ====");
		p.SendInfoMessage("/ac status —— 看三项开关 + 保护区参数");
		p.SendInfoMessage("/ac reload —— 重载中文配置");
		p.SendInfoMessage("/ac toggle nosummon|antiflood|multichest —— 开关对应功能");
		p.SendInfoMessage("/ac zone test —— 看自己是否在保护区内");
		p.SendInfoMessage("/ac zone radius <格> —— 热改地上半径");
		p.SendInfoMessage("/ac zone depth <格> —— 热改地下深度");
		p.SendInfoMessage("/ac zone sky on|off —— 出生点上空是否全保护");
	}

	private void ShowStatus(TSPlayer p)
	{
		Config config = _config;
		p.SendInfoMessage("==== AntiCheatingTool 状态 ====");
		p.SendInfoMessage("弹幕召唤拦截: " + OnOff(config.Features.EnableNoSummon) + " | 防物品洪水: " + OnOff(config.Features.EnableAntiItemFlood) + " | 多箱保护: " + OnOff(config.Features.EnableMultiChestProtect));
		p.SendInfoMessage("保护区: " + ProtectionZone.Describe(config));
		p.SendInfoMessage("弹幕拦截表: " + _projSet.Count + " 个 | 召唤物拦截表: " + _npcSet.Count + " 个");
		TSPlayer tSPlayer = p;
		string text;
		if (config.BombHandling.Enabled)
		{
			text = (string.Equals(config.BombHandling.Mode, "RevertItem", StringComparison.OrdinalIgnoreCase) ? "恢复掉落物" : "封印弹幕");
		}
		else
		{
			text = "已关闭";
		}
		tSPlayer.SendInfoMessage("炸弹处理: " + text);
		p.SendInfoMessage("违规计数: 召唤拦截 " + (config.NoSummon.Rule.Enabled ? (config.NoSummon.Rule.WindowSeconds + "秒内" + config.NoSummon.Rule.Threshold + "次") : "关闭") + " | 物品洪水 " + (config.AntiItemFlood.Rule.Enabled ? (config.AntiItemFlood.Rule.WindowSeconds + "秒内" + config.AntiItemFlood.Rule.Threshold + "次") : "关闭"));
	}

	private void ToggleFeature(TSPlayer p, string name)
	{
		Config config = _config;
		string text;
		switch (name)
		{
		case "nosummon":
			config.Features.EnableNoSummon = !config.Features.EnableNoSummon;
			text = "弹幕召唤拦截";
			break;
		case "antiflood":
			config.Features.EnableAntiItemFlood = !config.Features.EnableAntiItemFlood;
			text = "防物品洪水";
			break;
		case "multichest":
			config.Features.EnableMultiChestProtect = !config.Features.EnableMultiChestProtect;
			text = "多箱保护";
			break;
		default:
			p.SendErrorMessage("用法: /ac toggle nosummon|antiflood|multichest");
			return;
		}
		bool flag;
		if (name == "nosummon")
		{
			flag = config.Features.EnableNoSummon;
		}
		else
		{
			flag = ((name == "antiflood") ? config.Features.EnableAntiItemFlood : config.Features.EnableMultiChestProtect);
		}
		config.Save();
		p.SendSuccessMessage(text + " 已" + (flag ? "开启" : "关闭") + "，并写回配置文件。");
	}

	private void ZoneCommand(TSPlayer p, List<string> parms)
	{
		Config config = _config;
		if (parms.Count < 2)
		{
			p.SendErrorMessage("用法: /ac zone test|radius <格>|depth <格>|sky on|off");
			return;
		}
		string text = parms[1].ToLowerInvariant();
		switch (text)
		{
		case "test":
		{
			bool flag = ProtectionZone.ContainsTile(p.TileX, p.TileY, config);
			_003C_003Ey__InlineArray4<object> buffer = default;
			buffer[0] = p.TileX;
			buffer[1] = p.TileY;
			buffer[2] = (flag ? "在保护区内" : "在保护区外");
			buffer[3] = ProtectionZone.Describe(config);
			p.SendInfoMessage(string.Format("你当前在 ({0},{1})，{2}。{3}", (ReadOnlySpan<object?>)buffer));
			break;
		}
		case "sky":
			if (parms.Count < 3)
			{
				p.SendErrorMessage("用法: /ac zone sky on|off");
				break;
			}
			config.Protection.SkyToSurface = parms[2].ToLowerInvariant() == "on";
			config.Save();
			p.SendSuccessMessage("出生点上空全保护: " + OnOff(config.Protection.SkyToSurface));
			break;
		case "radius":
		case "depth":
		{
			if (parms.Count < 3)
			{
				p.SendErrorMessage("用法: /ac zone " + text + " <格>");
				break;
			}
			if (!int.TryParse(parms[2], out var result) || result < 0)
			{
				p.SendErrorMessage("请输入 0 或正整数（单位：格）");
				break;
			}
			if (text == "radius")
			{
				config.Protection.SpawnXRadius = result;
			}
			else
			{
				config.Protection.UndergroundDepth = result;
			}
config.Save();
			BoundaryBuilder.Build(config);
			p.SendSuccessMessage("已更新: " + ProtectionZone.Describe(config));
			break;
		}
		default:
			p.SendErrorMessage("用法: /ac zone test|radius <格>|depth <格>|sky on|off");
			break;
		}
	}

	private void AddViolation(ViolationTracker tracker, ViolationRule rule, string kickTip, int index)
	{
		if (!rule.Enabled)
		{
			return;
		}
		int num = tracker.Add(index, rule.WindowSeconds);
		if (num < rule.Threshold)
		{
			return;
		}
		tracker.Reset(index);
		if (index < 0 || index >= TShock.Players.Length)
		{
			return;
		}
		TSPlayer tSPlayer = TShock.Players[index];
		if (tSPlayer == null || string.IsNullOrEmpty(tSPlayer.Name))
		{
			return;
		}
		try
		{
			tSPlayer.Kick(tSPlayer.Name + kickTip + ",已踢出", force: true);
			_003C_003Ey__InlineArray4<object> buffer = default;
			buffer[0] = tSPlayer.Name;
			buffer[1] = kickTip;
			buffer[2] = rule.WindowSeconds;
			buffer[3] = num;
			LogWarn(string.Format("{0} {1}（{2}秒内 {3} 次），已踢出", (ReadOnlySpan<object?>)buffer));
		}
		catch (Exception ex)
		{
			LogWarn("踢人失败: " + ex.Message);
		}
	}

	private static string OnOff(bool b)
	{
		if (!b)
		{
			return "关";
		}
		return "开";
	}

	private static void LogInfo(string msg)
	{
		try
		{
			if (TShock.Log != null)
			{
				TShock.Log.ConsoleInfo("[AntiCheatingTool] " + msg);
			}
		}
		catch
		{
		}
	}

	private static void LogWarn(string msg)
	{
		try
		{
			if (TShock.Log != null)
			{
				TShock.Log.Warn("[AntiCheatingTool] " + msg);
			}
		}
		catch
		{
		}
	}
}












