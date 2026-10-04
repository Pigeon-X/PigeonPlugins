using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Xna.Framework;
using TShockAPI;
using TShockAPI.DB;
using VBY.Basic.Extension;
using 称号插件;

namespace CustomPlayer;

public class CustomPlayer
{
	public string Name;

	public TSPlayer Player;

	public Group? Group;

	public List<string> Permissions = new List<string>();

	public List<string> NegatedPermissions = new List<string>();

	private string? prefix;

	private string? suffix;

	private Color? chatColor;

	public List<TableInfo.Prefix> PrefixList = new List<TableInfo.Prefix>();

	public List<TableInfo.Prefix> SuffixList = new List<TableInfo.Prefix>();

	public List<string> HaveGroupNames = new List<string>();

	public static bool 对接称号插件 { get; set; } = true;

	public string? Prefix
	{
		get
		{
			if (!对接称号插件)
			{
				return prefix;
			}
			return global::称号插件.称号插件.获取聊天信息(Name).前缀;
		}
		set
		{
			if (对接称号插件)
			{
				global::称号插件.称号插件.获取聊天信息(Name).前缀 = value;
			}
			else
			{
				prefix = value;
			}
		}
	}

	public string? Suffix
	{
		get
		{
			if (!对接称号插件)
			{
				return suffix;
			}
			return global::称号插件.称号插件.获取聊天信息(Name).后缀;
		}
		set
		{
			if (对接称号插件)
			{
				global::称号插件.称号插件.获取聊天信息(Name).后缀 = value;
			}
			else
			{
				suffix = value;
			}
		}
	}

	public Color? ChatColor
	{
		get
		{
			if (!对接称号插件)
			{
				return chatColor;
			}
			return global::称号插件.称号插件.获取聊天信息(Name).颜色;
		}
		set
		{
			if (对接称号插件)
			{
				global::称号插件.称号插件.获取聊天信息(Name).颜色 = value;
			}
			else
			{
				chatColor = value;
			}
		}
	}

	public CustomPlayer(string name, TSPlayer player)
	{
		Name = name;
		Player = player;
	}

	public void AddPermission(string permission)
	{
		if (permission.StartsWith('!'))
		{
			NegatedPermissions.Add(permission.Substring(1));
		}
		else
		{
			Permissions.Add(permission);
		}
	}

	public bool DelPermission(string permission)
	{
		if (!permission.StartsWith('!'))
		{
			return Permissions.Remove(permission);
		}
		return NegatedPermissions.Remove(permission.Substring(1));
	}

