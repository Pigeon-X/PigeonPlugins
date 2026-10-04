using System;
using Newtonsoft.Json;
using VBY.Basic.Config;

namespace CustomPlayer;

public class Root : MainRoot
{
	public int ServerId;

	public string DatabaseType = "Mysql";

	public string MysqlHost;

	public string MysqlUser;

	public string MysqlPass;

	public string MysqlDatabase;

	public uint MysqlPort;

	public bool Debug;

	public bool EnableGroup = true;

	public bool EnablePrefix = true;

	public bool EnableSuffix = true;

	public bool EnablePermission = true;

	public bool CoverGroup = true;

	public bool 对接称号插件;

	[JsonIgnore]
	public bool UseTShockDatabase => string.Equals(DatabaseType, "TShock", StringComparison.OrdinalIgnoreCase);
}
