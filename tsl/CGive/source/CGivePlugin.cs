using System.Collections.Immutable;
using TShockAPI;
using TShockAPI.Hooks;
using UnifierTSL.PluginService;
using UnifierTSL.Plugins;

namespace CGive;

[PluginMetadata("CGive", "1.0.1.1", "Leader, 肝帝熙恩", "离线 give / 登录补发")]
public sealed class CGivePlugin : BasePlugin
{
    private Command? _command;

    public override Task InitializeAsync(
        IPluginConfigRegistrar configRegistrar,
        ImmutableArray<PluginInitInfo> priorInitializations,
        CancellationToken cancellationToken = default)
    {
        Data.Init();

        _command = new Command("cgive.admin", GiveCommand, "cgive")
        {
            AllowServer = false,
            HelpText = "离线 give / 登录补发。"
        };
        Commands.ChatCommands.Add(_command);
        PlayerHooks.PlayerPostLogin += OnPlayerPostLogin;

        TShock.Log.Info("[CGive] UnifierTSL 版已加载。");
        return Task.CompletedTask;
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        PlayerHooks.PlayerPostLogin -= OnPlayerPostLogin;
        if (_command is not null)
            Commands.ChatCommands.Remove(_command);
        return Task.CompletedTask;
    }

    private static void OnPlayerPostLogin(PlayerPostLoginEventArgs args)
    {
        var player = args.Player;
        if (player is null || !player.IsLoggedIn) return;

        foreach (var item in CGive.GetCGiveForPlayer(player.Name))
        {
            if (item.who == "-1")
            {
                var given = new Given { Name = player.Name, Id = item.id };
                if (!given.IsGiven() && item.ExecuteOnLogin(player))
                    given.Save();
            }
            else if (item.who.Equals(player.Name, StringComparison.OrdinalIgnoreCase)
                     && item.ExecuteOnLogin(player))
            {
                item.Del();
            }
        }
    }

    private void GiveCommand(CommandArgs args)
    {
        if (args.Parameters.Count == 0)
        {
            args.Player.SendInfoMessage("/cgive add [执行者] [被执行者] [命令]");
            args.Player.SendInfoMessage("  执行者: Server 或 玩家名");
            args.Player.SendInfoMessage("  被执行者: 玩家名 或 -1(所有玩家)");
            args.Player.SendInfoMessage("/cgive list - 列出所有离线命令");
            args.Player.SendInfoMessage("/cgive del [id] - 删除指定 id 的离线命令");
            args.Player.SendInfoMessage("/cgive reset - 重置所有数据");
            return;
        }

        switch (args.Parameters[0])
        {
            case "reset":
                Data.Command("DELETE FROM CGive");
                Data.Command("DELETE FROM Given");
                args.Player.SendSuccessMessage("成功删除所有数据。");
                break;

            case "del":
            {
                if (args.Parameters.Count < 2)
                {
                    args.Player.SendErrorMessage("用法：/cgive del [id]");
                    break;
                }

                if (!int.TryParse(args.Parameters[1], out var id))
                {
                    args.Player.SendErrorMessage("ID 输入错误。");
                    break;
                }

                new CGive { id = id }.Del();
                Data.Command("DELETE FROM Given WHERE id=@0", id);
                args.Player.SendSuccessMessage("已执行删除。");
                break;
            }

            case "list":
            {
                foreach (var item in CGive.GetCGive())
                {
                    args.Player.SendInfoMessage($"执行者:{item.Executer} 被执行者:{item.who} 命令:{item.cmd} id:{item.id}");
                }
                break;
            }

            case "add":
            {
                if (args.Parameters.Count < 4)
                {
                    args.Player.SendErrorMessage("用法：/cgive add [执行者] [被执行者] [命令]");
                    break;
                }

                var item = new CGive
                {
                    Executer = args.Parameters[1],
                    who = args.Parameters[2],
                    cmd = string.Join(' ', args.Parameters.Skip(3))
                };

                if (!item.Execute())
                {
                    args.Player.SendInfoMessage("命令已保存。");
                    item.Save();
                }
                else
                {
                    args.Player.SendInfoMessage("命令执行成功。");
                }
                break;
            }

            default:
                args.Player.SendErrorMessage($"未知子命令：{args.Parameters[0]}，输入 /cgive 查看帮助。");
                break;
        }
    }
}