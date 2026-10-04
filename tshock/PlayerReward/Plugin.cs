using System.Reflection;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace PlayerReward;

[ApiVersion(2, 1)]
public sealed class PlayerRewardPlugin : TerrariaPlugin
{
    private const string ConfigFileName = "PlayerReward.json";
    private static PlayerRewardPlugin? instance;
    private static readonly object ConfigLock = new();
    private RewardCatalog? catalog;
    private RewardStore? store;
    private string configPath = string.Empty;
    private bool storeReady;

    public override string Name => "PlayerReward";
    public override string Author => "LaoSparrow / 流光系统适配";
    public override Version Version => new(2, 0, 0);
    public override string Description => "玩家礼包、赞助礼包与 CustomPlayer 组对接";

    public PlayerRewardPlugin(Main game) : base(game)
    {
    }

    public override void Initialize()
    {
        instance = this;
        this.configPath = Path.Combine(TShock.SavePath, ConfigFileName);
        this.LoadConfig();
        this.TryEnsureStore();
        this.RegisterCommands();
        TShock.Log.ConsoleInfo("[PlayerReward] 已加载：/pr 玩家礼包，/sr 赞助礼包，支持 CustomPlayer 组。");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Commands.ChatCommands.RemoveAll(x =>
                x.CommandDelegate == PlayerCommand ||
                x.CommandDelegate == SponsorCommand ||
                x.CommandDelegate == ReloadCommand);
        }

