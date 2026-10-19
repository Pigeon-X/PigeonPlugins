using TShockAPI;

namespace PeaceMode;

internal static class PeaceModeReloadCommand
{
    private static Command? _command;

    internal static void Register(PeaceModePlugin plugin)
    {
        _command = new Command("peace.reload", plugin.ReloadConfig, "和平", "peacemode")
        {
            AllowServer = true,
            DoLog = true
        };
        Commands.ChatCommands.Add(_command);
    }

    internal static void Unregister()
    {
        if (_command is null) return;
        Commands.ChatCommands.Remove(_command);
        _command = null;
    }
}
