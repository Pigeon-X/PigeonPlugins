using System.Collections.Immutable;
using System.Timers;
using TShockAPI;
using TShockAPI.Hooks;
using UnifierTSL.PluginService;
using UnifierTSL.Plugins;

namespace TeleportRequest;

[PluginMetadata("TeleportRequest", "1.0.4", "MarioE, Dr.Toxic, 肝帝熙恩", "传送前需要被传送者接受或拒绝请求")]
public sealed class TeleportRequestPlugin : BasePlugin
{
    private readonly bool[] _autoDeny = new bool[256];
    private readonly bool[] _autoAccept = new bool[256];
    private readonly TPRequest[] _requests = new TPRequest[256];
    private System.Timers.Timer? _timer;
    private static Config _config = new();

    internal static string ConfigPath => Path.Combine(TShock.SavePath, "tpconfig.json");

    public override Task InitializeAsync(
        IPluginConfigRegistrar configRegistrar,
        ImmutableArray<PluginInitInfo> priorInitializations,
        CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < _requests.Length; i++)
            _requests[i] = new TPRequest();

        SetupConfig();
        RegisterCommands();

        PlayerHooks.PlayerLogout += OnPlayerLogout;
        GeneralHooks.ReloadEvent += OnReload;

        _timer = new System.Timers.Timer(Math.Max(1, _config.IntervalInSeconds) * 1000)
        {
            AutoReset = true,
            Enabled = true
        };
        _timer.Elapsed += OnElapsed;

