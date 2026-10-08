using Newtonsoft.Json;
using TShockAPI;
namespace UserCheck;

public class Config
{
    private static string ConfigPath = Path.Combine(TShock.SavePath, "HelpPlus.json");

    public static Config Settings = new();
    
    [JsonProperty("每页行数")] 
    public int PageSize = 30;

    [JsonProperty("每行字数")] 
    public int WithSize = 120;
    
    [JsonProperty("按照字母顺序")] 
    public bool OrderByLetter;
    
    [JsonProperty("简短提示开关")] public bool DisPlayShort = true;
    
    [JsonProperty("简短提示对应")]
    public Dictionary<string, string> ShortCommands;
    public Config()
    {
        this.ShortCommands = new Dictionary<string, string>
        {
            { "分界线", "分界线" },
            { "1", "数字" },
            { "user", "用户" },
            { "login", "登录" },
            { "logout", "登出" },
            { "password", "改密" },
            { "accountinfo", "账号" },
            { "ban", "封禁" },
            { "broadcast", "广播" },
            { "displaylogs", "日志" },
            { "group", "组管理" },
            { "itemban", "禁物品" },
            { "projban", "禁弹幕" },
            { "tileban", "禁图格" },
            { "region", "区域" },
            { "kick", "踢人" },
            { "mute", "禁言" },
            { "overridessc", "改SSC" },
            { "savessc", "存SSC" },
            { "uploadssc", "传SSC" },
            { "tempgroup", "临时组" },
            { "su", "临时超管" },
            { "sudo", "代超管" },
            { "userinfo", "玩家信息" },
            { "annoy", "打扰" },
            { "rocket", "上天" },
            { "firework", "烟火" },
            { "checkupdates", "查更新" },
            { "off", "关服存" },
            { "off-nosave", "关服" },
            { "reload", "重载" },
            { "serverpassword", "服密码" },
            { "version", "版本" },
            { "whitelist", "白名单" },
            { "give", "给物品" },
            { "item", "给物品" },
            { "butcher", "杀生物" },
            { "renamenpc", "改NPC" },
            { "maxspawns", "刷怪数" },
            { "spawnboss", "Boss" },
            { "spawnmob", "召生物" },
            { "spawnrate", "刷怪率" },
            { "clearangler", "清渔夫" },
            { "home", "回家" },
            { "spawn", "出生点" },
            { "tp", "传送" },
            { "tphere", "拉人" },
            { "tpnpc", "传NPC" },
            { "tppos", "传坐标" },
            { "pos", "看坐标" },
            { "tpallow", "传送保护" },
            { "worldmode", "世界难度" },
            { "antibuild", "建筑保护" },
            { "grow", "种植" },
            { "forcehalloween", "万圣节" },
            { "forcexmas", "圣诞节" },
            { "worldevent", "事件" },
            { "hardmode", "困难" },
            { "evil", "邪恶" },
            { "protectspawn", "护出生" },
            { "save", "存世界" },
            { "setspawn", "设出生" },
            { "setdungeon", "设地牢" },
            { "settle", "平衡液体" },
            { "time", "时间" },
            { "wind", "风速" },
            { "worldinfo", "地图信息" },
            { "buff", "增益" },
            { "clear", "清理" },
            { "gbuff", "增益" },
            { "godmode", "上帝" },
            { "heal", "治疗" },
            { "kill", "杀死" },
            { "me", "发消息" },
            { "party", "队内" },
            { "reply", "回复" },
            { "rest", "REST" },
            { "slap", "伤害" },
            { "serverinfo", "服信息" },
            { "warp", "传送点" },
            { "whisper", "私聊" },
            { "wallow", "私聊保护" },
            { "dump-reference-data", "生成文档" },
            { "sync", "同步" },
            { "respawn", "复活" },
            { "aliases", "别名" },
            { "motd", "进服提示" },
            { "playing", "在线" },
            { "rules", "规则" },
            { "death", "死亡" },
            { "pvpdeath", "对战死" },
            { "alldeath", "全体死" },
            { "allpvpdeath", "全对战" },
            { "bossdamage", "伤害" },
            { "pco", "计划书" },
            { "pout", "计划导出" },
            { "bak", "备份" },
            { "tv", "查看" },
            { "pbackup", "计划备份" },
            { "prestore", "计划恢复" },
            { "poutreset", "重置导出" },
            { "ac", "反作弊" },
            { "开关", "控制" },
            { "bear", "熊宠" },
            { "custom", "自定义" },
            { "go", "传送" },
            { "dimip", "维度" },
            { "help", "帮助" },
            { "hr", "热载" },
            { "peace", "和平" },
            { "bosslimit", "限制" },
            { "register", "注册" },
            { "跨服", "跨服" },
            { "返回", "返回" },
            { "跨服密码", "密码" },
            { "curfew", "宵禁" },
            { "alias", "别名" },
            { "shopui", "商店UI" },
            { "runas", "代执行" },
            { "banp", "封禁" },
            { "remove", "移除" },
            { "find", "查找" },
            { "pvp", "对战" },
            { "pvplock", "锁战" },
            { "team", "队伍" },
            { "teamlock", "锁队" },
            { "进度", "进度" },
            { "planoff", "关计划" },
            { "chestfix", "修箱" },
            { "ping", "延迟" },
            { "statustext", "计分板" },
            { "autoregister", "自动注册" },
            { "export", "导出" },
            { "pwd", "密码" },
            { "scan", "扫描" },
            { "projlist", "弹幕表" },
            { "scanlist", "扫描表" },
            { "lightning", "闪电" },
            { "mg", "小游戏" },
            { "小游戏", "小游戏" },
            { "cnspvp", "对战" },
            { "join", "加入" },
            { "称号", "称号" },
            { "改称号", "改称号" },
            { "经济", "经济" },
            { "经济重载", "经济重载" },
            { "progressloot", "进度掉落" },
            { "重载进度", "重载进度" },
            { "重置PigeonRPG数据", "重置数据" },
            { "世界进度", "世界进度" },
            { "我的进度", "我的进度" },
            { "解锁", "解锁" },
            { "rpg", "RPG" },
            { "升级", "升级" },
            { "下一级", "下一级" },
            { "重置职业", "重置职业" },
            { "rpg经验", "经验" },
            { "rpgstatus", "状态" },
            { "rpgquest", "任务" },
            { "rpgruntime", "运行时" },
            { "rpg补发", "补发" },
            { "pr", "礼包" },
            { "sr", "赞助礼包" },
            { "prreload", "礼包重载" },
            { "th", "称号" },
            { "veinminer", "连锁挖矿" },
            { "pgreload", "重载" },
            { "pgscan", "扫描" },
            { "pglivecheck", "自检" },
            { "pgmatrix", "矩阵" },
            { "商店", "商店" },
            { "商店重载", "商店重载" },
            { "技能", "技能" },
            { "技能重载", "技能重载" },
            { "礼包", "礼包" },
            { "装备效果", "装备效果" },
            { "装备效果重载", "装备重载" },
            { "怪物分层", "怪物分层" },
            { "怪物分层重载", "怪物重载" },
            { "接受tp", "接受TP" },
            { "自动拒绝tp", "拒绝TP" },
            { "自动接受tp", "接受TP" },
            { "拒绝tp", "拒绝TP" },
            { "tpahere", "拉人请求" },
            { "tpa", "传送请求" },
            { "back", "回死亡点" },
            { "permabuff", "永久" },
            { "buffcheck", "查增益" },
            { "gpermabuff", "给增益" },
            { "regionbuff", "区域" },
            { "globalbuff", "全局" },
            { "clearbuffs", "清增益" },
            { "scommand", "查命令" },
            { "sperm", "查权限" },
            { "sgcommand", "查组命令" },
            { "pluginlist", "插件权限" },
            { "invsee", "看背包" },
            { "house", "圈地" },
            { "query", "查货币" },
            { "es", "倍率" },
            { "bank", "银行" },
            { "level", "等级" },
            { "rank", "升级" },
            { "reset", "重置" },
            { "skill", "技能" },
            { "task", "任务" },
            { "plus", "强化" },
            { "clearallplayersplus", "清强化" },
            { "zhelp", "备份帮助" },
            { "zsave", "备份" },
            { "zvisa", "看存档" },
            { "zsaveauto", "自动备份" },
            { "zback", "回档" },
            { "zclone", "克隆" },
            { "zmodify", "改玩家" },
            { "zfre", "冻结" },
            { "zunfre", "解冻" },
            { "zresetdb", "清备份" },
            { "zresetex", "重置额外" },
            { "zreset", "重置角色" },
            { "zresetallplayers", "重置全服" },
            { "vi", "看背包" },
            { "vid", "看背包" },
            { "vs", "看状态" },
            { "zsort", "排行" },
            { "zban", "封禁" },
            { "zhide", "隐藏弹字" },
            { "zclear", "清理" },
            { "deal", "交易" },
            { "igen", "快速构建" },
            { "relive", "复活" },
            { "bossinfo", "进度查询" },
            { "zout", "导出存档" },
            { "分界线2", "常用插件" }
        };
    }

    /// <summary>
    /// 将配置文件写入硬盘
    /// </summary>
    internal void Write()
    {
        using FileStream fileStream = new (ConfigPath, FileMode.Create, FileAccess.Write, FileShare.Write);
        using StreamWriter streamWriter = new (fileStream);
        streamWriter.Write(JsonConvert.SerializeObject(this, JsonSettings));
    }

    /// <summary>
    /// 从硬盘读取配置文件
    /// </summary>
    internal static void Read()
    {
        Config result;
        if (!File.Exists(ConfigPath))
        {
            result = new Config();
            result.Write();
        }
        else
        {
            using FileStream fileStream = new (ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using StreamReader streamReader = new (fileStream);
            result = JsonConvert.DeserializeObject<Config>(streamReader.ReadToEnd(), JsonSettings)!;
        }

        Settings = result;
    }

    private static readonly JsonSerializerSettings JsonSettings = new () { Formatting = Formatting.Indented, ObjectCreationHandling = ObjectCreationHandling.Replace };
}