        base.Dispose(disposing);
    }

    private void RegisterCommands()
    {
        Commands.ChatCommands.Add(new Command(
            new List<string> { "playerreward.user", "playerreward.admin" },
            PlayerCommand,
            "pr", "playerreward", "礼包")
        {
            AllowServer = true,
            HelpText = "玩家礼包：list、get [id]、admin list/reset、admin reload"
        });

        Commands.ChatCommands.Add(new Command(
            new List<string> { "playerreward.user", "playerreward.admin" },
            SponsorCommand,
            "sr", "sponsor", "赞助", "赞助礼包")
        {
            AllowServer = true,
            HelpText = "赞助礼包：list、get [id]、admin list/reset"
        });

        Commands.ChatCommands.Add(new Command(
            "playerreward.admin",
            ReloadCommand,
            "prreload", "奖励重载")
        {
            AllowServer = true,
            HelpText = "重载 PlayerReward.json"
        });
    }

    private static void PlayerCommand(CommandArgs args) => RunSafe(args, () => HandleCommand(args, RewardType.Player, false));

    private static void SponsorCommand(CommandArgs args) => RunSafe(args, () => HandleCommand(args, RewardType.Sponsor, true));

    private static void ReloadCommand(CommandArgs args) => RunSafe(args, () =>
    {
        if (!EnsureAdmin(args))
        {
            return;
        }

        var plugin = instance;
        if (plugin == null)
        {
            Send(args, "PlayerReward 插件实例不存在。");
            return;
        }

        if (plugin.LoadConfig())
        {
            Send(args, "PlayerReward.json 重载成功。");
        }
        else
        {
            Send(args, "PlayerReward.json 重载失败，请查看服务器日志。");
        }
    });

    private static void HandleCommand(CommandArgs args, RewardType type, bool sponsor)
    {
        var parameters = args.Parameters;
        if (parameters.Count == 0 || IsHelp(parameters[0]))
        {
            SendHelp(args, sponsor);
            return;
        }

        switch (parameters[0].ToLowerInvariant())
        {
            case "list":
                ListAvailable(args, type, sponsor);
                break;
            case "get":
                Claim(args, type, sponsor, parameters.Count > 1 ? parameters[1] : null);
                break;
            case "admin":
                Admin(args, type, sponsor, parameters);
                break;
            default:
                SendHelp(args, sponsor);
                break;
        }
    }

    private static void ListAvailable(CommandArgs args, RewardType type, bool sponsor)
    {
        if (!EnsureUser(args) || !EnsureData(args))
        {
            return;
        }

        var account = AccountName(args.Player);
        var groups = GroupResolver.ForPlayer(args.Player, account);
        var packs = GetPacks(type);
        var available = RewardCatalog.Filter(packs, groups, Main.hardMode, instance!.catalog!.Config.HardModeReplacesNormal);
        if (available.Count == 0)
        {
            Send(args, sponsor ? "当前无可获取的赞助礼包。" : "当前无可获取的玩家礼包。");
            return;
        }

        var claimed = instance.store!.GetClaimed(account, type);
        var lines = new List<string>();
        lines.Add(sponsor ? "当前可获得的赞助礼包：" : "当前可获得的玩家礼包：");
        for (var i = 0; i < available.Count; i++)
        {
            var pack = available[i];
            var state = claimed.Contains(pack.Name) ? " [已领取]" : string.Empty;
            lines.Add($"  {i}: {pack.Name}{state}");
        }

        Send(args, string.Join(Environment.NewLine, lines));
    }

    private static void Claim(CommandArgs args, RewardType type, bool sponsor, string? idText)
    {
        if (!EnsureUser(args) || !EnsureRealPlayer(args) || !EnsureData(args))
        {
            return;
        }

        var account = AccountName(args.Player);
        var groups = GroupResolver.ForPlayer(args.Player, account);
        var packs = GetPacks(type);
        var available = RewardCatalog.Filter(packs, groups, Main.hardMode, instance!.catalog!.Config.HardModeReplacesNormal);
        var claimed = instance.store!.GetClaimed(account, type);
        var unclaimed = available.Where(x => !claimed.Contains(x.Name)).ToList();
        if (unclaimed.Count == 0)
        {
            Send(args, sponsor ? "当前没有新的赞助礼包可领取。" : "当前没有新的玩家礼包可领取。");
            return;
        }

        var index = 0;
        if (!string.IsNullOrWhiteSpace(idText) &&
            (!int.TryParse(idText, out index) || index < 0 || index >= unclaimed.Count))
        {
            Send(args, $"ID超出范围，最大ID值为 {unclaimed.Count - 1}。");
            return;
        }

        var pack = unclaimed[index];
        if (!HasInventorySpace(args.Player, pack.Items.Count))
        {
            Send(args, $"背包空间不足，需要 {pack.Items.Count} 个空位。");
            return;
        }

        if (!instance.store.TryClaim(account, type, pack.Name))
        {
            Send(args, "该礼包已经领取过了。");
            return;
        }

        try
        {
            GivePack(args.Player, pack);
        }
        catch
        {
            instance.store.RemoveClaim(account, type, pack.Name);
            throw;
        }

        RunPackCommands(args.Player, pack.Commands);
        Send(args, $"成功获取 {pack.Name} 礼包。");
    }

    private static void Admin(CommandArgs args, RewardType type, bool sponsor, List<string> parameters)
    {
        if (!EnsureAdmin(args) || !EnsureData(args))
        {
            return;
        }

        if (parameters.Count < 2 || IsHelp(parameters[1]))
        {
            Send(args, sponsor
                ? "/sr admin list <玩家> | /sr admin reset player <玩家> | /sr admin reset all"
                : "/pr admin list <玩家> | /pr admin reset player <玩家> | /pr admin reset all | /pr admin reload");
            return;
        }

        switch (parameters[1].ToLowerInvariant())
        {
            case "reload":
                if (sponsor)
                {
                    Send(args, "请使用 /pr admin reload 或 /prreload。");
                    return;
                }

                if (instance!.LoadConfig())
                {
                    Send(args, "PlayerReward.json 重载成功。");
                }
                else
                {
                    Send(args, "PlayerReward.json 重载失败，请查看日志。");
                }
                break;
            case "list":
                if (parameters.Count < 3)
                {
                    Send(args, "缺少玩家名。");
                    return;
                }

                AdminList(args, type, sponsor, parameters[2]);
                break;
            case "reset":
                AdminReset(args, type, sponsor, parameters);
                break;
            default:
                Send(args, sponsor
                    ? "/sr admin list <玩家> | /sr admin reset player <玩家> | /sr admin reset all"
                    : "/pr admin list <玩家> | /pr admin reset player <玩家> | /pr admin reset all | /pr admin reload");
                break;
        }
    }

    private static void AdminList(CommandArgs args, RewardType type, bool sponsor, string playerName)
    {
        var groups = ResolveGroupsForAdmin(playerName);
        if (groups.Count == 0)
        {
            Send(args, $"未找到玩家或组数据：{playerName}");
            return;
        }

        var packs = GetPacks(type);
        var available = RewardCatalog.Filter(packs, groups, Main.hardMode, instance!.catalog!.Config.HardModeReplacesNormal);
        var claimed = instance.store!.GetClaimed(playerName, type);
        var lines = new List<string>
        {
            $"玩家 {playerName} 的{(sponsor ? "赞助" : "玩家")}礼包："
        };
        if (available.Count == 0)
        {
            lines.Add("  当前没有可领取礼包。");
        }
        else
        {
            for (var i = 0; i < available.Count; i++)
            {
                var state = claimed.Contains(available[i].Name) ? " [已领取]" : string.Empty;
                lines.Add($"  {i}: {available[i].Name}{state}");
            }
        }

        Send(args, string.Join(Environment.NewLine, lines));
    }

    private static void AdminReset(CommandArgs args, RewardType type, bool sponsor, List<string> parameters)
    {
        if (parameters.Count < 3)
        {
            Send(args, "用法：admin reset player <玩家> 或 admin reset all");
            return;
        }

        var mode = parameters[2].ToLowerInvariant();
        if (mode == "all")
        {
            var count = instance!.store!.ResetAll(type);
            Send(args, $"已重置 {count} 条{(sponsor ? "赞助" : "玩家")}礼包记录。");
            return;
        }

        if (mode != "player" || parameters.Count < 4)
        {
            Send(args, "用法：admin reset player <玩家> 或 admin reset all");
            return;
        }

        var playerName = parameters[3];
        if (TShock.UserAccounts.GetUserAccountByName(playerName) == null &&
            TSPlayer.FindByNameOrID(playerName).Count == 0)
        {
            Send(args, $"未找到玩家：{playerName}");
            return;
        }

        var removed = instance!.store!.ResetPlayer(playerName, type);
        Send(args, $"已重置玩家 {playerName} 的 {removed} 条{(sponsor ? "赞助" : "玩家")}礼包记录。");
    }

    private static HashSet<string> ResolveGroupsForAdmin(string playerName)
    {
        var online = TSPlayer.FindByNameOrID(playerName);
        if (online.Count == 1)
        {
            return GroupResolver.ForPlayer(online[0], playerName);
        }

        return GroupResolver.ForOfflineAccount(playerName);
    }

    private static IReadOnlyList<RuntimePack> GetPacks(RewardType type)
    {
        return type == RewardType.Player
            ? instance!.catalog!.PlayerPacks
            : instance!.catalog!.SponsorPacks;
    }

    private bool LoadConfig()
    {
        lock (ConfigLock)
        {
            try
            {
                var loaded = RewardCatalog.Load(this.configPath);
                this.catalog = loaded;
                return true;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError("[PlayerReward] 读取 PlayerReward.json 失败，继续使用上一次有效配置：" + ex.Message);
                return false;
            }
        }
    }

    private bool TryEnsureStore()
    {
        if (this.storeReady)
        {
            return true;
        }

        try
        {
            this.store ??= new RewardStore();
            this.store.EnsureSchema();
            this.store.TryMigrateLegacyTable();
            this.storeReady = true;
            return true;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError("[PlayerReward] 数据库初始化失败，礼包领取暂不可用：" + ex.Message);
            return false;
        }
    }

    private static bool EnsureData(CommandArgs args)
    {
        var plugin = instance;
        if (plugin == null || plugin.catalog == null)
        {
            Send(args, "PlayerReward 配置尚未加载。");
            return false;
        }

        if (!plugin.TryEnsureStore())
        {
            Send(args, "PlayerReward 数据库暂不可用。");
            return false;
        }

        return true;
    }

    private static bool EnsureUser(CommandArgs args)
    {
        if (args.Player == null || args.Player == TSPlayer.Server)
        {
            return true;
        }

        if (!args.Player.HasPermission("playerreward.user") && !args.Player.HasPermission("playerreward.admin"))
        {
            Send(args, "没有权限使用该指令。");
            return false;
        }

        return true;
    }

    private static bool EnsureAdmin(CommandArgs args)
    {
        if (args.Player == null || args.Player == TSPlayer.Server || args.Player.HasPermission("playerreward.admin"))
        {
            return true;
        }

        Send(args, "没有 playerreward.admin 权限。");
        return false;
    }

    private static bool EnsureRealPlayer(CommandArgs args)
    {
        if (args.Player != null && args.Player.RealPlayer)
        {
            return true;
        }

        Send(args, "该指令必须在游戏内由真实玩家使用。");
        return false;
    }

    private static bool HasInventorySpace(TSPlayer? player, int required)
    {
        if (player == null || required <= 0)
        {
            return true;
        }

        var free = player.TPlayer.inventory.Take(50).Count(x => x.type == 0);
        return free >= required;
    }

    private static void GivePack(TSPlayer player, RuntimePack pack)
    {
        var slot = 0;
        foreach (var spec in pack.Items)
        {
            while (slot < 50 && player.TPlayer.inventory[slot].type != 0)
            {
                slot++;
            }

            if (slot >= 50)
            {
                throw new InvalidOperationException("背包空间不足，礼包发放中止。");
            }

            var item = new Item();
            item.netDefaults(spec.Id);
            item.stack = spec.Stack;
            item.prefix = (byte)spec.Prefix;
            player.TPlayer.inventory[slot] = item;
            NetMessage.SendData(
                MessageID.SyncEquipment,
                player.Index,
                -1,
                null,
                player.Index,
                slot,
                player.TPlayer.inventory[slot].prefix);
            slot++;
        }
    }

    private static void RunPackCommands(TSPlayer player, IReadOnlyList<string> commands)
    {
        foreach (var raw in commands)
        {
            var command = raw
                .Replace("{name}", player.Name, StringComparison.OrdinalIgnoreCase)
                .Replace("{account}", player.Account?.Name ?? player.Name, StringComparison.OrdinalIgnoreCase)
                .Trim();
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            if (!command.StartsWith('/') && !command.StartsWith('.'))
            {
                command = "/" + command;
            }

            try
            {
                // RPG 经验 add 是管理员命令。礼包是可信的自动奖励，
                // 命中 rpg/rpg经验/rpgexp 时用服务器身份执行，避免玩家因缺少
                // pigeonrpg.runtime.admin 权限导致经验奖励静默失败。
                var executor = IsRpgCommand(command) ? TSPlayer.Server : player;
                if (!Commands.HandleCommand(executor, command))
                {
                    TShock.Log.ConsoleError($"[PlayerReward] 礼包命令执行失败：{command}");
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[PlayerReward] 礼包命令异常：{command}；{ex.Message}");
            }
        }
    }

    private static bool IsRpgCommand(string command)
    {
        var normalized = command.Trim().TrimStart('/', '.').TrimStart();
        return normalized.StartsWith("rpg", StringComparison.OrdinalIgnoreCase);
    }

    private static string AccountName(TSPlayer? player)
    {
        return player?.Account?.Name ?? player?.Name ?? string.Empty;
    }

    private static bool IsHelp(string text)
    {
        return text.Equals("help", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("帮助", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("?", StringComparison.OrdinalIgnoreCase);
    }

    private static void SendHelp(CommandArgs args, bool sponsor)
    {
        if (sponsor)
        {
            Send(args, "/sr list | /sr get [id] | /sr admin list <玩家> | /sr admin reset player <玩家> | /sr admin reset all");
        }
        else
        {
            Send(args, "/pr list | /pr get [id] | /pr admin list <玩家> | /pr admin reset player <玩家> | /pr admin reset all | /pr admin reload");
        }
    }

    private static void Send(CommandArgs args, string message)
    {
        if (args.Player != null)
        {
            args.Player.SendInfoMessage(message);
        }
        else
        {
            TShock.Log.ConsoleInfo(message);
        }
    }

    private static void RunSafe(CommandArgs args, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError("[PlayerReward] 指令执行失败：" + ex);
            Send(args, "指令执行失败，请查看服务器日志。");
        }
    }
}
