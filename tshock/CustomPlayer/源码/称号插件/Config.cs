using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using TShockAPI;

namespace 称号插件;

public class Config
{
	public class 名称
	{
		public string 玩家用户名 = "";

		public string? 前前缀 = "";

		public string? 前缀;

		public string? 角色名;

		public string? 后缀;

		public string? 后后缀 = "";

		public Color? 颜色;
	}

	public class 聊天信息
	{
		public string? 前前缀 = "";

		public string? 前缀;

		public string? 角色名;

		public string? 后缀;

		public string? 后后缀 = "";

		public Color? 颜色;
	}

	public string 说明1 = "称号设置中,前缀、名称、后缀设置为null则与玩家当前用户组的前后缀一致";

	public string 说明2 = "给称号指令:/称号 . 重载指令:/reload . 权限:“称号”";

	public string 说明3 = "改称号指令:/改称号  . 权限:“改称号”用于改自己的称号（需要先有一个称号）,可以给权限以允许普通玩家使用";

	public string 说明4 = "已设置的称号不受最长字符影响,用于防止普通玩家将自己的称号修改过长";

	public bool 是否开启称号插件 = true;

	public bool 启用远程配置;

	public string 远程配置接口 = "http://";

	public string 聊天格式 = "{前前缀}{前缀}{角色名}{后缀}{后后缀}: {聊天内容}";

	public string 聊天气泡启用时玩家名称格式 = "{前前缀}{前缀}{角色名}{后缀}{后后缀}";

	public bool 是否开启聊天气泡;

	public int 称号最大字符数 = 13;

	public bool 是否允许管理绕过最大字符数检测 = true;

	public bool 是否允许无称号玩家给自己称号;

	public List<名称> 称号设置 = new List<名称>();

	public static void GetConfig()
	{
		try
		{
			string text = "tshock/Chrome.Title.json";
			if (!File.Exists(text))
			{
				FileTools.CreateIfNot(Path.Combine((string)text), JsonConvert.SerializeObject((object)称号插件.配置, (Formatting)1));
				称号插件.配置 = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Path.Combine((string)text)));
				File.WriteAllText(text, JsonConvert.SerializeObject((object)称号插件.配置, (Formatting)1));
			}
		}
		catch
		{
			((TSPlayer)TSPlayer.Server).SendErrorMessage("[Chrome.Title]配置文件读取错误！！！");
		}
	}
}
