using System.Collections.Immutable;
using TShockAPI;
using UnifierTSL;
using UnifierTSL.Module;
using UnifierTSL.Plugins;

[assembly: CoreModule]

namespace PeaceMode;

[PluginMetadata("PeaceMode", "1.0.1", "Pigeon-X", "和平模式：禁止所有 NPC、召唤和世界事件，并静默相关公告；配置只手动重载。")]
public sealed partial class PeaceModePlugin : BasePlugin
{
    private readonly List<Action> _unbind = [];
    private IPluginConfigHandle<PeaceModeConfig>? _config;
    private volatile bool _enabled;

    public bool Active => _enabled;

    public override async Task InitializeAsync(
        IPluginConfigRegistrar configRegistrar,
        ImmutableArray<PluginInitInfo> priorInitializations,
        CancellationToken cancellationToken = default)
    {
        _config = configRegistrar
            .CreateConfigRegistration<PeaceModeConfig>("PeaceMode.json", ConfigFormat.SystemTextJson)
            .WithDefault(() => new PeaceModeConfig())
            .OnDeserializationFailure(DeserializationFailureHandling.ReturnNewInstance, false)
            // 外部改文件不自动重读；只有 /和平 重载 才重新加载。
            .TriggerReloadOnExternalChange(false)
            .Complete();
        _enabled = (await _config.RequestAsync(cancellationToken: cancellationToken))?.Enabled ?? true;
        try
        {
            BindNpcHooks();
            BindEventHooks();
            BindNetworkHooks();
        }
        catch
        {
            Unbind();
            throw;
        }
        PeaceModeReloadCommand.Register(this);
        UnifierApi.Logger.Info($"[PeaceMode] 和平模式{(_enabled ? "已启用" : "已停用")}，配置：{_config.FilePath}；修改后执行 /和平 重载。");
    }

    internal void ReloadConfig(CommandArgs args)
    {
        try
        {
            if (_config is null) { args.Player?.SendInfoMessage("配置句柄尚未就绪。"); return; }
            var config = _config.Request(reloadFromIO: true);
            _enabled = config?.Enabled ?? true;
            args.Player?.SendInfoMessage($"PeaceMode 配置已重新加载：{(_enabled ? "启用" : "停用")}。");
        }
        catch (Exception ex)
        {
            UnifierApi.Logger.Warning("[PeaceMode] 重载失败：" + ex.Message);
            args.Player?.SendErrorMessage("重载失败，已保留旧配置：" + ex.Message);
        }
    }

    private void Bind(Action subscribe, Action unsubscribe)
    {
        subscribe();
        _unbind.Add(unsubscribe);
    }

    private void Unbind()
    {
        _enabled = false;
        for (var i = _unbind.Count - 1; i >= 0; i--) _unbind[i]();
        _unbind.Clear();
        _config = null;
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        PeaceModeReloadCommand.Unregister();
        Unbind();
        return Task.CompletedTask;
    }

    public override ValueTask DisposeAsync(bool isDisposing)
    {
        PeaceModeReloadCommand.Unregister();
        Unbind();
        return base.DisposeAsync(isDisposing);
    }
}