	public static CustomPlayer Read(string name, TSPlayer player)
	{
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		CustomPlayer cply = new CustomPlayer(name, player);
		QueryResult val = Utils.QueryPlayer(name);
		try
		{
			if (val.Read())
			{
				if (CustomPlayerPlugin.ReadConfig.Root.EnablePermission && !val.Reader.IsDBNull(0))
				{
					string[] array = val.Reader.GetString("Commands").Split(',', StringSplitOptions.RemoveEmptyEntries);
					foreach (string permission in array)
					{
						cply.AddPermission(permission);
					}
				}
				if (!val.Reader.IsDBNull(2))
				{
					string[] array2 = val.Reader.GetString("ChatColor").Split(',');
					cply.ChatColor = new Color(int.Parse(array2[0]), int.Parse(array2[1]), int.Parse(array2[2]));
				}
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		if (CustomPlayerPlugin.ReadConfig.Root.EnablePermission)
		{
			QueryResult val2 = Utils.QueryReader("select Value,StartTime,EndTime,DurationText from ExpirationInfo where Type = 'Permission' AND Name = @0", name);
			try
			{
				val2.Reader.ForEach((IDataReader x) =>
				{
					TimeOutObject timeOutObject = x.GetTimeOutObject(name, "Permission");
					if (timeOutObject.NoExpired)
					{
						CustomPlayerPluginHelpers.TimeOutList.Add(timeOutObject);
						cply.AddPermission(timeOutObject.Value);
					}
					else
					{
						player.SendInfoMessage("权限:" + timeOutObject.Value + " 已过期");
						timeOutObject.Delete();
					}
				});
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		bool flag = false;
		if (CustomPlayerPlugin.ReadConfig.Root.EnableGroup)
		{
			QueryResult val3 = Utils.QueryReader("select Value,StartTime,EndTime,DurationText from ExpirationInfo where Type = 'Group' AND Name = @0", name);
			try
			{
				val3.Reader.ForEach((IDataReader x) =>
				{
					TimeOutObject timeOutObject = x.GetTimeOutObject(name, "Group");
					if (timeOutObject.NoExpired)
					{
						cply.HaveGroupNames.Add(timeOutObject.Value);
						CustomPlayerPluginHelpers.TimeOutList.Add(timeOutObject);
					}
					else
					{
						player.SendInfoMessage("组:" + timeOutObject.Value + " 已过期");
						timeOutObject.Delete();
					}
				});
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			Group val4 = null;
			List<string> list = new List<string>();
			foreach (string haveGroupName in cply.HaveGroupNames)
			{
				if (CustomPlayerPluginHelpers.Groups.GroupExists(haveGroupName))
				{
					if (val4 == null)
					{
						val4 = CustomPlayerPluginHelpers.Groups.GetGroupByName(haveGroupName);
						continue;
					}
					Group groupByName = CustomPlayerPluginHelpers.Groups.GetGroupByName(haveGroupName);
					if (CustomPlayerPluginHelpers.GroupGrade[val4.Name] < CustomPlayerPluginHelpers.GroupGrade[groupByName.Name])
					{
						val4 = groupByName;
					}
				}
				else
				{
					player.SendErrorMessage("组:{0} 不存在", new object[1] { haveGroupName });
					((TSPlayer)TSPlayer.Server).SendErrorMessage("[CustomPlayerPlugin]组:{0} 不存在", new object[1] { haveGroupName });
					TShock.Log.Error("[CustomPlayerPlugin]组:{0} 不存在", new object[1] { haveGroupName });
					list.Add(haveGroupName);
				}
			}
			cply.HaveGroupNames.RemoveRange(list);
			cply.Group = val4;
			if (CustomPlayerPlugin.ReadConfig.Root.CoverGroup && val4 != null)
			{
				cply.Group = player.Group;
				player.Group = val4;
				flag = true;
			}
		}
		if (CustomPlayerPlugin.ReadConfig.Root.EnablePrefix)
		{
			QueryResult val5 = Utils.PrefixQuery(name);
			try
			{
				val5.Reader.ForEach((IDataReader x) =>
				{
					TimeOutObject timeOutObject = new TimeOutObject(name, x.GetString("Value"), "Prefix", x.GetDateTime("StartTime"), x.GetDateTime("EndTime"), x.GetString("DurationText"), x.GetInt32("Id"));
					if (timeOutObject.NoExpired)
					{
						cply.PrefixList.Add(new TableInfo.Prefix(name, timeOutObject.Id, timeOutObject.Value, timeOutObject.StartTime, timeOutObject.EndTime, timeOutObject.DurationText));
						CustomPlayerPluginHelpers.TimeOutList.Add(timeOutObject);
					}
					else
					{
						player.SendInfoMessage("前缀:" + timeOutObject.Value + " 已过期");
						timeOutObject.Delete();
					}
				});
			}
			finally
			{
				((IDisposable)val5)?.Dispose();
			}
		}
		if (CustomPlayerPlugin.ReadConfig.Root.EnableSuffix)
		{
			QueryResult val6 = Utils.SuffixQuery(name);
			try
			{
				val6.Reader.ForEach((IDataReader x) =>
				{
					TimeOutObject timeOutObject = new TimeOutObject(name, x.GetString("Value"), "Suffix", x.GetDateTime("StartTime"), x.GetDateTime("EndTime"), x.GetString("DurationText"), x.GetInt32("Id"));
					if (timeOutObject.NoExpired)
					{
						cply.SuffixList.Add(new TableInfo.Prefix(name, timeOutObject.Id, timeOutObject.Value, timeOutObject.StartTime, timeOutObject.EndTime, timeOutObject.DurationText));
						CustomPlayerPluginHelpers.TimeOutList.Add(timeOutObject);
					}
					else
					{
						player.SendInfoMessage("后缀:" + timeOutObject.Value + " 已过期");
						timeOutObject.Delete();
					}
				});
			}
			finally
			{
				((IDisposable)val6)?.Dispose();
			}
		}
		if (对接称号插件)
		{
			global::称号插件.称号插件.称号信息[name] = new 称号插件.Config.聊天信息
			{
				前前缀 = "",
				前缀 = null,
				角色名 = null,
				后缀 = null,
				后后缀 = ""
			};
		}
		bool flag2 = false;
		int useingPrefixId = -1;
		int useingSuffixId = -1;
		QueryResult val7 = Utils.QueryReader("select PrefixId,SuffixId from Useing where Name = @0 and ServerId = @1", name, CustomPlayerPlugin.ReadConfig.Root.ServerId);
		try
		{
			if (val7.Read())
			{
				flag2 = true;
				useingPrefixId = val7.Reader.GetInt32("PrefixId");
				useingSuffixId = val7.Reader.GetInt32("SuffixId");
			}
		}
		finally
		{
			((IDisposable)val7)?.Dispose();
		}
		if (flag2)
		{
			cply.Prefix = cply.PrefixList.Find((TableInfo.Prefix x) => x.Id == useingPrefixId)?.Value;
			cply.Suffix = cply.SuffixList.Find((TableInfo.Prefix x) => x.Id == useingSuffixId)?.Value;
		}
		else
		{
			Utils.Query("insert into Useing(Name,ServerId,PrefixId,SuffixId) values(@0,@1,-1,-1)", name, CustomPlayerPlugin.ReadConfig.Root.ServerId);
		}
		object[] array3 = new object[3]
		{
			name,
			flag ? "conver" : "",
			null
		};
		object obj;
		if (!flag)
		{
			Group? val8 = cply.Group;
			obj = ((val8 != null) ? val8.Name : null) ?? "null";
		}
		else
		{
			obj = player.Group.Name;
		}
		array3[2] = obj;
		Utils.I("return CustomPlayer:{0} {1}group:{2}", array3);
		return cply;
	}
}
