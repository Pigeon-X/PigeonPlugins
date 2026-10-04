using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using TShockAPI;
using TShockAPI.Configuration;
using Terraria;
using Terraria.GameContent.NetModules;
using Terraria.Localization;
using Terraria.Net;
using TerrariaApi.Server;

namespace 称号插件;

public static class 称号插件
{
	[CompilerGenerated]
	private static class _003C_003EO
	{
		public static HookHandler<EventArgs> _003C0_003E__OnInitialize;

		public static HookHandler<ServerChatEventArgs> _003C1_003E__聊天;

		public static HookHandler<GreetPlayerEventArgs> _003C2_003E__OnJoin;

		public static HookHandler<LeaveEventArgs> _003C3_003E__OnLeave;

		public static CommandDelegate _003C4_003E__指令1;

		public static CommandDelegate _003C5_003E__重载;

		public static CommandDelegate _003C6_003E__指令2;
	}

	public static Config 配置 = new Config();

	public static Dictionary<string, Config.聊天信息> 称号信息 = new Dictionary<string, Config.聊天信息>();

	public static bool 启用远程配置 = false;

	public static string path = "tshock/Chrome.Title.json";

	private static Command[] 命令列表 = Array.Empty<Command>();

	private static readonly HttpClient client = new HttpClient();

	public static Config.聊天信息 获取聊天信息(string 玩家名)
	{
		if (!称号信息.TryGetValue(玩家名, out Config.聊天信息 value))
		{
			value = new Config.聊天信息
			{
				前前缀 = "",
				前缀 = null,
				角色名 = null,
				后缀 = null,
				后后缀 = ""
			};
			称号信息[玩家名] = value;
		}
		return value;
	}

	public static void 挂接(TerrariaPlugin owner)
	{
		ServerApi.Hooks.GameInitialize.Register(owner, (HookHandler<EventArgs>)OnInitialize);
		ServerApi.Hooks.ServerChat.Register(owner, (HookHandler<ServerChatEventArgs>)聊天);
		ServerApi.Hooks.NetGreetPlayer.Register(owner, (HookHandler<GreetPlayerEventArgs>)OnJoin);
		ServerApi.Hooks.ServerLeave.Register(owner, (HookHandler<LeaveEventArgs>)OnLeave);
		Config.GetConfig();
		Reload();
	}

	public static void 摘除(TerrariaPlugin owner)
	{
		ServerApi.Hooks.GameInitialize.Deregister(owner, (HookHandler<EventArgs>)OnInitialize);
		ServerApi.Hooks.ServerChat.Deregister(owner, (HookHandler<ServerChatEventArgs>)聊天);
		ServerApi.Hooks.NetGreetPlayer.Deregister(owner, (HookHandler<GreetPlayerEventArgs>)OnJoin);
		ServerApi.Hooks.ServerLeave.Deregister(owner, (HookHandler<LeaveEventArgs>)OnLeave);
		Command[] array = 命令列表;
		foreach (Command item in array)
		{
			Commands.ChatCommands.Remove(item);
		}
		命令列表 = Array.Empty<Command>();
	}

