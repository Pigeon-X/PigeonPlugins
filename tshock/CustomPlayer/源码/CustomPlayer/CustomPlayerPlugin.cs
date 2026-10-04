using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Timers;
using CustomPlayer.ModfiyGroup;
using GetText;
using Microsoft.Xna.Framework;
using Rests;
using TShockAPI;
using TShockAPI.Configuration;
using TShockAPI.DB;
using TShockAPI.Hooks;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent.NetModules;
using Terraria.Localization;
using Terraria.Net;
using Terraria.UI.Chat;
using TerrariaApi.Server;
using VBY.Basic;
using GroupExistsException = CustomPlayer.ModfiyGroup.GroupExistsException;
using GroupManagerException = CustomPlayer.ModfiyGroup.GroupManagerException;
using GroupNotExistException = CustomPlayer.ModfiyGroup.GroupNotExistException;
using GroupManager = CustomPlayer.ModfiyGroup.GroupManager;
using VBY.Basic.Command;
using VBY.Basic.Extension;
using 称号插件;

namespace CustomPlayer;

[ApiVersion(2, 1)]
public class CustomPlayerPlugin : TerrariaPlugin
{
	public class PermInfo
	{
		public string Perm;

		public string Time;

		public PermInfo(string perm, string time)
		{
			Perm = perm;
			Time = time;
		}
	}

	public class TitleInfo
	{
		public string Title;

		public string Time;

		public int TitleId;

		public TitleInfo(string title, int titleId, string time)
		{
			Title = title;
			TitleId = titleId;
			Time = time;
		}
	}

	public const string VersionText = "1.0.0.5";

	public static readonly Config ReadConfig = new Config(TShock.SavePath);

	private static (string Prefix, string Suffix, TimeSpan? Time) TestObject;

	private readonly SubCmdRoot CmdCommand;

	private readonly SubCmdRoot CtlCommand;

	private readonly System.Timers.Timer TimeOutTimer = new System.Timers.Timer(300000.0);

	private readonly Command[] AddCommands;

	public override string Name { get; } = "CustomPlayerPlugin";

	public override string Author { get; } = "yu";

	public override Version Version { get; } = System.Version.Parse("1.0.0.5");

	private void CmdPermissionList(SubCmdArgs args)
	{
		TSPlayer player = args.commandArgs.Player;
		IEnumerable<TimeOutObject> enumerable = CustomPlayerPluginHelpers.TimeOutList.Where((TimeOutObject x) => x.Type == "Permission" && x.Name == player.Name);
		if (!enumerable.Any())
		{
			player.SendInfoMessage("你当前没有权限");
		}
		foreach (TimeOutObject item in enumerable)
		{
			player.SendInfoMessage("权限:" + item.Value + " 剩余时间:" + item.RemainTime);
		}
	}