        TShock.Log.Info("[TeleportRequest] UnifierTSL 版已加载。");
        return Task.CompletedTask;
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        PlayerHooks.PlayerLogout -= OnPlayerLogout;
        GeneralHooks.ReloadEvent -= OnReload;

        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Elapsed -= OnElapsed;
            _timer.Dispose();
            _timer = null;
        }

        return Task.CompletedTask;
    }

    private void RegisterCommands()
    {
        Commands.ChatCommands.Add(new Command("tprequest.gettpr", TPAccept, "接受tp", "atp")
        {
            AllowServer = false,
            HelpText = "接受传送请求。"
        });
        Commands.ChatCommands.Add(new Command("tprequest.tpauto", TPAutoDeny, "自动拒绝tp", "autodeny")
        {
            AllowServer = false,
            HelpText = "自动拒绝所有人的传送请求。"
        });
        Commands.ChatCommands.Add(new Command("tprequest.tpauto", TPAutoAccept, "自动接受tp", "autoaccept")
        {
            AllowServer = false,
            HelpText = "自动接受所有人的传送请求。"
        });
        Commands.ChatCommands.Add(new Command("tprequest.gettpr", TPDeny, "拒绝tp", "dtp")
        {
            AllowServer = false,
            HelpText = "拒绝传送请求。"
        });
        Commands.ChatCommands.Add(new Command("tprequest.tpat", TPAHere, "tpahere")
        {
            AllowServer = false,
            HelpText = "发出把指定玩家传送到你当前位置的请求。"
        });
        Commands.ChatCommands.Add(new Command("tprequest.tpat", TPA, "tpa")
        {
            AllowServer = false,
            HelpText = "发出传送到指定玩家当前位置的请求。"
        });
    }

    private void OnElapsed(object? sender, ElapsedEventArgs e)
    {
        for (var i = 0; i < _requests.Length; i++)
        {
            var request = _requests[i];
            if (request.timeout <= 0) continue;

            var target = GetPlayer(request.dst);
            var requester = GetPlayer(i);
            if (target is null || requester is null)
            {
                request.timeout = 0;
                continue;
            }

            request.timeout--;
            if (request.timeout == 0)
            {
                requester.SendErrorMessage("传送请求已超时。");
                target.SendInfoMessage($"玩家[{requester.Name}]的传送请求已超时。");
                continue;
            }

            var format = request.dir
                ? "你被请求传送到玩家[{0}]的当前位置。({1}接受tp / {1}atp，或 {1}拒绝tp / {1}dtp)"
                : "玩家[{0}]要求传送到你当前位置。({1}接受tp / {1}atp，或 {1}拒绝tp / {1}dtp)";
            target.SendInfoMessage(string.Format(format, requester.Name, Commands.Specifier));
        }
    }

    private void OnPlayerLogout(PlayerLogoutEventArgs e)
    {
        var index = e.Player?.Index ?? -1;
        if ((uint)index >= 256) return;
        _autoDeny[index] = false;
        _autoAccept[index] = false;
        _requests[index].timeout = 0;
    }

    private void TPA(CommandArgs e)
    {
        if (!TryFindTarget(e, out var target)) return;
        if (target.Equals(e.Player))
        {
            e.Player.SendErrorMessage("禁止向自己发送传送请求！");
            return;
        }

        if (!CanTeleportTo(e.Player, target))
        {
            e.Player.SendErrorMessage($"你无法传送到玩家[{target.Name}]。");
            return;
        }

        if ((target.TPAllow && _autoAccept[target.Index]) || HasOverride(e.Player))
        {
            TeleportAndNotify(e.Player, target);
            return;
        }

        if (IsTargetBusy(target.Index))
        {
            e.Player.SendErrorMessage($"玩家[{target.Name}]已被其他玩家发出传送请求。");
            return;
        }

        _requests[e.Player.Index].dir = false;
        _requests[e.Player.Index].dst = (byte)target.Index;
        _requests[e.Player.Index].timeout = _config.TimeoutCount + 1;
        e.Player.SendSuccessMessage($"已成功向玩家[{target.Name}]发出传送请求。");
    }

    private void TPAHere(CommandArgs e)
    {
        if (!TryFindTarget(e, out var target)) return;
        if (target.Equals(e.Player))
        {
            e.Player.SendErrorMessage("禁止向自己发送传送请求！");
            return;
        }

        if (!CanTeleportTo(e.Player, target))
        {
            e.Player.SendErrorMessage($"你无法传送到玩家[{target.Name}]。");
            return;
        }

        if ((target.TPAllow && _autoAccept[target.Index]) || HasOverride(e.Player))
        {
            TeleportAndNotify(target, e.Player);
            return;
        }

        if (IsTargetBusy(target.Index))
        {
            e.Player.SendErrorMessage($"玩家[{target.Name}]已被其他玩家发出传送请求。");
            return;
        }

        _requests[e.Player.Index].dir = true;
        _requests[e.Player.Index].dst = (byte)target.Index;
        _requests[e.Player.Index].timeout = _config.TimeoutCount + 1;
        e.Player.SendSuccessMessage($"已成功向玩家[{target.Name}]发出传送请求。");
    }

    private void TPAccept(CommandArgs e)
    {
        var requestIndex = FindRequestFor(e.Player.Index);
        if (requestIndex < 0)
        {
            e.Player.SendErrorMessage("你暂时没有收到其他玩家的传送请求。");
            return;
        }

        var request = _requests[requestIndex];
        var requester = GetPlayer(requestIndex);
        if (requester is null)
        {
            request.timeout = 0;
            e.Player.SendErrorMessage("请求方已经离线。");
            return;
        }

        var destination = request.dir ? requester : e.Player;
        var source = request.dir ? e.Player : requester;
        TeleportAndNotify(source, destination);
        request.timeout = 0;
    }

    private void TPDeny(CommandArgs e)
    {
        var requestIndex = FindRequestFor(e.Player.Index);
        if (requestIndex < 0)
        {
            e.Player.SendErrorMessage("你暂时没有收到其他玩家的传送请求。");
            return;
        }

        var requester = GetPlayer(requestIndex);
        _requests[requestIndex].timeout = 0;
        e.Player.SendSuccessMessage($"已拒绝玩家[{requester?.Name ?? "未知"}]的传送请求。");
        requester?.SendErrorMessage($"玩家[{e.Player.Name}]拒绝你的传送请求。");
    }

    private void TPAutoDeny(CommandArgs e)
    {
        if (_autoAccept[e.Player.Index])
        {
            e.Player.SendErrorMessage("请先解除自动接受传送。");
            return;
        }

        _autoDeny[e.Player.Index] = !_autoDeny[e.Player.Index];
        e.Player.SendInfoMessage($"{(_autoDeny[e.Player.Index] ? "启用" : "解除")}自动拒绝传送请求。");
    }

    private void TPAutoAccept(CommandArgs e)
    {
        if (_autoDeny[e.Player.Index])
        {
            e.Player.SendErrorMessage("请先解除自动拒绝传送。");
            return;
        }

        _autoAccept[e.Player.Index] = !_autoAccept[e.Player.Index];
        e.Player.SendInfoMessage($"{(_autoAccept[e.Player.Index] ? "启用" : "解除")}自动接受传送请求。");
    }

    private bool TryFindTarget(CommandArgs e, out TSPlayer target)
    {
        target = null!;
        if (e.Parameters.Count == 0)
        {
            e.Player.SendErrorMessage($"格式错误，正确格式为：{Commands.Specifier}tpa <玩家>");
            return false;
        }

        var matches = TSPlayer.FindByNameOrID(string.Join(" ", e.Parameters));
        if (matches.Count == 0)
        {
            e.Player.SendErrorMessage("找不到这位玩家。");
            return false;
        }
        if (matches.Count > 1)
        {
            e.Player.SendErrorMessage("匹配到多于一位玩家。");
            return false;
        }

        target = matches[0];
        return true;
    }

    private bool CanTeleportTo(TSPlayer source, TSPlayer target)
    {
        return (target.TPAllow && !_autoDeny[target.Index]) || HasOverride(source);
    }

    private static bool HasOverride(TSPlayer player)
    {
        return player.Group?.HasPermission(Permissions.tpoverride) == true;
    }

    private bool IsTargetBusy(int targetIndex)
    {
        return _requests.Any(request => request.timeout > 0 && request.dst == targetIndex);
    }

    private int FindRequestFor(int playerIndex)
    {
        for (var i = 0; i < _requests.Length; i++)
            if (_requests[i].timeout > 0 && _requests[i].dst == playerIndex)
                return i;
        return -1;
    }

    private static TSPlayer? GetPlayer(int index)
    {
        if (index < 0 || index >= TShock.Players.Length) return null;
        return TShock.Players[index];
    }

    private static void TeleportAndNotify(TSPlayer source, TSPlayer destination)
    {
        if (!source.Teleport(destination.X, destination.Y, 1)) return;
        source.SendSuccessMessage($"已经传送到玩家[{destination.Name}]的当前位置。");
        destination.SendSuccessMessage($"玩家[{source.Name}]已传送到你的当前位置。");
    }

    private void SetupConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
                _config = Config.Read(ConfigPath);
            else
                _config = new Config();

            _config.Write(ConfigPath);
        }
        catch (Exception ex)
        {
            TShock.Log.Error("[TeleportRequest] 配置出现异常：" + ex);
        }
    }

    private void OnReload(ReloadEventArgs args)
    {
        SetupConfig();
        args.Player?.SendSuccessMessage("[TeleportRequest] 配置已重新加载。");
    }
}