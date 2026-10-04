using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using GetText;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;
using VBY.Basic;
using VBY.Basic.Extension;

namespace CustomPlayer;

public static class Utils
{
	public static Func<FormattableStringAdapter, object[], string> GetStringMethod;

	public static bool OwnsConnection => !CustomPlayerPlugin.ReadConfig.Root.UseTShockDatabase;

	public static SqlTable SqlTableCreate(Type tableClassType)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected Obj, but got Unknown
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Expected Obj, but got Unknown
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Expected Obj, but got Unknown
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Expected Obj, but got Unknown
		FieldInfo[] fields = tableClassType.GetFields();
		List<SqlColumn> list = new List<SqlColumn>(fields.Length);
		foreach (FieldInfo item in fields.OrderBy((FieldInfo x) => x.MetadataToken))
		{
			if (item.FieldType == TypeOf.Int32)
			{
				list.Add(new SqlColumn(item.Name, (MySqlDbType)3));
			}
			else if (item.FieldType == TypeOf.DateTime)
			{
				list.Add(new SqlColumn(item.Name, (MySqlDbType)12));
			}
			else if (item.FieldType == TypeOf.String)
			{
				list.Add(new SqlColumn(item.Name, (MySqlDbType)752, item.GetCustomAttribute<LengthAttribute>()?.Length));
			}
		}
		return new SqlTable(tableClassType.Name, list.ToArray());
	}

	public static string GetString(FormattableStringAdapter text, params object[] args)
	{
		return GetStringMethod(text, args);
	}

	public static CustomPlayer? FindPlayer(string name)
	{
		return CustomPlayerPluginHelpers.Players.Find((CustomPlayer x) => x?.Name == name);
	}

	public static bool CreateConnection()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Expected Obj, but got Unknown
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Expected Obj, but got Unknown
		if (CustomPlayerPlugin.ReadConfig.Root.UseTShockDatabase)
		{
			if (TShock.DB == null)
			{
				return false;
			}
			CustomPlayerPluginHelpers.DB = TShock.DB;
			return true;
		}
		CustomPlayerPluginHelpers.DB = (IDbConnection)new MySqlConnection(((DbConnectionStringBuilder)new MySqlConnectionStringBuilder
		{
			Server = CustomPlayerPlugin.ReadConfig.Root.MysqlHost,
			Port = CustomPlayerPlugin.ReadConfig.Root.MysqlPort,
			Database = CustomPlayerPlugin.ReadConfig.Root.MysqlDatabase,
			UserID = CustomPlayerPlugin.ReadConfig.Root.MysqlUser,
			Password = CustomPlayerPlugin.ReadConfig.Root.MysqlPass
		}).ConnectionString);
		return true;
	}

	public static string QuoteIdent(string name)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Invalid comparison between Unknown and I4
		if ((int)DbExt.GetSqlType(CustomPlayerPluginHelpers.DB) != 2)
		{
			return "\"" + name + "\"";
		}
		return "`" + name + "`";
	}

	/// <summary>数据库是否可用（没建起来 / 建起来但连不上，都算不可用）。</summary>
	public static bool DbReady => CustomPlayerPluginHelpers.DB != null;

	private static int _dbWarned;
	private static int _dbErrorWarned;

	/// <summary>
	/// 数据库不可用时给一个「空结果」：TShock 的 QueryResult 对 null 是安全的
	/// （Read() 返回 false、Get() 返回 default、Dispose 全空判断），
	/// 所以调用方完全不用改 —— 读=查不到行、写=不做，玩家照常进服，不会再把异常抛给 TShock 刷屏。
	/// </summary>
	private static QueryResult EmptyResult()
	{
		WarnDbUnavailable();
		return new QueryResult(null, null, null);
	}

	private static void WarnDbUnavailable()
	{
		if (Interlocked.Exchange(ref _dbWarned, 1) == 0)
		{
			TShock.Log.ConsoleError("[CustomPlayer] 数据库不可用（CustomPlayer.json 读取失败，或 MySQL 连不上）。本插件的称号 / 权限 / 分组数据本次全部跳过，玩家仍可正常进服；数据库修好后 /custom reload 或重启服务器即可恢复。");
		}
	}

	private static void WarnDbErrorOnce(Exception e)
	{
		if (Interlocked.Exchange(ref _dbErrorWarned, 1) == 0)
		{
			TShock.Log.ConsoleError("[CustomPlayer] 数据库查询失败，已按“查不到数据”处理（后续同类错误不再刷屏）：" + e.Message);
		}
	}

	public static int Query(string commandText, params object[] args)
	{
		if (CustomPlayerPluginHelpers.DB == null)
		{
			WarnDbUnavailable();
			return 0;
		}
		try
		{
			return DbExt.Query(CustomPlayerPluginHelpers.DB, commandText, args);
		}
		catch (Exception e)
		{
			WarnDbErrorOnce(e);
			return 0;
		}
	}

	public static QueryResult QueryReader(string commandText, params object[] args)
	{
		if (CustomPlayerPluginHelpers.DB == null)
		{
			return EmptyResult();
		}
		try
		{
			return DbExt.QueryReader(CustomPlayerPluginHelpers.DB, commandText, args);
		}
		catch (Exception e)
		{
			WarnDbErrorOnce(e);
			return EmptyResult();
		}
	}

	public static QueryResult QueryPlayer(string name)
	{
		return QueryReader("select Commands," + QuoteIdent("Group") + ",ChatColor from PlayerList where Name = @0", name);
	}

	public static QueryResult TitleQuery(string type, string name)
	{
		return QueryReader("select Id,Value,StartTime,EndTime,DurationText from " + type + " where Name  = @0", name);
	}

	public static QueryResult TitleQuery(string type, string name, int titleId)
	{
		return QueryReader("select Value,StartTime,EndTime,DurationText from " + type + " where Name  = @0 AND Id = @1", name, titleId);
	}

	public static QueryResult PrefixQuery(string name)
	{
		return TitleQuery("Prefix", name);
	}

	public static QueryResult SuffixQuery(string name)
	{
		return TitleQuery("Suffix", name);
	}

	public static void I(string msg)
	{
		if (CustomPlayerPlugin.ReadConfig.Root.Debug)
		{
			((TSPlayer)TSPlayer.Server).SendInfoMessage(msg);
		}
	}

	public static void I(string msg, params object[] args)
	{
		if (CustomPlayerPlugin.ReadConfig.Root.Debug)
		{
			((TSPlayer)TSPlayer.Server).SendInfoMessage(msg, args);
		}
	}

	public static bool NotifyPlayer(string typeChinese, bool forever, TimeOutObject data, out CustomPlayer cply)
	{
		cply = FindPlayer(data.Name);
		if (cply != null)
		{
			if (forever)
			{
				cply.Player.SendInfoMessage("你已获得永久" + typeChinese + ":" + data.Value);
			}
			else
			{
				cply.Player.SendInfoMessage("你获得" + typeChinese + ":" + data.Value);
			}
			CustomPlayerPluginHelpers.TimeOutList.Add(data);
			return true;
		}
		return false;
	}

	public static string NullOrEmptyReturn(this string? args, string value)
	{
		if (!string.IsNullOrEmpty(args))
		{
			return args;
		}
		return value;
	}

	public static CustomPlayer GetPlayer(this TSPlayer player)
	{
		return CustomPlayerPluginHelpers.Players[player.Index];
	}

	public static void Reload(this CustomPlayer cply)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Expected Obj, but got Unknown
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Expected Obj, but got Unknown
		CustomPlayerPlugin.OnPlayerLogout(new PlayerLogoutEventArgs(cply.Player));
		CustomPlayerPlugin.OnPlayerPostLogin(new PlayerPostLoginEventArgs(cply.Player));
	}

	public static TimeOutObject GetTimeOutObject(this IDataReader reader, string name, string type)
	{
		return new TimeOutObject(name, reader.GetString("Value"), type, reader.GetDateTime("StartTime"), reader.GetDateTime("EndTime"), reader.GetString("DurationText"));
	}

	public static void Add(this List<TableInfo.Prefix> list, TimeOutObject data)
	{
		list.Add(new TableInfo.Prefix(data.Name, data.Id, data.Value, data.StartTime, data.EndTime, data.DurationText));
	}
}
