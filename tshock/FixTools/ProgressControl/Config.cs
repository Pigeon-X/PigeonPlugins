using Newtonsoft.Json;
using System.Collections;
using Terraria;
using TShockAPI;


namespace ProgressControl;

public class Config
{
    public static string configPath = Path.Combine(TShock.SavePath, "流光系统", "ProgressControl.json");
    private static string legacyConfigPath = Path.Combine(TShock.SavePath, "ProgressControl.json");
    private static string legacyBracketedConfigPath = Path.Combine(TShock.SavePath, "[流光系统]", "ProgressControl.json");


    #region 写入配置格式方法
    public static Config LoadConfigFile()
    {
        if (!Directory.Exists(TShock.SavePath))
        {
            Directory.CreateDirectory(TShock.SavePath);
        }

        var configDirectory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(configDirectory) && !Directory.Exists(configDirectory))
        {
            Directory.CreateDirectory(configDirectory);
        }

        if (!File.Exists(configPath))
        {
            var legacyConfigPaths = new[] { legacyConfigPath, legacyBracketedConfigPath };
            foreach (var oldConfigPath in legacyConfigPaths)
            {
                if (!File.Exists(oldConfigPath)) continue;
                File.Move(oldConfigPath, configPath);
                break;
            }
        }

        if (!File.Exists(configPath))
        {
            var NewConfig = new Config
            {
                StartServerDate = DateTime.Now,
                OpenAutoReset = false,
                HowLongTimeOfAotuResetServer = 0,
                ResetTSCharacter = false,
                DeleteWorldForReset = false,
                NpcKillCountForAutoReset = new Dictionary<int, ArrayList>(),
                CommandForBeforeResetting = new HashSet<string>(),
                DeleteSQLiteForBeforeResetting = new HashSet<string>(),
                DeleteFileForBeforeResetting = new HashSet<string>(),
                MapSizeForAfterReset = 0,
                MapDifficultyForAfterReset = 0,
                WorldSeedForAfterReset = "",
                WorldNameForAfterReset = "",
                ExpectedUsageWorldFileNameForAotuReset = new HashSet<string>(),
                WorldPath = "",
                LasetServerRestartDate = DateTime.Now,
                AutoRestartServer = false,
                HowLongTimeOfRestartServer = 0,
                CommandForBeforeRestart = new HashSet<string>(),
                OpenAutoControlProgressLock = true,
                ProgressLockTimeForStartServerDate = new Dictionary<string, double>()
                {
                    {"史莱姆王", 0},
                    {"克苏鲁之眼", 24},
                    {"世界吞噬者", 36},
                    {"克苏鲁之脑", 36},
                    {"蜂后", 48},
                    {"巨鹿", 54},
                    {"骷髅王", 72},
                    {"血肉墙", 96},
                    {"史莱姆皇后", 96},
                    {"双子魔眼", 102},
                    {"毁灭者", 108},
                    {"机械骷髅王", 114},
                    {"猪龙鱼公爵", 126},
                    {"世纪之花", 132},
                    {"光之女皇", 138},
                    {"石巨人", 144},
                    {"拜月教教徒", 156},
                    {"四柱", 162},
                    {"月亮领主", 170}
                },
                CustomNPCIDLockTimeForStartServerDate = new Dictionary<int, double>(),
                LasetAutoCommandDate = DateTime.Now,
                OpenAutoCommand = false,
                HowLongTimeOfAutoCommand = 0,
                AutoCommandList = new HashSet<string>(),
                AutoCommandOfBroadcast = false,
                CheckPerm = false,
                ServerLogWriterEnabledForAotuResetting = false,
                Command_PcoDelFile_DeletePath = new HashSet<string>(),
                Command_PcoCopy_CopyPath = new HashSet<string>(),
                Command_PcoCopy_PastePath = "",
                Command_PcoCopy_CoverFile = false
            };
            File.WriteAllText(configPath, JsonConvert.SerializeObject(NewConfig, Formatting.Indented));
        }
        Config config;
        try
        {
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath))!;
        }
        catch
        {
            TSPlayer.All.SendWarningMessage(GetString("ProgressControl.json 反序列化失败，可能有填写错误或配置文件需要更新，已对配置文件作了更新处理"));
            Console.WriteLine(GetString("ProgressControl.json 反序列化失败，可能有填写错误或配置文件需要更新，已对配置文件作了更新处理"));
            config = PControl.config;
            PControl.config.SaveConfigFile();
        }
        return config;
    }
    #endregion

    /// <summary>
    /// 保存
    /// </summary>
    public void SaveConfigFile()
    {
        File.WriteAllText(configPath, JsonConvert.SerializeObject(this, Formatting.Indented));
    }

    /// <summary>
    /// 将原版的tshock的config.json从内存中写入文件一次
    /// </summary>
    public static void SaveTConfig()
    {
        TShock.Config.Write(Path.Combine(TShock.SavePath, "config.json"));
    }

    public Config()
    {
        this.StartServerDate = DateTime.Now;
        this.OpenAutoReset = false;
        this.HowLongTimeOfAotuResetServer = 176;
        this.ResetTSCharacter = true;
        this.DeleteWorldForReset = true;
        this.NpcKillCountForAutoReset = new Dictionary<int, ArrayList>();
        this.CommandForBeforeResetting = new HashSet<string>();
        this.DeleteSQLiteForBeforeResetting = new HashSet<string>();
        this.DeleteFileForBeforeResetting = new HashSet<string>();
        this.MapSizeForAfterReset = 3;
        this.MapDifficultyForAfterReset = 2;
        this.WorldSeedForAfterReset = "";
        this.WorldNameForAfterReset = "SFE4";
        this.ExpectedUsageWorldFileNameForAotuReset = new HashSet<string>();
        this.WorldPath = "./world/";

        this.LasetServerRestartDate = DateTime.Now;
        this.AutoRestartServer = false;
        this.HowLongTimeOfRestartServer = 0;
        this.CommandForBeforeRestart = new HashSet<string>();

        this.OpenAutoControlProgressLock = false;
        this.ProgressLockTimeForStartServerDate = new Dictionary<string, double>();
        this.CustomNPCIDLockTimeForStartServerDate = new Dictionary<int, double> { };

        this.LasetAutoCommandDate = DateTime.Now;
        this.OpenAutoCommand = false;
        this.HowLongTimeOfAutoCommand = 0;
        this.AutoCommandList = new HashSet<string> { };
        this.AutoCommandOfBroadcast = true;
        this.CheckPerm = true;
        this.ServerLogWriterEnabledForAotuResetting = false;
        this.Command_PcoDelFile_DeletePath = new HashSet<string>();
        this.Command_PcoCopy_CopyPath = new HashSet<string>();
        this.Command_PcoCopy_PastePath = "tshock/Zhipm/";
        this.Command_PcoCopy_CoverFile = true;
    }

    //重置计划"
    [JsonProperty("开服日期", Order = -15)]
    public DateTime StartServerDate;
    [JsonProperty("是否启用自动重置世界", Order = -15)]
    public bool OpenAutoReset;
    [JsonProperty("多少小时后开始自动重置世界", Order = -15)]
    public double HowLongTimeOfAotuResetServer;

    [JsonProperty("重置是否重置玩家数据", Order = -14)]
    public bool ResetTSCharacter;
    [JsonProperty("重置前是否删除地图", Order = -14)]
    public bool DeleteWorldForReset;
    [JsonProperty("NPC死亡次数触发执行指令", Order = -14)]
    public Dictionary<int, ArrayList> NpcKillCountForAutoReset;
    [JsonProperty("重置前执行的指令", Order = -14)]
    public HashSet<string> CommandForBeforeResetting;
    [JsonProperty("重置前删除哪些数据库表", Order = -14)]
    public HashSet<string> DeleteSQLiteForBeforeResetting;
    [JsonProperty("重置前删除哪些文件或文件夹", Order = -14)]
    public HashSet<string> DeleteFileForBeforeResetting;

    [JsonProperty("重置后的地图大小_小1_中2_大3", Order = -13)]
    public int MapSizeForAfterReset;
    [JsonProperty("重置后的地图难度_普通0_专家1_大师2_旅途3", Order = -13)]
    public int MapDifficultyForAfterReset;
    [JsonProperty("重置后的地图种子", Order = -13)]
    public string WorldSeedForAfterReset;
    [JsonProperty("重置后的地图名称", Order = -13)]
    public string WorldNameForAfterReset;
    [JsonProperty("你提供用于重置的地图名称", Order = -13)]
    public HashSet<string> ExpectedUsageWorldFileNameForAotuReset;
    [JsonProperty("地图存放目录", Order = -13)]
    public string WorldPath;
    

    //重启计划
    [JsonProperty("上次重启服务器的日期", Order = -11)]
    public DateTime LasetServerRestartDate;
    [JsonProperty("是否启用自动重启服务器", Order = -11)]
    public bool AutoRestartServer;
    [JsonProperty("多少小时后开始自动重启服务器", Order = -11)]
    public double HowLongTimeOfRestartServer;
    [JsonProperty("重启前执行的指令", Order = -11)]
    public HashSet<string> CommandForBeforeRestart;

    //Boss进度控制计划
    [JsonProperty("是否自动控制NPC进度", Order = -10)]
    public bool OpenAutoControlProgressLock;
    [JsonProperty("Boss封禁时长距开服日期", Order = -10)]
    public Dictionary<string, double> ProgressLockTimeForStartServerDate;
    [JsonProperty("NPC封禁时长距开服日期_ID和单位小时", Order = -10)]
    public Dictionary<int, double> CustomNPCIDLockTimeForStartServerDate;

    //指令使用计划
    [JsonProperty("上次自动执行指令的日期", Order = -9)]
    public DateTime LasetAutoCommandDate;
    [JsonProperty("是否启用自动执行指令", Order = -9)]
    public bool OpenAutoCommand;
    [JsonProperty("多少小时后开始自动执行指令", Order = -9)]
    public double HowLongTimeOfAutoCommand;
    [JsonProperty("自动执行的指令_不需要加斜杠", Order = -9)]
    public HashSet<string> AutoCommandList;
    [JsonProperty("执行指令时是否发广播", Order = -9)]
    public bool AutoCommandOfBroadcast;
    [JsonProperty("越权检查", Order = -9)]
    public bool CheckPerm;
    [JsonProperty("是否关闭ServerLog写入功能(Windows千万别开)", Order = -9)]
    public bool ServerLogWriterEnabledForAotuResetting;
    [JsonProperty("指令功能_删除哪些文件或文件夹", Order = -8)]
    public HashSet<string> Command_PcoDelFile_DeletePath;
    [JsonProperty("指令功能_要复制的文件或文件夹", Order = -8)]
    public HashSet<string> Command_PcoCopy_CopyPath;
    [JsonProperty("指令功能_复制目标目录", Order = -8)]
    public string Command_PcoCopy_PastePath { get; set; }
    [JsonProperty("指令功能_文件是否允许覆盖", Order = -8)]
    public bool Command_PcoCopy_CoverFile { get; set; } = true;

    /// <summary>
    /// 地图的文件夹目录
    /// </summary>
    /// <returns></returns>
    public string path()
    {
        return string.IsNullOrWhiteSpace(this.WorldPath) ? Main.WorldPath : this.WorldPath;
    }


    /// <summary>
    /// 为防止地图的文件夹目录里对即将要创造的地图名称重名，进行后缀加数字
    /// </summary>
    /// <param name="name">要创造的地图的名字</param>
    /// <param name="willDelete">将要删掉的地图的名字，删掉发生在创造地图前</param>
    /// <returns></returns>
    public string AddNumberFile(string? name, string willDelete = "")
    {   //尝试给重复地图编号，其实原版有自动编号的，但是自动编号的地图名称和地图数据的内部名称不一致，你自己手动开服就分不清了
        var count = 1;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "World";
        }

        while (true)
        {
            if (File.Exists(this.path() + "/" + name + (count == 1 ? "" : count) + ".wld") && (name + (count == 1 ? "" : count)) != willDelete)
            {
                count++;
            }
            else
            {
                return (name + (count == 1 ? "" : count)) != willDelete ? name + (count == 1 ? "" : count) : willDelete;
            }
        }
    }
}