	private void CmdReload(SubCmdArgs args)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected Obj, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected Obj, but got Unknown
		OnPlayerLogout(new PlayerLogoutEventArgs(args.commandArgs.Player));
		OnPlayerPostLogin(new PlayerPostLoginEventArgs(args.commandArgs.Player));
		args.commandArgs.Player.SendSuccessMessage("重载成功");
	}

	private static void Group(CommandArgs args)
	{
		//IL_050e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0515: Expected Obj, but got Unknown
		//IL_0fe0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fe7: Expected Obj, but got Unknown
		//IL_0ed4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0edb: Expected Obj, but got Unknown
		switch ((args.Parameters.Count == 0) ? "help" : args.Parameters[0].ToLower())
		{
		case "add":
		{
			if (args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid syntax. Proper syntax: {0}group add <group name> [permissions]."), Commands.Specifier));
				break;
			}
			string text = args.Parameters[1];
			args.Parameters.RemoveRange(0, 2);
			string permissions = string.Join(",", args.Parameters);
			try
			{
				CustomPlayerPluginHelpers.Groups.AddGroup(text, null, permissions, "255,255,255");
				args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Group " + text + " was added successfully.")));
				break;
			}
			catch (GroupExistsException)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("A group with the same name already exists.")));
				break;
			}
			catch (GroupManagerException ex2)
			{
				args.Player.SendErrorMessage(ex2.ToString());
				break;
			}
		}
		case "addperm":
		{
			if (args.Parameters.Count < 3)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid syntax. Proper syntax: {0}group addperm <group name> <permissions...>."), Commands.Specifier));
				break;
			}
			string text14 = args.Parameters[1];
			args.Parameters.RemoveRange(0, 2);
			if (text14 == "*")
			{
				foreach (Group group in CustomPlayerPluginHelpers.Groups)
				{
					CustomPlayerPluginHelpers.Groups.AddPermissions(group.Name, args.Parameters);
				}
				args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("The permissions have been added to all of the groups in the system.")));
				break;
			}
			try
			{
				string text15 = CustomPlayerPluginHelpers.Groups.AddPermissions(text14, args.Parameters);
				if (text15.Length > 0)
				{
					args.Player.SendSuccessMessage(text15);
				}
				break;
			}
			catch (GroupManagerException ex10)
			{
				args.Player.SendErrorMessage(ex10.ToString());
				break;
			}
		}
		case "help":
		{
			int num5 = default;
			if (PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out num5))
			{
				List<string> list3 = new List<string>
				{
					Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("add <name> <permissions...> - Adds a new group.")),
					Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("addperm <group> <permissions...> - Adds permissions to a group.")),
					Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("color <group> <rrr,ggg,bbb> - Changes a group's chat color.")),
					Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("rename <group> <new name> - Changes a group's name.")),
					Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("del <group> - Deletes a group.")),
					Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("delperm <group> <permissions...> - Removes permissions from a group.")),
					Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("list [page] - Lists groups.")),
					Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("listperm <group> [page] - Lists a group's permissions.")),
					Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("parent <group> <parent group> - Changes a group's parent group.")),
					Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("prefix <group> <prefix> - Changes a group's prefix.")),
					Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("suffix <group> <suffix> - Changes a group's suffix."))
				};
				TSPlayer player3 = args.Player;
				int num6 = num5;
				PaginationTools.Settings val = new PaginationTools.Settings();
				val.HeaderFormat = Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Group Sub-Commands ({{0}}/{{1}}):"));
				val.FooterFormat = Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Type {0}group help {{0}} for more sub-commands."), Commands.Specifier);
				PaginationTools.SendPage(player3, num6, (IList)list3, val);
			}
			break;
		}
		case "parent":
		{
			if (args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid syntax. Proper syntax: {0}group parent <group name> [new parent group name]."), Commands.Specifier));
				break;
			}
			string text4 = args.Parameters[1];
			Group groupByName3 = CustomPlayerPluginHelpers.Groups.GetGroupByName(text4);
			if (groupByName3 == null)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("No such group \"{0}\"."), text4));
			}
			else if (args.Parameters.Count > 2)
			{
				string text5 = string.Join(" ", args.Parameters.Skip(2));
				if (string.IsNullOrWhiteSpace(text5) || CustomPlayerPluginHelpers.Groups.GroupExists(text5))
				{
					try
					{
						CustomPlayerPluginHelpers.Groups.UpdateGroup(text4, text5, groupByName3.Permissions, groupByName3.ChatColor, groupByName3.Suffix, groupByName3.Prefix);
						if (!string.IsNullOrWhiteSpace(text5))
						{
							args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Parent of group \"{0}\" set to \"{1}\"."), text4, text5));
						}
						else
						{
							args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Removed parent of group \"{0}\"."), text4));
						}
						break;
					}
					catch (GroupManagerException ex4)
					{
						args.Player.SendErrorMessage(ex4.Message);
						break;
					}
				}
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("No such group \"{0}\"."), text5));
			}
			else if (groupByName3.Parent != null)
			{
				args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Parent of \"{0}\" is \"{1}\"."), groupByName3.Name, groupByName3.Parent.Name));
			}
			else
			{
				args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Group \"{0}\" has no parent."), groupByName3.Name));
			}
			break;
		}
		case "suffix":
		{
			if (args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid syntax. Proper syntax: {0}group suffix <group name> [new suffix]."), Commands.Specifier));
				break;
			}
			string text6 = args.Parameters[1];
			Group groupByName4 = CustomPlayerPluginHelpers.Groups.GetGroupByName(text6);
			if (groupByName4 == null)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("No such group \"{0}\"."), text6));
				break;
			}
			if (args.Parameters.Count > 2)
			{
				string text7 = string.Join(" ", args.Parameters.Skip(2));
				try
				{
					CustomPlayerPluginHelpers.Groups.UpdateGroup(text6, groupByName4.ParentName, groupByName4.Permissions, groupByName4.ChatColor, text7, groupByName4.Prefix);
					if (!string.IsNullOrWhiteSpace(text7))
					{
						args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Suffix of group \"{0}\" set to \"{1}\"."), text6, text7));
					}
					else
					{
						args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Removed suffix of group \"{0}\"."), text6));
					}
					break;
				}
				catch (GroupManagerException ex5)
				{
					args.Player.SendErrorMessage(ex5.Message);
					break;
				}
			}
			if (!string.IsNullOrWhiteSpace(groupByName4.Suffix))
			{
				args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Suffix of \"{0}\" is \"{1}\"."), groupByName4.Name, groupByName4.Suffix));
			}
			else
			{
				args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Group \"{0}\" has no suffix."), groupByName4.Name));
			}
			break;
		}
		case "prefix":
		{
			if (args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid syntax. Proper syntax: {0}group prefix <group name> [new prefix]."), Commands.Specifier));
				break;
			}
			string text10 = args.Parameters[1];
			Group groupByName5 = CustomPlayerPluginHelpers.Groups.GetGroupByName(text10);
			if (groupByName5 == null)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("No such group \"{0}\"."), text10));
				break;
			}
			if (args.Parameters.Count > 2)
			{
				string text11 = string.Join(" ", args.Parameters.Skip(2));
				try
				{
					CustomPlayerPluginHelpers.Groups.UpdateGroup(text10, groupByName5.ParentName, groupByName5.Permissions, groupByName5.ChatColor, groupByName5.Suffix, text11);
					if (!string.IsNullOrWhiteSpace(text11))
					{
						args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Prefix of group \"{0}\" set to \"{1}\"."), text10, text11));
					}
					else
					{
						args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Removed prefix of group \"{0}\"."), text10));
					}
					break;
				}
				catch (GroupManagerException ex8)
				{
					args.Player.SendErrorMessage(ex8.Message);
					break;
				}
			}
			if (!string.IsNullOrWhiteSpace(groupByName5.Prefix))
			{
				args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Prefix of \"{0}\" is \"{1}\"."), groupByName5.Name, groupByName5.Prefix));
			}
			else
			{
				args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Group \"{0}\" has no prefix."), groupByName5.Name));
			}
			break;
		}
		case "color":
		{
			if (args.Parameters.Count < 2 || args.Parameters.Count > 3)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid syntax. Proper syntax: {0}group color <group name> [new color(000,000,000)]."), Commands.Specifier));
				break;
			}
			string text2 = args.Parameters[1];
			Group groupByName2 = CustomPlayerPluginHelpers.Groups.GetGroupByName(text2);
			if (groupByName2 == null)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("No such group \"{0}\"."), text2));
			}
			else if (args.Parameters.Count == 3)
			{
				string text3 = args.Parameters[2];
				string[] array = text3.Split(',');
				if (array.Length == 3 && byte.TryParse(array[0], out var _) && byte.TryParse(array[1], out var _) && byte.TryParse(array[2], out var _))
				{
					try
					{
						CustomPlayerPluginHelpers.Groups.UpdateGroup(text2, groupByName2.ParentName, groupByName2.Permissions, text3, groupByName2.Suffix, groupByName2.Prefix);
						args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Chat color for group \"{0}\" set to \"{1}\"."), text2, text3));
						break;
					}
					catch (GroupManagerException ex3)
					{
						args.Player.SendErrorMessage(ex3.Message);
						break;
					}
				}
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid syntax for color, expected \"rrr,ggg,bbb\".")));
			}
			else
			{
				args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Chat color for \"{0}\" is \"{1}\"."), groupByName2.Name, groupByName2.ChatColor));
			}
			break;
		}
		case "rename":
		{
			if (args.Parameters.Count != 3)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid syntax. Proper syntax: {0}group rename <group> <new name>."), Commands.Specifier));
				break;
			}
			string name = args.Parameters[1];
			string newName = args.Parameters[2];
			try
			{
				string text9 = CustomPlayerPluginHelpers.Groups.RenameGroup(name, newName);
				args.Player.SendSuccessMessage(text9);
				break;
			}
			catch (GroupManagerException ex7)
			{
				args.Player.SendErrorMessage(ex7.Message);
				break;
			}
		}
		case "del":
			if (args.Parameters.Count != 2)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid syntax. Proper syntax: {0}group del <group name>."), Commands.Specifier));
				break;
			}
			try
			{
				string text8 = CustomPlayerPluginHelpers.Groups.DeleteGroup(args.Parameters[1], exceptions: true);
				if (text8.Length > 0)
				{
					args.Player.SendSuccessMessage(text8);
				}
				break;
			}
			catch (GroupManagerException ex6)
			{
				args.Player.SendErrorMessage(ex6.Message);
				break;
			}
		case "delperm":
		{
			if (args.Parameters.Count < 3)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid syntax. Proper syntax: {0}group delperm <group name> <permissions...>."), Commands.Specifier));
				break;
			}
			string text12 = args.Parameters[1];
			args.Parameters.RemoveRange(0, 2);
			if (text12 == "*")
			{
				foreach (Group group2 in CustomPlayerPluginHelpers.Groups)
				{
					CustomPlayerPluginHelpers.Groups.DeletePermissions(group2.Name, args.Parameters);
				}
				args.Player.SendSuccessMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("The permissions have been removed from all of the groups in the system.")));
				break;
			}
			try
			{
				string text13 = CustomPlayerPluginHelpers.Groups.DeletePermissions(text12, args.Parameters);
				if (text13.Length > 0)
				{
					args.Player.SendSuccessMessage(text13);
				}
				break;
			}
			catch (GroupManagerException ex9)
			{
				args.Player.SendErrorMessage(ex9.Message);
				break;
			}
		}
		case "list":
		{
			int num3 = default;
			if (PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out num3))
			{
				IEnumerable<string> enumerable = CustomPlayerPluginHelpers.Groups.groups.Select((Group grp) => grp.Name);
				TSPlayer player2 = args.Player;
				int num4 = num3;
				List<string> list2 = PaginationTools.BuildLinesFromTerms((IEnumerable)enumerable, (Func<object, string>)null, ", ", 80);
				PaginationTools.Settings val = new PaginationTools.Settings();
				val.HeaderFormat = Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Groups ({{0}}/{{1}}):"));
				val.FooterFormat = Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Type {0}group list {{0}} for more."), Commands.Specifier);
				PaginationTools.SendPage(player2, num4, (IList)list2, val);
			}
			break;
		}
		case "listperm":
		{
			int num = default;
			if (args.Parameters.Count == 1)
			{
				args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid syntax. Proper syntax: {0}group listperm <group name> [page]."), Commands.Specifier));
			}
			else if (PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out num))
			{
				if (!CustomPlayerPluginHelpers.Groups.GroupExists(args.Parameters[1]))
				{
					args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid group.")));
					break;
				}
				Group groupByName = CustomPlayerPluginHelpers.Groups.GetGroupByName(args.Parameters[1]);
				List<string> totalPermissions = groupByName.TotalPermissions;
				TSPlayer player = args.Player;
				int num2 = num;
				List<string> list = PaginationTools.BuildLinesFromTerms((IEnumerable)totalPermissions, (Func<object, string>)null, ", ", 80);
				PaginationTools.Settings val = new PaginationTools.Settings();
				val.HeaderFormat = Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Permissions for {0} ({{0}}/{{1}}):"), groupByName.Name);
				val.FooterFormat = Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Type {0}group listperm {1} {{0}} for more."), Commands.Specifier, groupByName.Name);
				val.NothingToDisplayString = Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("There are currently no permissions for " + groupByName.Name + "."));
				PaginationTools.SendPage(player, num2, (IList)list, val);
			}
			break;
		}
		default:
			args.Player.SendErrorMessage(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid subcommand! Type {0}group help for more information on valid commands."), Commands.Specifier));
			break;
		}
	}

	private void CtlGroupCtlAdd(SubCmdArgs args)
	{
		string name = args.Parameters[0];
		string text = args.Parameters[1];
		string text2 = args.Parameters[2];
		TSPlayer player = args.commandArgs.Player;
		DateTime startTime = DateTime.Now;
		DateTime endTime = default;
		TimeSpan addTime = default;
		bool flag = text2 == "-1";
		if (TimeParse(flag, text2, ref startTime, ref endTime, ref addTime, player))
		{
			return;
		}
		List<string> haveGroups = new List<string>();
		QueryResult val = Utils.QueryReader("select Value,Type,StartTime,EndTime,DurationText from ExpirationInfo where Type = 'Group' AND Name = @0", name);
		try
		{
			val.Reader.ForEach((IDataReader x) =>
			{
				TimeOutObject timeOutObject = new TimeOutObject(name, x.GetString("Value"), x.GetString("Type"), x.GetDateTime("StartTime"), x.GetDateTime("EndTime"), x.GetString("DurationText"));
				if (timeOutObject.NoExpired)
				{
					haveGroups.Add(timeOutObject.Value);
				}
				else
				{
					player.SendInfoMessage("组:" + timeOutObject.Value + " 已过期");
					timeOutObject.Delete();
				}
			});
			if (haveGroups.Contains(text))
			{
				player.SendInfoMessage("玩家:{0} 已拥有组:{1}", new object[2] { name, text });
				return;
			}
			if (!CustomPlayerPluginHelpers.Groups.GroupExists(text))
			{
				player.SendInfoMessage("组:{0} 不存在,如果确定存在,请执行:{1}{2} {3}", new object[4]
				{
					text,
					Commands.Specifier,
					AddCommands[1].Name,
					CtlCommand["Reload"].Names[0]
				});
				return;
			}
			int num = (haveGroups.Any() ? haveGroups.Select((string x) => CustomPlayerPluginHelpers.GroupGrade[x]).Max() : (-1));
			int num2 = CustomPlayerPluginHelpers.GroupGrade[text];
			if (num < num2)
			{
				player.SendInfoMessage("玩家:{0} 组升级为 {1}", new object[2] { name, text });
			}
			Utils.Query("insert into ExpirationInfo(Name,Value,Type,StartTime,EndTime,DurationText) values(@0,@1,@2,@3,@4,@5)", name, text, "Group", startTime, endTime, text2);
			if (flag)
			{
				player.SendInfoMessage($"添加成功 玩家:{name} 组:{text} 持续时间:永久");
			}
			else
			{
				player.SendInfoMessage($"添加成功 玩家:{name} 组:{text} 起始时间:{startTime} 结束时间:{endTime} 持续时间:{addTime}");
			}
			Utils.FindPlayer(name)?.Reload();
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void CtlGroupCtlDel(SubCmdArgs args)
	{
		string name = args.Parameters[0];
		string groupName = args.Parameters[1];
		TSPlayer player = args.commandArgs.Player;
		List<string> haveGroups = new List<string>();
		TimeOutObject curObj = null;
		QueryResult val = Utils.QueryReader("select Value,Type,StartTime,EndTime,DurationText from ExpirationInfo where Type = 'Group' AND Name = @0", name);
		try
		{
			val.Reader.ForEach((IDataReader x) =>
			{
				TimeOutObject timeOutObject = new TimeOutObject(name, x.GetString("Value"), x.GetString("Type"), x.GetDateTime("StartTime"), x.GetDateTime("EndTime"), x.GetString("DurationText"));
				if (timeOutObject.NoExpired)
				{
					haveGroups.Add(timeOutObject.Value);
				}
				else
				{
					player.SendInfoMessage("组:" + timeOutObject.Value + " 已过期");
					timeOutObject.Delete();
				}
				if (timeOutObject.Value == groupName)
				{
					curObj = timeOutObject;
				}
			});
			if (curObj == null)
			{
				player.SendInfoMessage("玩家:{0} 不拥有组:{1}", new object[2] { name, groupName });
				return;
			}
			curObj.Delete();
			CustomPlayer customPlayer = Utils.FindPlayer(name);
			if (customPlayer != null)
			{
				customPlayer.Reload();
				customPlayer.Player.SendInfoMessage("你的组:{0} 已被管理员删除", new object[1] { groupName });
			}
			player.SendInfoMessage("删除成功 玩家:" + name + " 组:" + groupName);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void CtlGroupCtlList(SubCmdArgs args)
	{
		string name = args.Parameters[0];
		TSPlayer player = args.commandArgs.Player;
		QueryResult val = Utils.QueryReader("select Value,Type,StartTime,EndTime,DurationText from ExpirationInfo where Type = 'Group' AND Name = @0", name);
		try
		{
			List<TimeOutObject> objs = new List<TimeOutObject>();
			val.Reader.ForEach((IDataReader x) =>
			{
				TimeOutObject timeOutObject = new TimeOutObject(name, x.GetString("Value"), x.GetString("Type"), x.GetDateTime("StartTime"), x.GetDateTime("EndTime"), x.GetString("DurationText"));
				if (timeOutObject.NoExpired)
				{
					objs.Add(timeOutObject);
				}
				else
				{
					player.SendInfoMessage("组:" + timeOutObject.Value + " 已过期");
					timeOutObject.Delete();
				}
			});
			if (!objs.Any())
			{
				player.SendInfoMessage("此玩家没有组");
				return;
			}
			TimeOutObject[] array = objs.OrderByDescending((TimeOutObject x) => CustomPlayerPluginHelpers.GroupGrade[x.Value]).ToArray();
			player.SendSuccessMessage("组:{0} 剩余时间:{1}", new object[2]
			{
				array[0].Value,
				array[0].RemainTime
			});
			for (int num = 1; num < array.Length; num++)
			{
				player.SendInfoMessage("组:{0} 剩余时间:{1}", new object[2]
				{
					array[num].Value,
					array[num].RemainTime
				});
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void CtlPermissionAdd(SubCmdArgs args)
	{
		string text = args.Parameters[0];
		string text2 = args.Parameters[1];
		string text3 = args.Parameters[2];
		TSPlayer player = args.commandArgs.Player;
		DateTime startTime = DateTime.Now;
		DateTime endTime = default;
		TimeSpan addTime = default;
		bool flag = text3 == "-1";
		if (!TimeParse(flag, text3, ref startTime, ref endTime, ref addTime, player))
		{
			if (Utils.NotifyPlayer("权限", flag, new TimeOutObject(text, text2, "Permission", startTime, endTime, text3), out CustomPlayer cply))
			{
				cply.AddPermission(text2);
			}
			Utils.Query("insert into ExpirationInfo(Name,Value,Type,StartTime,EndTime,DurationText) values(@0,@1,@2,@3,@4,@5)", text, text2, "Permission", startTime, endTime, text3);
			if (flag)
			{
				player.SendInfoMessage($"添加成功 玩家:{text} 权限:{text2} 持续时间:永久");
			}
			else
			{
				player.SendInfoMessage($"添加成功 玩家:{text} 权限:{text2} 起始时间:{startTime} 结束时间:{endTime} 持续时间:{addTime}");
			}
		}
	}

	private void CtlPermissionDel(SubCmdArgs args)
	{
		TSPlayer player = args.commandArgs.Player;
		string name = args.Parameters[0];
		string delPermission = args.Parameters[1];
		if (Utils.Query("delete from ExpirationInfo where Name = @0 AND Value = @1", name, delPermission) == 0)
		{
			player.SendInfoMessage("未在数据库找到 玩家:" + name + " 的权限:" + delPermission);
			return;
		}
		player.SendSuccessMessage("删除成功");
		CustomPlayer customPlayer = Utils.FindPlayer(name);
		if (customPlayer != null)
		{
			CustomPlayerPluginHelpers.TimeOutList.RemoveAll((TimeOutObject x) => x.Name == name && x.Value == delPermission);
			customPlayer.DelPermission(delPermission);
			customPlayer.Player.SendInfoMessage("你的权限:" + delPermission + " 已被管理员提前删除");
		}
	}

	private void CtlPermissionList(SubCmdArgs args)
	{
		TSPlayer player = args.commandArgs.Player;
		string text = args.Parameters[0];
		QueryResult val = Utils.QueryReader("select Value,EndTime,DurationText from ExpirationInfo where name = @0 AND Type = @1", text, "Permission");
		try
		{
			if (val.Read())
			{
				player.SendInfoMessage("玩家:" + text + " 的权限");
				val.Reader.DoForEach((IDataReader x) =>
				{
					player.SendInfoMessage("权限:" + x.GetString("Value") + " 剩余时间:" + ((x.GetString("DurationText") == "-1") ? "永久" : (x.GetDateTime("EndTime") - DateTime.Now).ToString("d\\.hh\\:mm\\:ss")));
				});
			}
			else
			{
				player.SendInfoMessage("没有找到此玩家的权限");
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		QueryResult val2 = Utils.QueryReader("select Commands from PlayerList where name = @0", text);
		try
		{
			if (val2.Read() && !val2.Reader.IsDBNull(0))
			{
				player.SendInfoMessage("永久权限");
				string[] array = val2.Reader.GetString(0).Split(",", StringSplitOptions.RemoveEmptyEntries);
				foreach (string text2 in array)
				{
					player.SendInfoMessage("权限:" + text2 + " 剩余时间:永久");
				}
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void CtlReload(SubCmdArgs args)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Expected Obj, but got Unknown
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Expected Obj, but got Unknown
		if (ReadConfig.Read(args.commandArgs.Player, readKey: false, log: true, ReadConfig.Root))
		{
			PluginInit();
			CustomPlayerPluginHelpers.Groups = new GroupManager(CustomPlayerPluginHelpers.DB);
			CustomPlayerPluginHelpers.GroupGrade.Clear();
			CustomPlayerPluginHelpers.GroupLevelSet();
			for (int i = 0; i <= CustomPlayerPluginHelpers.Players.Length - 1; i++)
			{
				if (CustomPlayerPluginHelpers.Players[i] != null)
				{
					OnPlayerLogout(new PlayerLogoutEventArgs(CustomPlayerPluginHelpers.Players[i].Player));
				}
			}
			foreach (TSPlayer item in TShock.Players.Where((TSPlayer x) => x?.IsLoggedIn ?? false))
			{
				OnPlayerLogout(new PlayerLogoutEventArgs(item));
			}
			args.commandArgs.Player.SendSuccessMessage("重载成功");
		}
		else
		{
			args.commandArgs.Player.SendSuccessMessage("重载错误");
			args.commandArgs.Player.SendErrorMessage(ReadConfig.ErrorString);
		}
	}

	private void CtlPrefixAdd(SubCmdArgs args)
	{
		CtlTitleAdd(ref args, "Prefix");
	}

	private void CtlPrefixDel(SubCmdArgs args)
	{
		CtlTitleDel(ref args, "Prefix");
	}

	private void CtlPrefixList(SubCmdArgs args)
	{
		CtlTitleList(ref args, "Prefix");
	}

	private void CtlPrefixTest(SubCmdArgs args)
	{
		CtlTitleTest(ref args, "Prefix");
	}

	private void CtlPrefixWear(SubCmdArgs args)
	{
		CtlTitleWear(ref args, "Prefix");
	}

	private void CtlSuffixAdd(SubCmdArgs args)
	{
		CtlTitleAdd(ref args, "Suffix");
	}

	private void CtlSuffixDel(SubCmdArgs args)
	{
		CtlTitleDel(ref args, "Suffix");
	}

	private void CtlSuffixList(SubCmdArgs args)
	{
		CtlTitleList(ref args, "Suffix");
	}

	private void CtlSuffixTest(SubCmdArgs args)
	{
		CtlTitleTest(ref args, "Suffix");
	}

	private void CtlSuffixWear(SubCmdArgs args)
	{
		CtlTitleWear(ref args, "Suffix");
	}

	private static bool TimeParse(bool forever, string time, ref DateTime startTime, ref DateTime endTime, ref TimeSpan addTime, TSPlayer? player)
	{
		if (forever)
		{
			endTime = startTime;
		}
		else if (time == "get")
		{
			if (!TestObject.Time.HasValue)
			{
				if (player != null)
				{
					player.SendErrorMessage("Time.Test 未设置过值");
				}
				return true;
			}
			addTime = TestObject.Time.Value;
			endTime = startTime.Add(TestObject.Time.Value);
		}
		else
		{
			if (!TimeSpan.TryParse(time, out addTime))
			{
				if (player != null)
				{
					player.SendErrorMessage("转换 " + time + " 为 TimeSpan 失败");
				}
				return true;
			}
			endTime = startTime.Add(addTime);
		}
		return false;
	}

	private static bool TitleParse(string type, ref string title, TSPlayer? player)
	{
		if (title == "get")
		{
			if (string.IsNullOrEmpty((type == "Prefix") ? TestObject.Prefix : TestObject.Suffix))
			{
				if (player != null)
				{
					player.SendErrorMessage(type + ".Test 未设置有效值");
				}
				return true;
			}
			title = ((type == "Prefix") ? TestObject.Prefix : TestObject.Suffix);
		}
		return false;
	}

	private static void TitleList(ref SubCmdArgs args, string type)
	{
		TSPlayer player = args.commandArgs.Player;
		CustomPlayer player2 = player.GetPlayer();
		List<TableInfo.Prefix> list = ((type == "前缀") ? player2.PrefixList : player2.SuffixList);
		player.SendInfoMessage($"你的所有{type}({list.Count})");
		foreach (TableInfo.Prefix item in list)
		{
			player.SendInfoMessage($"ID:{item.Id} 内容:{item.Value} 剩余时间:{((item.DurationText == "-1") ? "永久" : (DateTime.Now - item.EndTime).ToString())}");
		}
	}

	private static void TitleWear(ref SubCmdArgs args, string type)
	{
		TSPlayer player = args.commandArgs.Player;
		if (!int.TryParse(args.Parameters[0], out var id))
		{
			player.SendErrorMessage("转换Id失败");
			return;
		}
		bool flag = type == "Prefix";
		string text = (flag ? "前缀" : "后缀");
		bool flag2 = id == -1;
		CustomPlayer player2 = player.GetPlayer();
		List<TableInfo.Prefix> list = (flag ? player2.PrefixList : player2.SuffixList);
		if (flag2)
		{
			if (flag)
			{
				player2.Prefix = null;
			}
			else
			{
				player2.Suffix = null;
			}
			player.SendSuccessMessage("佩戴" + text + "已清除");
		}
		else
		{
			TableInfo.Prefix prefix = list.Find((TableInfo.Prefix x) => x.Id == id);
			if (prefix == null)
			{
				player.SendInfoMessage($"{text}ID:{id} 未找到");
				return;
			}
			if (flag)
			{
				player2.Prefix = prefix.Value;
			}
			else
			{
				player2.Suffix = prefix.Value;
			}
			player.SendSuccessMessage($"{text}ID:{id} 已佩戴");
		}
		Utils.Query("update Useing set " + type + "Id = @0 where Name = @1 and ServerId = @2", id, player.Name, ReadConfig.Root.ServerId);
	}

	private static void CtlTitleList(ref SubCmdArgs args, string type)
	{
		TSPlayer player = args.commandArgs.Player;
		string text = args.Parameters[0];
		bool flag = type == "Prefix";
		string typeChinese = (flag ? "前缀" : "后缀");
		QueryResult val = (flag ? Utils.PrefixQuery(text) : Utils.SuffixQuery(text));
		try
		{
			if (val.Read())
			{
				player.SendInfoMessage("玩家:" + text + " 的" + typeChinese);
				val.Reader.DoForEach((IDataReader x) =>
				{
					player.SendInfoMessage($"{typeChinese}:{x.GetString("Value")} Id:{x.GetInt32("Id")} 剩余时间:{((x.GetString("DurationText") == "-1") ? "永久" : (x.GetDateTime("EndTime") - DateTime.Now).ToString("d\\.hh\\:mm\\:ss"))}");
				});
			}
			else
			{
				player.SendInfoMessage("没有找到此玩家的" + typeChinese);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static void CtlTitleAdd(ref SubCmdArgs args, string type)
	{
		string text = args.Parameters[0];
		string title = args.Parameters[1].Replace(";", "");
		string text2 = args.Parameters[2];
		TSPlayer player = args.commandArgs.Player;
		DateTime startTime = DateTime.Now;
		DateTime endTime = default;
		TimeSpan addTime = default;
		bool flag = text2 == "-1";
		int num = 0;
		bool flag2 = type == "Prefix";
		string text3 = (flag2 ? "前缀" : "后缀");
		if (TimeParse(flag, text2, ref startTime, ref endTime, ref addTime, player) || TitleParse(type, ref title, player))
		{
			return;
		}
		QueryResult val = Utils.QueryReader("select max(Id) from " + type + " where Name = @0", text);
		try
		{
			if (val.Read() && !val.Reader.IsDBNull(0))
			{
				num = val.Reader.GetInt32(0) + 1;
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		TimeOutObject data = new TimeOutObject(text, title, type, startTime, endTime, text2, num);
		if (Utils.NotifyPlayer(text3, flag, data, out CustomPlayer cply))
		{
			(flag2 ? cply.PrefixList : cply.SuffixList).Add(data);
		}
		Utils.Query("insert into " + type + "(Name,Id,Value,StartTime,EndTime,DurationText) values(@0,@1,@2,@3,@4,@5)", text, num, title, startTime, endTime, text2);
		if (flag)
		{
			player.SendInfoMessage($"添加成功 玩家:{text} {text3}:{title} Id:{num} 持续时间:永久");
		}
		else
		{
			player.SendInfoMessage($"添加成功 玩家:{text} {text3}:{title} Id:{num} 起始时间:{startTime} 结束时间:{endTime} 持续时间:{addTime}");
		}
	}

	private static void CtlTitleDel(ref SubCmdArgs args, string type)
	{
		string text = args.Parameters[0];
		string text2 = args.Parameters[1];
		TSPlayer player = args.commandArgs.Player;
		string value = ((type == "Prefix") ? "前缀" : "后缀");
		if (int.TryParse(text2, out var result) && Utils.Query("delete from " + type + " where Name = @0 AND Id = @1", text, result) > 0)
		{
			player.SendSuccessMessage($"删除玩家[{text}] {value}Id:{result} 成功");
		}
		else if (Utils.Query("delete from " + type + " where Name = @0 AND Value = @1", text, text2) > 0)
		{
			player.SendSuccessMessage($"删除玩家:[{text}] {value}Value:{text2} 成功");
		}
		else
		{
			player.SendErrorMessage("删除失败,没有找到");
		}
	}

	private static void CtlTitleTest(ref SubCmdArgs args, string type)
	{
		CustomPlayer player = args.commandArgs.Player.GetPlayer();
		if (type == "Prefix")
		{
			player.Prefix = args.Parameters[0];
			TestObject.Prefix = args.Parameters[0];
		}
		else
		{
			player.Suffix = args.Parameters[0];
			TestObject.Suffix = args.Parameters[0];
		}
	}

	private static void CtlTitleWear(ref SubCmdArgs args, string type)
	{
		TSPlayer player = args.commandArgs.Player;
		string text = args.Parameters[0];
		bool flag = type == "Prefix";
		string text2 = (flag ? "前缀" : "后缀");
		if (!int.TryParse(args.Parameters[1], out var result))
		{
			player.SendErrorMessage("转换" + text2 + "Id失败");
			return;
		}
		if (!int.TryParse(args.Parameters[2], out var result2))
		{
			player.SendErrorMessage("转换ServerId失败");
			return;
		}
		bool flag2 = false;
		QueryResult val = Utils.TitleQuery(type, text, result);
		try
		{
			flag2 = val.Read();
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		if (!flag2)
		{
			player.SendErrorMessage($"玩家:{text} {text2}Id:{result} 未找到");
			return;
		}
		bool flag3 = false;
		QueryResult val2 = Utils.QueryReader("select PrefixId,SuffixId from Useing where Name = @0 and ServerId = @1", text, result2);
		try
		{
			flag3 = val2.Read();
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		if (flag3)
		{
			Utils.Query("update Useing set " + type + "Id = @0 where Name = @1 and ServerId = @2", result, text, result2);
		}
		else
		{
			Utils.Query("insert into Useing(Name,ServerId,PrefixId,SuffixId) values(@0,@1,@2,@3)", text, result2, flag ? result : (-1), flag ? (-1) : result);
		}
		player.SendSuccessMessage($"已为玩家:{text} 佩戴{text2}Id:{result} 到服务器:{result2}");
	}

	public CustomPlayerPlugin(Main game)
		: base(game)
	{
		((TerrariaPlugin)this).Order = 1;
		CmdCommand = new SubCmdRoot("Bear");
		CtlCommand = new SubCmdRoot("Custom");
		CmdCommand.AddList("Permission", "权限", "perm").AddCmd(CmdPermissionList, "List", "权限列表");
		SubCmdNodeList subCmdNodeList = CmdCommand.AddList("Prefix", "前缀");
		subCmdNodeList.AddCmd((SubCmdArgs args) =>
		{
			TitleList(ref args, "前缀");
		}, "List", "前缀列表");
		subCmdNodeList.AddCmdA((SubCmdArgs args) =>
		{
			TitleWear(ref args, "Prefix");
		}, "Wear", "前缀佩戴", "<前缀ID>", "为-1时设置为空");
		SubCmdNodeList subCmdNodeList2 = CmdCommand.AddList("Suffix", "后缀");
		subCmdNodeList2.AddCmd((SubCmdArgs args) =>
		{
			TitleList(ref args, "后缀");
		}, "List", "后缀列表");
		subCmdNodeList2.AddCmdA((SubCmdArgs args) =>
		{
			TitleWear(ref args, "Suffix");
		}, "Wear", "后缀佩戴", "<后缀ID>", "为-1时设置为空");
		CmdCommand.AddCmd(CmdReload, "Reload", "数据修改后重载");
		CmdCommand.SetAllNodeRun(new AllowInfo(null, false, null));
		CtlCommand.AddCmd((SubCmdArgs args) =>
		{
			args.commandArgs.Parameters.RemoveAt(0);
			Group(args.commandArgs);
		}, "Group", "ts组管理");
		SubCmdNodeList subCmdNodeList3 = CtlCommand.AddList("GroupCtl", "组管理");
		subCmdNodeList3.AddCmdAA(CtlGroupCtlAdd, "添加组", "<玩家名> <组名> <时限>");
		subCmdNodeList3.AddCmdAA(CtlGroupCtlDel, "删除组", "<玩家名> <组名>");
		subCmdNodeList3.AddCmdAA(CtlGroupCtlList, "列出组", "<玩家名>");
		SubCmdNodeList subCmdNodeList4 = CtlCommand.AddList("Permission", "权限管理", "perm");
		subCmdNodeList4.AddCmdAA(CtlPermissionAdd, "添加权限", "<玩家名> <权限名> <时限>", " <时限>为-1时为永久,为get时会获取Time.Test命令的设置");
		subCmdNodeList4.AddCmdAA(CtlPermissionDel, "删除权限", "<玩家名> <权限名>");
		subCmdNodeList4.AddCmdAA(CtlPermissionList, "权限列表", "<玩家名>");
		SubCmdNodeList subCmdNodeList5 = CtlCommand.AddList("Prefix", "前缀管理");
		subCmdNodeList5.AddCmdAA(CtlPrefixAdd, "添加前缀", "<玩家名> <前缀头衔> <时限>", " <时限>为-1时为永久,为get时会获取Time.Test命令的设置\n <前缀头衔>为get时会获取Prefix.Test命令的设置");
		subCmdNodeList5.AddCmdAA(CtlPrefixDel, "删除前缀", "<玩家名> <前缀ID/前缀头衔>");
		subCmdNodeList5.AddCmdAA(CtlPrefixList, "前缀列表", "<玩家名>");
		subCmdNodeList5.AddCmdAA(CtlPrefixTest, "前缀测试", "<前缀头衔>", " 会把输入的前缀设置为当前玩家的前缀").AllowServer = false;
		subCmdNodeList5.AddCmdAA(CtlPrefixWear, "佩戴前缀", "<玩家名> <前缀ID> <服务器ID>");
		SubCmdNodeList subCmdNodeList6 = CtlCommand.AddList("Suffix", "后缀管理");
		subCmdNodeList6.AddCmdAA(CtlSuffixAdd, "添加后缀", "<玩家名> <后缀头衔> <时限>", " <时限>为-1时为永久,为get时会获取Time.Test命令的设置\n <后缀头衔>为get时会获取Suffix.Test命令的设置");
		subCmdNodeList6.AddCmdAA(CtlSuffixDel, "删除后缀", "<玩家名> <后缀ID/后缀头衔>");
		subCmdNodeList6.AddCmdAA(CtlSuffixList, "后缀列表", "<玩家名>");
		subCmdNodeList6.AddCmdAA(CtlSuffixTest, "后缀测试", "<后缀头衔>", " 会把输入的后缀设置为当前玩家的后缀").AllowServer = false;
		subCmdNodeList6.AddCmdAA(CtlSuffixWear, "佩戴后缀", "<玩家名> <后缀ID> <服务器ID>");
		SubCmdNodeList subCmdNodeList7 = CtlCommand.AddList("Time", "时间相关");
		subCmdNodeList7.AddCmd((SubCmdArgs args) =>
		{
			OnTimer(null, null);
			args.commandArgs.Player.SendSuccessMessage("时间检查完成");
		}, "Check", "立刻进行时间检查");
		subCmdNodeList7.AddCmdA((SubCmdArgs args) =>
		{
			if (!TestObject.Time.HasValue)
			{
				TestObject.Time = default(TimeSpan);
			}
			if (TimeSpan.TryParse(args.Parameters[0], out var result))
			{
				args.commandArgs.Player.SendSuccessMessage("转换成功: " + result);
				TestObject.Time = result;
			}
			else
			{
				args.commandArgs.Player.SendInfoMessage("转换失败");
			}
		}, "Test", "时间测试", "<时限>");
		CtlCommand.AddCmd(CtlReload, "Reload", "重载");
		PluginInit();
		AddCommands = ReadConfig.Root.Commands.GetCommands(CmdCommand, CtlCommand);
	}

	private static void PluginInit(TSPlayer? player = null)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected Obj, but got Unknown
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Invalid comparison between Unknown and I4
		if (!ReadConfig.Read())
		{
			return;
		}
		CustomPlayer.对接称号插件 = ReadConfig.Root.对接称号插件;
		if (!Utils.CreateConnection())
		{
			return;
		}
		try
		{
			SqlTableCreator val = new SqlTableCreator(CustomPlayerPluginHelpers.DB, DbExt.GetSqlQueryBuilder(CustomPlayerPluginHelpers.DB));
			Type[] nestedTypes = typeof(TableInfo).GetNestedTypes();
			foreach (Type tableClassType in nestedTypes)
			{
				val.EnsureTableStructure(Utils.SqlTableCreate(tableClassType));
			}
			if ((int)DbExt.GetSqlType(CustomPlayerPluginHelpers.DB) == 1)
			{
				Utils.Query("PRAGMA journal_mode=WAL");
				Utils.Query("PRAGMA busy_timeout=5000");
				Utils.Query("PRAGMA synchronous=NORMAL");
			}
			CustomPlayerPluginHelpers.Groups = new GroupManager(CustomPlayerPluginHelpers.DB);
			CustomPlayerPluginHelpers.GroupGrade.Clear();
			CustomPlayerPluginHelpers.GroupLevelSet();
		}
		catch (Exception ex)
		{
			CustomPlayerPluginHelpers.DB = null;
			if (player == null)
			{
				VBY.Basic.Utils.WriteColorLine("数据库初始化失败: " + ex.Message);
			}
			else
			{
				player.SendErrorMessage("数据库初始化失败: " + ex.Message);
			}
		}
	}

	public override void Initialize()
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected Obj, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected Obj, but got Unknown
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected Obj, but got Unknown
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Expected Obj, but got Unknown
		RestInit();
		Commands.ChatCommands.AddRange(AddCommands);
		PlayerHooks.PlayerPostLogin += OnPlayerPostLogin;
		PlayerHooks.PlayerPermission += OnPlayerPermission;
		PlayerHooks.PlayerLogout += OnPlayerLogout;
		TimeOutTimer.Elapsed += OnTimer;
		TimeOutTimer.Start();
		if (!ReadConfig.Root.对接称号插件)
		{
			HandlerCollection<ServerChatEventArgs> serverChat = ServerApi.Hooks.ServerChat;
			IEnumerable enumerable = (IEnumerable)((object)serverChat).GetType().GetField("registrations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(serverChat);
			PropertyInfo property = enumerable.GetType().GenericTypeArguments[0].GetProperty("Registrator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			PropertyInfo property2 = enumerable.GetType().GenericTypeArguments[0].GetProperty("Handler", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (object item in enumerable)
			{
				HookHandler<ServerChatEventArgs> val5 = (HookHandler<ServerChatEventArgs>)property2.GetValue(item);
				TerrariaPlugin val6 = (TerrariaPlugin)property.GetValue(item);
				if (val6 is TShock)
				{
					TShock.Log.ConsoleInfo("TShock server chat handled forced to de-register");
					ServerApi.Hooks.ServerChat.Deregister(val6, val5);
					ServerApi.Hooks.ServerChat.Register(val6, (HookHandler<ServerChatEventArgs>)OnChat);
				}
			}
		}
		MethodInfo[] methods = typeof(TShock).Assembly.GetType("TShockAPI.I18n").GetMethods();
		foreach (MethodInfo methodInfo in methods)
		{
			if (methodInfo.Name == "GetString" && methodInfo.GetParameters().Length == 2)
			{
				Utils.GetStringMethod = (Func<FormattableStringAdapter, object[], string>)Delegate.CreateDelegate(typeof(Func<FormattableStringAdapter, object[], string>), methodInfo);
			}
		}
		ServerApi.Hooks.GamePostInitialize.Register((TerrariaPlugin)(object)this, (HookHandler<EventArgs>)OnGamePostInitialize);
		if (ReadConfig.Root.对接称号插件)
		{
			global::称号插件.称号插件.挂接((TerrariaPlugin)(object)this);
		}
	}

	private void OnGamePostInitialize(EventArgs args)
	{
		if (CustomPlayerPluginHelpers.DB == null)
		{
			PluginInit();
		}
	}

	protected override void Dispose(bool disposing)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Expected Obj, but got Unknown
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected Obj, but got Unknown
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Expected Obj, but got Unknown
		if (disposing)
		{
			Commands.ChatCommands.RemoveRange(AddCommands);
			TimeOutTimer.Elapsed -= OnTimer;
			PlayerHooks.PlayerPostLogin -= OnPlayerPostLogin;
			PlayerHooks.PlayerPermission -= OnPlayerPermission;
			PlayerHooks.PlayerLogout -= OnPlayerLogout;
			ServerApi.Hooks.GamePostInitialize.Deregister((TerrariaPlugin)(object)this, (HookHandler<EventArgs>)OnGamePostInitialize);
			if (ReadConfig.Root.对接称号插件)
			{
				global::称号插件.称号插件.摘除((TerrariaPlugin)(object)this);
			}
			if (Utils.OwnsConnection)
			{
				CustomPlayerPluginHelpers.DB?.Dispose();
			}
			TimeOutTimer.Stop();
			TimeOutTimer.Dispose();
		}
		base.Dispose(disposing);
	}

	private void OnTimer(object? sender, ElapsedEventArgs e)
	{
		DateTime now = DateTime.Now;
		Utils.I("time checking");
		foreach (TimeOutObject timeOut in CustomPlayerPluginHelpers.TimeOutList)
		{
			Utils.I($"name:{timeOut.Name} type:{timeOut.Type} value:{timeOut.Value} outed:{timeOut.TimeOuted}");
			if (timeOut.TimeOuted || timeOut.DurationText == "-1" || !(timeOut.EndTime < now))
			{
				continue;
			}
			timeOut.TimeOuted = true;
			CustomPlayer customPlayer = Utils.FindPlayer(timeOut.Name);
			if (customPlayer == null)
			{
				continue;
			}
			switch (timeOut.Type)
			{
			case "Permission":
				customPlayer.Permissions.Remove(timeOut.Value);
				customPlayer.NegatedPermissions.Remove(timeOut.Value);
				customPlayer.Player.SendInfoMessage("权限:" + timeOut.Value + " 已过期");
				break;
			case "Prefix":
				if (timeOut.Value == customPlayer.Prefix)
				{
					customPlayer.Prefix = null;
					Utils.Query("update Useing set PrefixId = -1 where Name = @0 and ServerId = @1", customPlayer.Name, ReadConfig.Root.ServerId);
				}
				customPlayer.Player.SendInfoMessage("前缀:" + timeOut.Value + " 已过期");
				break;
			case "Suffix":
				if (timeOut.Value == customPlayer.Suffix)
				{
					customPlayer.Suffix = null;
					Utils.Query("update Useing set SuffixId = -1 where Name = @0 and ServerId = @1", customPlayer.Name, ReadConfig.Root.ServerId);
				}
				customPlayer.Player.SendInfoMessage("后缀:" + timeOut.Value + " 已过期");
				break;
			case "Group":
			{
				Group val = customPlayer.Group;
				customPlayer.Player.SendInfoMessage("组:" + timeOut.Value + " 已过期");
				customPlayer.HaveGroupNames.Remove(timeOut.Value);
				customPlayer.Player.SendInfoMessage("因组过期,你附加组:{0} 已清除", new object[1] { timeOut.Value });
				if (!(timeOut.Value == val.Name))
				{
					break;
				}
				if (ReadConfig.Root.CoverGroup)
				{
					if (customPlayer.HaveGroupNames.Count > 0)
					{
						customPlayer.Player.Group = customPlayer.HaveGroupNames.Select((string x) => (CustomPlayerPluginHelpers.Groups.GetGroupByName(x), CustomPlayerPluginHelpers.GroupGrade[x])).MaxBy(((Group, int) x) => x.Item2).Item1;
						customPlayer.Player.SendInfoMessage("你的组切换为更低级的组:{0}", new object[1] { customPlayer.Group.Name });
					}
					else
					{
						customPlayer.Player.Group = customPlayer.Group;
						customPlayer.Player.SendInfoMessage("你的组切换为原始组:{0}", new object[1] { customPlayer.Group.Name });
					}
				}
				else if (customPlayer.HaveGroupNames.Count > 0)
				{
					customPlayer.Group = customPlayer.HaveGroupNames.Select((string x) => (CustomPlayerPluginHelpers.Groups.GetGroupByName(x), CustomPlayerPluginHelpers.GroupGrade[x])).MaxBy(((Group, int) x) => x.Item2).Item1;
					customPlayer.Player.SendInfoMessage("你的组切换为更低级的组:{0}", new object[1] { customPlayer.Group.Name });
				}
				break;
			}
			}
		}
		List<TimeOutObject> list = CustomPlayerPluginHelpers.TimeOutList.FindAll((TimeOutObject x) => x.TimeOuted);
		list.ForEach((TimeOutObject x) =>
		{
			x.Delete();
		});
		CustomPlayerPluginHelpers.TimeOutList.RemoveRange(list);
	}

	private void OnChat(ServerChatEventArgs args)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_0505: Unknown result type (might be due to invalid IL or missing references)
		//IL_0335: Unknown result type (might be due to invalid IL or missing references)
		//IL_0345: Unknown result type (might be due to invalid IL or missing references)
		//IL_034a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0351: Unknown result type (might be due to invalid IL or missing references)
		//IL_0419: Unknown result type (might be due to invalid IL or missing references)
		//IL_042c: Unknown result type (might be due to invalid IL or missing references)
		if (((HandledEventArgs)(object)args).Handled)
		{
			return;
		}
		TSPlayer val = TShock.Players[args.Who];
		if (val == null)
		{
			((HandledEventArgs)(object)args).Handled = true;
			return;
		}
		if (args.Text.Length > 500)
		{
			val.Kick("Crash attempt via Long chat packet.", true, false, (string)null, false);
			((HandledEventArgs)(object)args).Handled = true;
			return;
		}
		string text = args.Text;
		foreach (KeyValuePair<LocalizedText, ChatCommandId> localizedCommand in ChatManager.Commands._localizedCommands)
		{
			if (localizedCommand.Value._name == args.CommandId._name)
			{
				text = (string.IsNullOrEmpty(text) ? localizedCommand.Key.Value : (localizedCommand.Key.Value + " " + text));
				break;
			}
		}
		if ((text.StartsWith(((ConfigFile<TShockSettings>)(object)TShock.Config).Settings.CommandSpecifier) || text.StartsWith(((ConfigFile<TShockSettings>)(object)TShock.Config).Settings.CommandSilentSpecifier)) && !string.IsNullOrEmpty(text.Substring(1)))
		{
			try
			{
				((HandledEventArgs)(object)args).Handled = true;
				if (!Commands.HandleCommand(val, text))
				{
					val.SendErrorMessage("Unable To parse command. Please contact an administrator For assistance.");
					TShock.Log.ConsoleError("Unable To parse command '{0}' from player {1}.", new object[2] { text, val.Name });
				}
				return;
			}
			catch (Exception ex)
			{
				TShock.Log.ConsoleError("An exception occurred executing a command.");
				TShock.Log.Error(ex.ToString());
				return;
			}
		}
		if (!val.HasPermission(Permissions.canchat))
		{
			((HandledEventArgs)(object)args).Handled = true;
			return;
		}
		if (val.mute)
		{
			val.SendErrorMessage("You are muted!");
			((HandledEventArgs)(object)args).Handled = true;
			return;
		}
		string args2 = null;
		string args3 = null;
		Color? val2 = null;
		if (val.IsLoggedIn)
		{
			CustomPlayer customPlayer = CustomPlayerPluginHelpers.Players[args.Who];
			if (customPlayer != null)
			{
				args2 = customPlayer.Prefix;
				args3 = customPlayer.Suffix;
				val2 = customPlayer.ChatColor;
			}
		}
		Color valueOrDefault = val2.GetValueOrDefault();
		if (!val2.HasValue)
		{
			valueOrDefault = new Color((int)val.Group.R, (int)val.Group.G, (int)val.Group.B);
			val2 = valueOrDefault;
		}
		if (((ConfigFile<TShockSettings>)(object)TShock.Config).Settings.EnableChatAboveHeads)
		{
			Player val3 = Main.player[args.Who];
			string name = val3.name;
			string chatAboveHeadsFormat = ((ConfigFile<TShockSettings>)(object)TShock.Config).Settings.ChatAboveHeadsFormat;
			_003C_003Ey__InlineArray4<object> buffer = default;
			buffer[0] = val.Group.Name;
			buffer[1] = args2.NullOrEmptyReturn(val.Group.Prefix);
			buffer[2] = val.Name;
			buffer[3] = args3.NullOrEmptyReturn(val.Group.Suffix);
			val3.name = string.Format(chatAboveHeadsFormat, (ReadOnlySpan<object?>)buffer);
			NetMessage.SendData(4, -1, -1, NetworkText.FromLiteral(val3.name), args.Who, 0f, 0f, 0f, 0, 0, 0);
			val3.name = name;
			if (PlayerHooks.OnPlayerChat(val, args.Text, ref text))
			{
				((HandledEventArgs)(object)args).Handled = true;
				return;
			}
			NetPacket val4 = NetTextModule.SerializeServerMessage(NetworkText.FromLiteral(name), val2.Value, Convert.ToByte(args.Who));
			NetManager.Instance.Broadcast(val4, args.Who);
			NetMessage.SendData(4, -1, -1, NetworkText.FromLiteral(name), args.Who, 0f, 0f, 0f, 0, 0, 0);
			string chatAboveHeadsFormat2 = ((ConfigFile<TShockSettings>)(object)TShock.Config).Settings.ChatAboveHeadsFormat;
			_003C_003Ey__InlineArray4<object> buffer2 = default;
			buffer2[0] = val.Group.Name;
			buffer2[1] = args2.NullOrEmptyReturn(val.Group.Prefix);
			buffer2[2] = val.Name;
			buffer2[3] = args3.NullOrEmptyReturn(val.Group.Suffix);
			string text2 = "<" + string.Format(chatAboveHeadsFormat2, (ReadOnlySpan<object?>)buffer2) + "> " + text;
			val.SendMessage(text2, val2.Value);
			((TSPlayer)TSPlayer.Server).SendMessage(text2, val2.Value);
			TShock.Log.Info("Broadcast: {0}", new object[1] { text2 });
			((HandledEventArgs)(object)args).Handled = true;
		}
		else
		{
			string chatFormat = ((ConfigFile<TShockSettings>)(object)TShock.Config).Settings.ChatFormat;
			_003C_003Ey__InlineArray5<object> buffer3 = default;
			buffer3[0] = val.Group.Name;
			buffer3[1] = args2.NullOrEmptyReturn(val.Group.Prefix);
			buffer3[2] = val.Name;
			buffer3[3] = args3.NullOrEmptyReturn(val.Group.Suffix);
			buffer3[4] = args.Text;
			text = string.Format(chatFormat, (ReadOnlySpan<object?>)buffer3);
			bool flag = PlayerHooks.OnPlayerChat(val, args.Text, ref text);
			((HandledEventArgs)(object)args).Handled = true;
			if (!flag)
			{
				TShock.Utils.Broadcast(text, val2.Value);
			}
		}
	}

	private void OnPlayerPermission(PlayerPermissionEventArgs args)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		if ((int)args.Result != 0 || args.Player.Index < 0 || args.Player.Index > 254)
		{
			return;
		}
		CustomPlayer customPlayer = CustomPlayerPluginHelpers.Players[args.Player.Index];
		if (customPlayer == null)
		{
			return;
		}
		if (customPlayer.NegatedPermissions.Contains(args.Permission))
		{
			args.Result = (PermissionHookResult)1;
			return;
		}
		if (customPlayer.Permissions.Contains(args.Permission))
		{
			args.Result = (PermissionHookResult)2;
		}
		if (!ReadConfig.Root.CoverGroup)
		{
			Group? val = customPlayer.Group;
			if (val != null && val.HasPermission(args.Permission))
			{
				args.Result = (PermissionHookResult)2;
			}
		}
	}

	public static void OnPlayerPostLogin(PlayerPostLoginEventArgs args)
	{
		// 兜底：这里任何异常都会冒到 TShock 的插件钩子里刷报错。数据库不可用时 Utils 已按「查不到数据」处理，这里再包一层。
		try
		{
			CustomPlayerPluginHelpers.Players[args.Player.Index] = CustomPlayer.Read(args.Player.Name, args.Player);
		}
		catch (Exception e)
		{
			TShock.Log.ConsoleError("[CustomPlayer] 登录读取玩家数据失败（已跳过，玩家可正常进服）：" + e.Message);
		}
	}

	public static void OnPlayerLogout(PlayerLogoutEventArgs args)
	{
		CustomPlayerPluginHelpers.Players[args.Player.Index] = null;
		CustomPlayerPluginHelpers.TimeOutList.RemoveAll((TimeOutObject x) => x.Name == args.Player.Name);
	}

	private void RestInit()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected Obj, but got Unknown
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected Obj, but got Unknown
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Expected Obj, but got Unknown
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected Obj, but got Unknown
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected Obj, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Expected Obj, but got Unknown
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected Obj, but got Unknown
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Expected Obj, but got Unknown
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Expected Obj, but got Unknown
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Expected Obj, but got Unknown
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Expected Obj, but got Unknown
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Expected Obj, but got Unknown
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Expected Obj, but got Unknown
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Expected Obj, but got Unknown
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Expected Obj, but got Unknown
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Expected Obj, but got Unknown
		((Rest)TShock.RestApi).Register((RestCommand)new SecureRestCommand("/custom/add/perm", (RestCommandD)((RestRequestArgs args) => RestAdd(args, "Perm")), new string[1] { "custom.admin" }));
		((Rest)TShock.RestApi).Register((RestCommand)new SecureRestCommand("/custom/add/prefix", (RestCommandD)((RestRequestArgs args) => RestAdd(args, "Prefix")), new string[1] { "custom.admin" }));
		((Rest)TShock.RestApi).Register((RestCommand)new SecureRestCommand("/custom/add/suffix", (RestCommandD)((RestRequestArgs args) => RestAdd(args, "Suffix")), new string[1] { "custom.admin" }));
		((Rest)TShock.RestApi).Register((RestCommand)new SecureRestCommand("/custom/list/perm", (RestCommandD)((RestRequestArgs args) => RestList(args, "Perm")), new string[1] { "custom.admin" }));
		((Rest)TShock.RestApi).Register((RestCommand)new SecureRestCommand("/custom/list/prefix", (RestCommandD)((RestRequestArgs args) => RestList(args, "Prefix")), new string[1] { "custom.admin" }));
		((Rest)TShock.RestApi).Register((RestCommand)new SecureRestCommand("/custom/list/suffix", (RestCommandD)((RestRequestArgs args) => RestList(args, "Suffix")), new string[1] { "custom.admin" }));
		((Rest)TShock.RestApi).Register((RestCommand)new SecureRestCommand("custom/wear/prefix", (RestCommandD)((RestRequestArgs args) => RestWear(args, "Prefix")), new string[1] { "custom.admin" }));
		((Rest)TShock.RestApi).Register((RestCommand)new SecureRestCommand("custom/wear/suffix", (RestCommandD)((RestRequestArgs args) => RestWear(args, "Suffix")), new string[1] { "custom.admin" }));
	}

	private object RestAdd(RestRequestArgs args, string type)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		RestObject val = new RestObject("200");
		if (CustomPlayerPluginHelpers.DB == null)
		{
			return SetError(val, "数据库未初始化,请等服务器完成启动后重试");
		}
		if (((IEnumerable<EscapedParameter>)args.Parameters).Count() < 3)
		{
			return SetError(val, "参数不足3个");
		}
		string text = args.Parameters["name"];
		string title = args.Parameters["value"];
		string text2 = args.Parameters["time"];
		if (string.IsNullOrEmpty(text))
		{
			return SetError(val, "name is empty");
		}
		if (string.IsNullOrEmpty(title))
		{
			return SetError(val, "value is empty");
		}
		if (string.IsNullOrEmpty(text2))
		{
			return SetError(val, "time is empty");
		}
		DateTime startTime = DateTime.Now;
		DateTime endTime = default;
		TimeSpan addTime = default;
		bool flag = text2 == "-1";
		if (TimeParse(flag, text2, ref startTime, ref endTime, ref addTime, null))
		{
			return SetError(val, "时间转换失败");
		}
		if (type == "Perm")
		{
			if (Utils.NotifyPlayer("权限", flag, new TimeOutObject(text, title, "Permission", startTime, endTime, text2), out CustomPlayer cply))
			{
				cply.AddPermission(title);
			}
			Utils.Query("insert into ExpirationInfo(Name,Value,Type,StartTime,EndTime,DurationText) values(@0,@1,@2,@3,@4,@5)", text, title, "Permission", startTime, endTime, text2);
			val.Response = (flag ? $"添加成功 玩家:{text} 权限:{title} 持续时间:永久" : $"添加成功 玩家:{text} 权限:{title} 起始时间:{startTime} 结束时间:{endTime} 持续时间:{addTime}");
		}
		else
		{
			int num = 0;
			bool flag2 = type == "Prefix";
			string text3 = (flag2 ? "前缀" : "后缀");
			if (TitleParse(type, ref title, null))
			{
				return SetError(val, "get未找到值");
			}
			QueryResult val2 = Utils.QueryReader("select max(Id) from " + type + " where Name = @0", text);
			try
			{
				if (val2.Read() && !val2.Reader.IsDBNull(0))
				{
					num = val2.Reader.GetInt32(0) + 1;
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			TimeOutObject data = new TimeOutObject(text, title, type, startTime, endTime, text2, num);
			if (Utils.NotifyPlayer(text3, flag, data, out CustomPlayer cply2))
			{
				(flag2 ? cply2.PrefixList : cply2.SuffixList).Add(data);
			}
			Utils.Query("insert into " + type + "(Name,Id,Value,StartTime,EndTime,DurationText) values(@0,@1,@2,@3,@4,@5)", text, num, title, startTime, endTime, text2);
			val.Response = (flag ? $"添加成功 玩家:{text} {text3}:{title} Id:{num} 持续时间:永久" : $"添加成功 玩家:{text} {text3}:{title} Id:{num} 起始时间:{startTime} 结束时间:{endTime} 持续时间:{addTime}");
		}
		return val;
	}

	private object RestList(RestRequestArgs args, string type)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		RestObject val = new RestObject("200");
		if (CustomPlayerPluginHelpers.DB == null)
		{
			return SetError(val, "数据库未初始化,请等服务器完成启动后重试");
		}
		string text = args.Parameters["name"];
		if (string.IsNullOrEmpty(text))
		{
			return SetError(val, "name is empty");
		}
		if (type == "Perm")
		{
			List<PermInfo> list = new List<PermInfo>();
			QueryResult val2 = Utils.QueryReader("select Value,EndTime,DurationText from ExpirationInfo where name = @0 AND Type = @1", text, "Permission");
			try
			{
				if (val2.Read())
				{
					val2.Reader.DoForEach((IDataReader x) =>
					{
						list.Add(new PermInfo(x.GetString("Value"), (x.GetString("DurationText") == "-1") ? "永久" : (x.GetDateTime("EndTime") - DateTime.Now).ToString("d\\.hh\\:mm\\:ss")));
					});
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			QueryResult val3 = Utils.QueryReader("select Commands from PlayerList where name = @0", text);
			try
			{
				if (val3.Read() && !val3.Reader.IsDBNull(0))
				{
					string[] array = val3.Reader.GetString(0).Split(",", StringSplitOptions.RemoveEmptyEntries);
					foreach (string perm in array)
					{
						list.Add(new PermInfo(perm, "永久"));
					}
				}
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			((Dictionary<string, object>)(object)val).Add("Permissions", (object)list);
		}
		else
		{
			List<TitleInfo> list2 = new List<TitleInfo>();
			QueryResult val4 = ((type == "Prefix") ? Utils.PrefixQuery(text) : Utils.SuffixQuery(text));
			try
			{
				if (val4.Read())
				{
					val4.Reader.DoForEach((IDataReader x) =>
					{
						list2.Add(new TitleInfo(x.GetString("Value"), x.GetInt32("Id"), (x.GetString("DurationText") == "-1") ? "永久" : (x.GetDateTime("EndTime") - DateTime.Now).ToString("d\\.hh\\:mm\\:ss")));
					});
				}
				((Dictionary<string, object>)(object)val).Add(type + "s", (object)list2);
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
		}
		return val;
	}

	private object RestWear(RestRequestArgs args, string type)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		RestObject val = new RestObject("200");
		if (CustomPlayerPluginHelpers.DB == null)
		{
			return SetError(val, "数据库未初始化,请等服务器完成启动后重试");
		}
		string text = args.Parameters["name"];
		bool flag = type == "Prefix";
		string text2 = (flag ? "前缀" : "后缀");
		if (!int.TryParse(args.Parameters["id"], out var result))
		{
			return SetError(val, "转换" + text2 + "Id失败");
		}
		if (!int.TryParse(args.Parameters["serverid"], out var result2))
		{
			return SetError(val, "转换ServerId失败");
		}
		bool flag2 = false;
		QueryResult val2 = Utils.TitleQuery(type, text, result);
		try
		{
			flag2 = val2.Read();
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		if (!flag2)
		{
			SetError(val, $"玩家:{text} {text2}Id:{result} 未找到");
			return val;
		}
		bool flag3 = false;
		QueryResult val3 = Utils.QueryReader("select PrefixId,SuffixId from Useing where Name = @0 and ServerId = @1", text, result2);
		try
		{
			flag3 = val3.Read();
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
		if (flag3)
		{
			Utils.Query("update Useing set " + type + "Id = @0 where Name = @1 and ServerId = @2", result, text, result2);
		}
		else
		{
			Utils.Query("insert into Useing(Name,ServerId,PrefixId,SuffixId) values(@0,@1,@2,@3)", text, result2, flag ? result : (-1), flag ? (-1) : result);
		}
		val.Response = $"已为玩家:{text} 佩戴{text2}Id:{result} 到服务器:{result2}";
		return val;
	}

	private RestObject SetError(RestObject obj, string error)
	{
		obj.Status = "400";
		obj.Error = error;
		return obj;
	}
}
