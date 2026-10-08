using System;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using TShockAPI;
using TShockAPI.Hooks;
using Terraria;
using Terraria.GameContent.Events;
using TerrariaApi.Server;

namespace PeaceMode;

[ApiVersion(2, 1)]
public class PeaceModePlugin : TerrariaPlugin
{
	private static Config _config = new Config();

	private int _updateTick;

	public override string Name => "PeaceMode";

	public override string Author => "lmx12330";

	public override string Description => "和平模式：禁止世界中全部 NPC 与事件生成";

	public override Version Version => new Version(1, 0, 0, 0);

	public static bool Active { get; private set; }

	public PeaceModePlugin(Main game)
		: base(game)
	{
	}

	public override void Initialize()
	{
		_config = Config.Read();
		Commands.ChatCommands.Add(new Command("peacemode.admin", PeaceCommand, "peace", "和平"));
		ServerApi.Hooks.NpcSpawn.Register((TerrariaPlugin)(object)this, (HookHandler<NpcSpawnEventArgs>)OnNpcSpawn);
		ServerApi.Hooks.NpcTransform.Register((TerrariaPlugin)(object)this, (HookHandler<NpcTransformationEventArgs>)OnNpcTransform);
		ServerApi.Hooks.GameUpdate.Register((TerrariaPlugin)(object)this, (HookHandler<EventArgs>)OnGameUpdate);
		GeneralHooks.ReloadEvent += OnReload;
		if (_config.Enabled)
		{
			SetActive(value: true);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			Commands.ChatCommands.RemoveAll((Command cmd) => cmd.CommandDelegate == new CommandDelegate(PeaceCommand));
			ServerApi.Hooks.NpcSpawn.Deregister((TerrariaPlugin)(object)this, (HookHandler<NpcSpawnEventArgs>)OnNpcSpawn);
			ServerApi.Hooks.NpcTransform.Deregister((TerrariaPlugin)(object)this, (HookHandler<NpcTransformationEventArgs>)OnNpcTransform);
			ServerApi.Hooks.GameUpdate.Deregister((TerrariaPlugin)(object)this, (HookHandler<EventArgs>)OnGameUpdate);
			GeneralHooks.ReloadEvent -= OnReload;
		}
		// 反编译把基类受保护方法写成了强制转换调用，这里改回直接调用基类实现
		base.Dispose(disposing);
	}

	private void PeaceCommand(CommandArgs args)
	{
		if (args.Parameters.Count == 0)
		{
			args.Player.SendInfoMessage("和平模式当前: " + (Active ? "[c/32FF82:已开启]" : "[c/FF514A:已关闭]"));
			args.Player.SendInfoMessage("用法: /peace on | off | status | reload");
			return;
		}
		switch (args.Parameters[0].ToLower())
		{
		case "on":
			SetActive(value: true);
			args.Player.SendSuccessMessage("和平模式已开启：禁止全部 NPC 与事件生成");
			break;
		case "off":
			SetActive(value: false);
			args.Player.SendSuccessMessage("和平模式已关闭");
			break;
		case "status":
			args.Player.SendInfoMessage("和平模式当前: " + (Active ? "[c/32FF82:已开启]" : "[c/FF514A:已关闭]"));
			args.Player.SendInfoMessage($"  禁止事件: {(_config.BanEvents ? "是" : "否")}  禁止下雨: {(_config.BanRain ? "是" : "否")}  禁止陨石: {(_config.BanMeteor ? "是" : "否")}");
			args.Player.SendInfoMessage("  城镇 NPC: " + (_config.IncludeTownNpcs ? "一并禁止" : "保留"));
			break;
		case "reload":
			Reload();
			args.Player.SendSuccessMessage("和平模式配置已重新加载");
			break;
		default:
			args.Player.SendErrorMessage("未知子命令。可用: on, off, status, reload");
			break;
		}
	}

	private static void SetActive(bool value)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		Active = value;
		if (value)
		{
			ClearEvents();
			if (_config.ClearExistingNpcsOnEnable)
			{
				ClearExistingNpcs();
			}
		}
		TShock.Utils.Broadcast(Active ? "[c/32FF82:【和平模式】已开启] 世界不再生成 NPC 与事件" : "[c/FF514A:【和平模式】已关闭] 世界恢复原状", Color.White);
	}

	private void Reload()
	{
		_config = Config.Read();
		if (_config.Enabled != Active)
		{
			SetActive(_config.Enabled);
		}
	}

	private void OnReload(ReloadEventArgs args)
	{
		Reload();
		args.Player?.SendSuccessMessage("和平模式配置已重新加载");
	}

	private void OnNpcSpawn(NpcSpawnEventArgs args)
	{
		if (Active && !((HandledEventArgs)(object)args).Handled)
		{
			NPC val = Main.npc[args.NpcId];
			if (val != null && (!val.townNPC || _config.IncludeTownNpcs))
			{
				((HandledEventArgs)(object)args).Handled = true;
				val.active = false;
				TSPlayer.All.SendData((PacketTypes)23, "", args.NpcId);
			}
		}
	}

	private void OnNpcTransform(NpcTransformationEventArgs args)
	{
		if (Active && !((HandledEventArgs)(object)args).Handled)
		{
			NPC val = Main.npc[args.NpcId];
			if (val != null && (!val.townNPC || _config.IncludeTownNpcs))
			{
				val.active = false;
				TSPlayer.All.SendData((PacketTypes)23, "", args.NpcId);
			}
		}
	}

	private static void ClearExistingNpcs()
	{
		int num = 0;
		for (int i = 0; i < Main.npc.Length; i++)
		{
			NPC val = Main.npc[i];
			if (val != null && val.active && (!val.townNPC || _config.IncludeTownNpcs))
			{
				val.active = false;
				val.life = 0;
				TSPlayer.All.SendData((PacketTypes)23, "", i);
				num++;
			}
		}
		if (num > 0)
		{
			TShock.Log.ConsoleInfo($"[PeaceMode] 已清除场上 {num} 个 NPC");
		}
	}

	private void OnGameUpdate(EventArgs args)
	{
		if (Active && ++_updateTick % 60 == 0)
		{
			ClearEvents();
		}
	}

	private static void ClearEvents()
	{
		bool flag = false;
		if (_config.BanEvents)
		{
			if (Main.invasionType != 0 || Main.invasionSize != 0)
			{
				Main.invasionType = 0;
				Main.invasionSize = 0;
				Main.invasionDelay = 0;
				flag = true;
			}
			if (Main.bloodMoon)
			{
				Main.bloodMoon = false;
				flag = true;
			}
			if (Main.eclipse)
			{
				Main.eclipse = false;
				flag = true;
			}
			if (Main.pumpkinMoon)
			{
				Main.pumpkinMoon = false;
				flag = true;
			}
			if (Main.snowMoon)
			{
				Main.snowMoon = false;
				flag = true;
			}
			if (Main.slimeRain)
			{
				Main.StopSlimeRain(true);
				flag = true;
			}
			if (DD2Event.Ongoing)
			{
				DD2Event.StopInvasion(false);
				flag = true;
			}
			if (Sandstorm.Happening)
			{
				Sandstorm.StopSandstorm();
				flag = true;
			}
		}
		if (_config.BanRain && Main.raining)
		{
			Main.StopRain(false);
			flag = true;
		}
		if (_config.BanMeteor && WorldGen.spawnMeteor)
		{
			WorldGen.spawnMeteor = false;
			flag = true;
		}
		if (flag)
		{
			TSPlayer.All.SendData((PacketTypes)7);
		}
	}
}