	private static void OnInitialize(EventArgs args)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected Obj, but got Unknown
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected Obj, but got Unknown
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Expected Obj, but got Unknown
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Expected Obj, but got Unknown
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected Obj, but got Unknown
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Expected Obj, but got Unknown
		Command[] array = new Command[3];
		CommandDelegate val = _003C_003EO._003C4_003E__指令1;
		if (val == null)
		{
			CommandDelegate val2 = 指令1;
			_003C_003EO._003C4_003E__指令1 = val2;
			val = val2;
		}
		array[0] = new Command("称号", val, new string[3] { "称号", "给称号", "ch" });
		CommandDelegate val3 = _003C_003EO._003C5_003E__重载;
		if (val3 == null)
		{
			CommandDelegate val4 = 重载;
			_003C_003EO._003C5_003E__重载 = val4;
			val3 = val4;
		}
		array[1] = new Command("称号", val3, new string[2] { "reload", "称号重载" });
		CommandDelegate val5 = _003C_003EO._003C6_003E__指令2;
		if (val5 == null)
		{
			CommandDelegate val6 = 指令2;
			_003C_003EO._003C6_003E__指令2 = val6;
			val5 = val6;
		}
		array[2] = new Command("改称号", val5, new string[2] { "改称号", "gch" });
		命令列表 = array;
		Commands.ChatCommands.AddRange(命令列表);
	}

	private static void OnLeave(LeaveEventArgs args)
	{
		TSPlayer val = TShock.Players[args.Who];
		if (val != null && 称号信息.ContainsKey(val.Name))
		{
			称号信息.Remove(val.Name);
		}
	}

	private static void OnJoin(GreetPlayerEventArgs args)
	{
		TSPlayer val = TShock.Players[args.Who];
		if (!称号信息.ContainsKey(val.Name))
		{
			称号信息.Add(val.Name, new Config.聊天信息
			{
				前前缀 = "",
				前缀 = null,
				角色名 = null,
				后缀 = null,
				后后缀 = ""
			});
		}
	}

	private static void 指令1(CommandArgs args)
	{
		try
		{
			if (启用远程配置)
			{
				args.Player.SendInfoMessage("远程配置开启中，该指令失效！");
				return;
			}
			string 玩家用户名 = args.Parameters[0];
			string text = "";
			if (args.Parameters.Count == 1)
			{
				查称号(args, 玩家用户名);
				return;
			}
			string text2 = args.Parameters[1];
			if (args.Parameters[1] != "sc" && args.Parameters[1] != "删除" && args.Parameters[1] != "qqz" && args.Parameters[1] != "前前缀" && args.Parameters[1] != "qz" && args.Parameters[1] != "前缀" && args.Parameters[1] != "js" && args.Parameters[1] != "角色名" && args.Parameters[1] != "jsm" && args.Parameters[1] != "角色" && args.Parameters[1] != "hz" && args.Parameters[1] != "后缀" && args.Parameters[1] != "" && args.Parameters[1] != "后后缀")
			{
				int count = args.Parameters.Count - 1;
				text = string.Join(" ", args.Parameters.GetRange(1, count));
				if (text.Length <= 配置.称号最大字符数)
				{
					设置称号(args, 玩家用户名, "前前缀", text);
				}
				else if (配置.是否允许管理绕过最大字符数检测)
				{
					设置称号(args, 玩家用户名, "前前缀", text);
				}
				else
				{
					args.Player.SendInfoMessage("设置的称号过长！");
				}
				return;
			}
			if (text2 != "sc" && text2 != "删除")
			{
				int count2 = args.Parameters.Count - 2;
				text = string.Join(" ", args.Parameters.GetRange(2, count2));
			}
			if (text.Length <= 配置.称号最大字符数)
			{
				设置称号(args, 玩家用户名, text2, text);
			}
			else if (配置.是否允许管理绕过最大字符数检测)
			{
				设置称号(args, 玩家用户名, text2, text);
			}
			else
			{
				args.Player.SendInfoMessage("设置的称号过长！");
			}
		}
		catch
		{
			args.Player.SendInfoMessage("设置指令:/称号 玩家名 称号位置 称号名");
			args.Player.SendInfoMessage("删除指令:/称号 玩家名 删除 ");
			args.Player.SendInfoMessage("称号位置:前前缀、前缀、角色名、后缀、后后缀");
			args.Player.SendInfoMessage("称号设置中，前缀、名称、后缀设置为null则与玩家当前用户组的前后缀一致");
		}
	}

	private static void 指令2(CommandArgs args)
	{
		if (启用远程配置)
		{
			args.Player.SendInfoMessage("远程配置开启中，该指令失效！");
			return;
		}
		if (!配置.称号设置.Exists((Config.名称 a) => a.玩家用户名 == args.Player.Name) && !配置.是否允许无称号玩家给自己称号)
		{
			args.Player.SendInfoMessage("您还没有称号。");
			return;
		}
		try
		{
			string name = args.Player.Name;
			string text = args.Parameters[0];
			string text2 = "";
			if ((text != "sc" && text != "删除") || 配置.是否允许无称号玩家给自己称号)
			{
				int count = args.Parameters.Count - 1;
				text2 = string.Join(" ", args.Parameters.GetRange(1, count));
				if (text2.Length <= 配置.称号最大字符数)
				{
					设置称号(args, name, text, text2);
				}
				else if (配置.是否允许管理绕过最大字符数检测 && args.Player.HasPermission("称号"))
				{
					设置称号(args, name, text, text2);
				}
				else
				{
					args.Player.SendInfoMessage("设置的称号过长！");
				}
			}
			else
			{
				args.Player.SendInfoMessage("我不推荐你这样做。");
			}
		}
		catch
		{
			args.Player.SendInfoMessage("修改指令:/称号 称号位置 称号名");
			args.Player.SendInfoMessage("称号位置:前前缀、前缀、角色名、后缀、后后缀");
			args.Player.SendInfoMessage("用于修改自己的称号");
		}
	}

	public static void 查称号(CommandArgs args, string? 玩家用户名)
	{
		if (配置.称号设置.Exists((Config.名称 a) => a.玩家用户名 == 玩家用户名))
		{
			string text = "";
			string text2 = null;
			string text3 = null;
			string text4 = null;
			string text5 = "";
			Config.名称 名称 = 配置.称号设置.Find((Config.名称 s) => s.玩家用户名 == 玩家用户名);
			if (名称 == null)
			{
				args.Player.SendInfoMessage(玩家用户名 + "没有称号。");
				return;
			}
			text = 名称.前前缀;
			text5 = 名称.后后缀;
			text2 = 名称.前缀;
			text3 = 名称.角色名;
			text4 = 名称.后缀;
			args.Player.SendInfoMessage($"{玩家用户名}的称号为:\n前前缀：{text}\n前缀：{text2}\n角色名：{text3}\n后缀：{text4}\n后后缀：{text5}");
		}
		else
		{
			args.Player.SendInfoMessage(玩家用户名 + "没有称号。");
		}
	}

	public static void 设置称号(CommandArgs args, string 玩家用户名, string? 称号位置, string? 称号名)
	{
		string text = "";
		string text2 = null;
		string text3 = null;
		string text4 = null;
		string text5 = "";
		if (配置.称号设置.Exists((Config.名称 s) => s.玩家用户名 == 玩家用户名))
		{
			Config.名称? 名称 = 配置.称号设置.Find((Config.名称 s) => s.玩家用户名 == 玩家用户名);
			text = 名称.前前缀;
			text2 = 名称.前缀;
			text3 = 名称.角色名;
			text4 = 名称.后缀;
			text5 = 名称.后后缀;
		}
		if (称号位置 != null)
		{
			int length = 称号位置.Length;
			if (length != 2)
			{
				if (length == 3)
				{
					char c = 称号位置[0];
					if ((uint)c <= 113u)
					{
						if (c != 'h')
						{
							if (c != 'j')
							{
								if (c == 'q' && 称号位置 == "qqz")
								{
									goto IL_0275;
								}
							}
							else if (称号位置 == "jsm")
							{
								goto IL_027d;
							}
						}
						else if (称号位置 == "hhz")
						{
							goto IL_0286;
						}
					}
					else if (c != '前')
					{
						if (c != '后')
						{
							if (c == '角' && 称号位置 == "角色名")
							{
								goto IL_027d;
							}
						}
						else if (称号位置 == "后后缀")
						{
							goto IL_0286;
						}
					}
					else if (称号位置 == "前前缀")
					{
						goto IL_0275;
					}
				}
			}
			else
			{
				char c = 称号位置[0];
				if ((uint)c <= 115u)
				{
					if ((uint)c <= 106u)
					{
						if (c != 'h')
						{
							if (c == 'j' && 称号位置 == "js")
							{
								goto IL_027d;
							}
						}
						else if (称号位置 == "hz")
						{
							goto IL_0281;
						}
					}
					else if (c != 'q')
					{
						if (c == 's' && 称号位置 == "sc")
						{
							goto IL_028b;
						}
					}
					else if (称号位置 == "qz")
					{
						goto IL_0279;
					}
				}
				else if ((uint)c <= 21069u)
				{
					if (c != '删')
					{
						if (c == '前' && 称号位置 == "前缀")
						{
							goto IL_0279;
						}
					}
					else if (称号位置 == "删除")
					{
						goto IL_028b;
					}
				}
				else if (c != '后')
				{
					if (c == '角' && 称号位置 == "角色")
					{
						goto IL_027d;
					}
				}
				else if (称号位置 == "后缀")
				{
					goto IL_0281;
				}
			}
		}
		args.Player.SendInfoMessage("设置指令:/称号 玩家名 称号位置 称号名");
		args.Player.SendInfoMessage("删除指令:/称号 玩家名 删除");
		args.Player.SendInfoMessage("称号位置:前前缀、前缀、角色名、后缀、后后缀");
		return;
		IL_0279:
		text2 = 称号名;
		goto IL_02e8;
		IL_0275:
		text = 称号名;
		goto IL_02e8;
		IL_0286:
		text5 = 称号名;
		goto IL_02e8;
		IL_0281:
		text4 = 称号名;
		goto IL_02e8;
		IL_027d:
		text3 = 称号名;
		goto IL_02e8;
		IL_028b:
		Remove(玩家用户名);
		args.Player.SendInfoMessage("已删除玩家：" + 玩家用户名 + "的称号");
		return;
		IL_02e8:
		Add(玩家用户名, text, text2, text3, text4, text5);
		args.Player.SendInfoMessage($"设置成功\n{玩家用户名}的称号为:\n前前缀：{text}\n前缀：{text2}\n角色名：{text3}\n后缀：{text4}\n后后缀：{text5}");
	}

	private static void 重载(CommandArgs args)
	{
		try
		{
			Reload();
		}
		catch
		{
			((TSPlayer)TSPlayer.Server).SendErrorMessage("[Chrome.Title]配置文件读取错误");
		}
	}

	public static async void Reload()
	{
		try
		{
			配置 = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Path.Combine((string)path)));
			File.WriteAllText(path, JsonConvert.SerializeObject((object)配置, (Formatting)1));
			if (配置.启用远程配置)
			{
				配置 = JsonConvert.DeserializeObject<Config>(await client.GetStringAsync(配置.远程配置接口));
				启用远程配置 = true;
			}
			else
			{
				启用远程配置 = false;
			}
		}
		catch
		{
			((TSPlayer)TSPlayer.Server).SendErrorMessage("[称号插件]配置文件读取错误");
		}
	}

	public static void Remove(string 玩家用户名)
	{
		配置.称号设置.RemoveAll((Config.名称 s) => s.玩家用户名 == 玩家用户名);
		File.WriteAllText(path, JsonConvert.SerializeObject((object)配置, (Formatting)1));
	}

	public static void Add(string 玩家用户名, string? 前前缀, string? 前缀, string? 角色名, string? 后缀, string? 后后缀)
	{
		if (配置.称号设置.Exists((Config.名称 s) => s.玩家用户名 == 玩家用户名))
		{
			Config.名称? 名称 = 配置.称号设置.Find((Config.名称 s) => s.玩家用户名 == 玩家用户名);
			名称.前前缀 = 前前缀;
			名称.前缀 = 前缀;
			名称.角色名 = 角色名;
			名称.后缀 = 后缀;
			名称.后后缀 = 后后缀;
		}
		else
		{
			配置.称号设置.Add(new Config.名称
			{
				玩家用户名 = 玩家用户名,
				前前缀 = 前前缀,
				前缀 = 前缀,
				角色名 = 角色名,
				后缀 = 后缀,
				后后缀 = 后后缀
			});
		}
		File.WriteAllText(path, JsonConvert.SerializeObject((object)配置, (Formatting)1));
	}

	private static void 聊天(ServerChatEventArgs args)
	{
		if (!配置.是否开启称号插件 || !聊天检测(args) || ((HandledEventArgs)(object)args).Handled)
		{
			return;
		}
		TSPlayer plr = TShock.Players[args.Who];
		if (!称号信息.ContainsKey(plr.Name))
		{
			称号信息.Add(plr.Name, new Config.聊天信息
			{
				前前缀 = "",
				前缀 = null,
				角色名 = null,
				后缀 = null,
				后后缀 = ""
			});
		}
		Config.聊天信息 value;
		if (配置.称号设置.Exists((Config.名称 a) => a.玩家用户名 == plr.Name))
		{
			Config.名称 名称 = 配置.称号设置.Find((Config.名称 s) => s.玩家用户名 == plr.Name);
			Config.聊天信息 聊天信息 = 称号信息[名称.玩家用户名];
			string 前前缀;
			if (名称.前前缀 == null || 名称.前前缀 == "")
			{
				Config.聊天信息 聊天信息2 = 聊天信息;
				if (聊天信息2.前前缀 == null)
				{
					聊天信息2.前前缀 = "";
				}
				前前缀 = 聊天信息.前前缀;
			}
			else
			{
				前前缀 = 名称.前前缀;
			}
			string 前缀 = 名称.前缀 ?? 聊天信息.前缀 ?? plr.Group.Prefix;
			string 角色名 = 名称.角色名 ?? 聊天信息.角色名 ?? plr.Name;
			string 后缀 = 名称.后缀 ?? 聊天信息.后缀 ?? plr.Group.Suffix;
			string 后后缀;
			if (名称.后后缀 == null || 名称.后后缀 == "")
			{
				Config.聊天信息 聊天信息2 = 聊天信息;
				if (聊天信息2.后后缀 == null)
				{
					聊天信息2.后后缀 = "";
				}
				后后缀 = 聊天信息.后后缀;
			}
			else
			{
				后后缀 = 名称.后后缀;
			}
			发送聊天(args, args.Text, 前前缀, 前缀, 角色名, 后缀, 后后缀, 名称.颜色);
		}
		else if (称号信息.TryGetValue(plr.Name, out value))
		{
			Config.聊天信息 聊天信息2 = value;
			if (聊天信息2.前前缀 == null)
			{
				聊天信息2.前前缀 = "";
			}
			string 前前缀2 = value.前前缀;
			string 前缀2 = value.前缀 ?? plr.Group.Prefix;
			string 角色名2 = value.角色名 ?? plr.Name;
			string 后缀2 = value.后缀 ?? plr.Group.Suffix;
			聊天信息2 = value;
			if (聊天信息2.后后缀 == null)
			{
				聊天信息2.后后缀 = "";
			}
			string 后后缀2 = value.后后缀;
			发送聊天(args, args.Text, 前前缀2, 前缀2, 角色名2, 后缀2, 后后缀2, value.颜色);
		}
		else
		{
			发送聊天(args, args.Text, "", plr.Group.Prefix, plr.Name, plr.Group.Suffix, "", null);
			称号信息.Add(plr.Name, new Config.聊天信息
			{
				前前缀 = "",
				前缀 = null,
				角色名 = null,
				后缀 = null,
				后后缀 = ""
			});
		}
	}

	public static bool 发送聊天(ServerChatEventArgs args, string 聊天内容, string 前前缀, string 前缀, string 角色名, string 后缀, string 后后缀, Color? 颜色)
	{
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		if (聊天检测(args))
		{
			string format = 配置.聊天格式.Replace("{前前缀}", "{0}").Replace("{前缀}", "{1}").Replace("{角色名}", "{2}")
				.Replace("{后缀}", "{3}")
				.Replace("{后后缀}", "{4}")
				.Replace("{聊天内容}", "{5}");
			_003C_003Ey__InlineArray6<object> buffer = default;
			buffer[0] = 前前缀;
			buffer[1] = 前缀;
			buffer[2] = 角色名;
			buffer[3] = 后缀;
			buffer[4] = 后后缀;
			buffer[5] = 聊天内容;
			string text = string.Format(format, (ReadOnlySpan<object?>)buffer);
			if (((HandledEventArgs)(object)args).Handled)
			{
				return false;
			}
			聊天气泡(args, 前前缀, 前缀, 角色名, 后缀, 后后缀);
			if (((HandledEventArgs)(object)args).Handled)
			{
				return true;
			}
			if (!颜色.HasValue)
			{
				TSPlayer.All.SendMessage(text, TShock.Players[args.Who].Group.R, TShock.Players[args.Who].Group.G, TShock.Players[args.Who].Group.B);
				((TSPlayer)TSPlayer.Server).SendMessage(text, TShock.Players[args.Who].Group.R, TShock.Players[args.Who].Group.G, TShock.Players[args.Who].Group.B);
			}
			else
			{
				TSPlayer all = TSPlayer.All;
				Color value = 颜色.Value;
				byte r = value.R;
				value = 颜色.Value;
				byte g = value.G;
				value = 颜色.Value;
				all.SendMessage(text, r, g, value.B);
				TSServerPlayer server = TSPlayer.Server;
				value = 颜色.Value;
				byte r2 = value.R;
				value = 颜色.Value;
				byte g2 = value.G;
				value = 颜色.Value;
				((TSPlayer)server).SendMessage(text, r2, g2, value.B);
			}
			TShock.Log.Info(TShock.Players[args.Who].Name + ">>> " + text);
			((HandledEventArgs)(object)args).Handled = true;
			return true;
		}
		return false;
	}

	public static bool 聊天检测(ServerChatEventArgs args)
	{
		if (((HandledEventArgs)(object)args).Handled)
		{
			return false;
		}
		if (args.Text == "")
		{
			((HandledEventArgs)(object)args).Handled = true;
			return false;
		}
		TSPlayer val = TShock.Players[args.Who];
		if (val == null)
		{
			((HandledEventArgs)(object)args).Handled = true;
			return false;
		}
		if (args.Text.Length > 500)
		{
			val.Kick("试图发送长聊天包崩溃服务器.", true, false, (string)null, false);
			((HandledEventArgs)(object)args).Handled = true;
			return false;
		}
		string text = args.Text;
		if ((text.StartsWith(((ConfigFile<TShockSettings>)(object)TShock.Config).Settings.CommandSpecifier) || text.StartsWith(((ConfigFile<TShockSettings>)(object)TShock.Config).Settings.CommandSilentSpecifier)) && !string.IsNullOrWhiteSpace(text.Substring(1)))
		{
			return false;
		}
		if (val.IsLoggedIn)
		{
			if (!val.HasPermission(Permissions.canchat))
			{
				val.SendErrorMessage("你没有聊天所需的权限\"tshock.canchat\"");
				((HandledEventArgs)(object)args).Handled = true;
				return false;
			}
			if (val.mute)
			{
				val.SendErrorMessage("你正被禁言中!");
				((HandledEventArgs)(object)args).Handled = true;
				return false;
			}
			return true;
		}
		return false;
	}

	private static void 聊天气泡(ServerChatEventArgs args, string 前前缀, string 前缀, string 角色名, string 后缀, string 后后缀)
	{
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		if (配置.是否开启聊天气泡)
		{
			string format = 配置.聊天气泡启用时玩家名称格式.Replace("{前前缀}", "{0}").Replace("{前缀}", "{1}").Replace("{角色名}", "{2}")
				.Replace("{后缀}", "{3}")
				.Replace("{后后缀}", "{4}");
			TSPlayer val = TShock.Players[args.Who];
			_003C_003Ey__InlineArray5<object> buffer = default;
			buffer[0] = 前前缀;
			buffer[1] = 前缀;
			buffer[2] = 角色名;
			buffer[3] = 后缀;
			buffer[4] = 后后缀;
			string text = string.Format(format, (ReadOnlySpan<object?>)buffer);
			string name = val.Name;
			Main.player[args.Who].name = text;
			NetMessage.SendData(4, -1, -1, NetworkText.FromLiteral(text), args.Who, 0f, 0f, 0f, 0, 0, 0);
			Main.player[args.Who].name = name;
			NetPacket val2 = NetTextModule.SerializeServerMessage(NetworkText.FromLiteral(args.Text), new Color((int)val.Group.R, (int)val.Group.G, (int)val.Group.B), (byte)args.Who);
			NetManager.Instance.Broadcast(val2, -1);
			NetMessage.SendData(4, -1, -1, NetworkText.FromLiteral(name), args.Who, 0f, 0f, 0f, 0, 0, 0);
			((TSPlayer)TSPlayer.Server).SendMessage("<" + text + "> " + args.Text, val.Group.R, val.Group.G, val.Group.B);
			TShock.Log.Info(name + ">>> <" + text + "> " + args.Text);
			((HandledEventArgs)(object)args).Handled = true;
		}
	}
}
