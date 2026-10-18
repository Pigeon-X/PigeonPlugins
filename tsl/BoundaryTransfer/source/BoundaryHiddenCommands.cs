using System.Text;
using TShockAPI;
using UnifierTSL;
using UnifierTSL.Events.Core;
using UnifierTSL.Events.Handlers;

namespace BoundaryTransfer;

public sealed partial class BoundaryTransferPlugin
{
    private void OnHiddenCommandMessage(ref ReadonlyEventArgs<MessageEvent> args)
    {
        if (args.Handled || !_running || !BoundaryCommandInput.TryMatch(args.Content.Text,
                args.Content.Sender.IsServer, Commands.Specifier, Commands.SilentSpecifier,
                out var commandText, out var argumentText)) return;

        // 必须在TShock的审计/广播路径之前消费，只回复发起者。
        args.Handled = true;
        args.StopPropagation = true;
        var sender = args.Content.Sender;
        var executor = new CommandExecutor(sender.SourceServer, sender.UserId);
        int maxLength = 2000;
        if (sender.IsClient)
        {
            int slot = sender.UserId;
            var player = slot < TShock.Players.Length ? TShock.Players[slot] : null;
            // 不能因为提前截获聊天就绕过TShock握手、连接与世界路由检查。
            if (player is null || !player.FinishedHandshake || !player.Active
                || player.GetCurrentServer() != sender.SourceServer
                || slot >= UnifiedServerCoordinator.globalClients.Length
                || UnifiedServerCoordinator.globalClients[slot] is not { PendingTermination: false } client
                || !client.IsConnected()) return;
            maxLength = Math.Clamp(TShock.Config.GetServerSettings(sender.SourceServer.Name).MaximumChatMessageLength, 250, 2000);
        }
        var command = _command;
        if (_stopping || command is null)
        {
            executor.SendErrorMessage("边界插件正在卸载，请稍后再试。");
            return;
        }
        if (commandText.Length > maxLength)
        {
            executor.SendErrorMessage("指令过长，未执行。");
            return;
        }
        if (!BoundaryCommandInput.TryParseArguments(argumentText, out var parameters))
        {
            executor.SendErrorMessage("参数中的双引号未闭合，未执行。");
            return;
        }
        // Command.Run仍做boundarytransfer.admin权限校验，但不会调用SendLogs。
        if (!command.Run(new CommandArgs(commandText, true, executor, parameters)))
            executor.SendErrorMessage("无效指令或无权使用。");
    }
}

internal static class BoundaryCommandInput
{
    internal static bool TryMatch(string text, bool allowBare, string specifier, string silentSpecifier,
        out string commandText, out string argumentText)
    {
        commandText = argumentText = string.Empty;
        text = text.Trim();
        int prefixLength = 0;
        if (!string.IsNullOrEmpty(specifier) && text.StartsWith(specifier, StringComparison.Ordinal))
            prefixLength = specifier.Length;
        if (!string.IsNullOrEmpty(silentSpecifier) && text.StartsWith(silentSpecifier, StringComparison.Ordinal))
            prefixLength = Math.Max(prefixLength, silentSpecifier.Length);
        if (prefixLength == 0 && !allowBare) return false;
        var body = text.AsSpan(prefixLength);
        int end = 0;
        while (end < body.Length && !char.IsWhiteSpace(body[end])) end++;
        var alias = body[..end];
        if (!alias.Equals("边界", StringComparison.OrdinalIgnoreCase)
            && !alias.Equals("boundarytransfer", StringComparison.OrdinalIgnoreCase)
            && !alias.Equals("borders", StringComparison.OrdinalIgnoreCase)) return false;
        commandText = body.ToString();
        argumentText = body[end..].ToString();
        return true;
    }

    // 与TShock相同的引号/转义用法；未闭合引号拒绝执行，避免误保存世界排列。
    internal static bool TryParseArguments(string text, out List<string> parameters)
    {
        parameters = [];
        var value = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length)
            {
                char next = text[++i];
                if (next is not ('"' or ' ' or '\\')) value.Append('\\');
                value.Append(next);
            }
            else if (c == '"')
            {
                quoted = !quoted;
                if (!quoted || value.Length > 0)
                {
                    parameters.Add(value.ToString());
                    value.Clear();
                }
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (value.Length > 0) { parameters.Add(value.ToString()); value.Clear(); }
            }
            else value.Append(c);
        }
        if (quoted) { parameters.Clear(); return false; }
        if (value.Length > 0) parameters.Add(value.ToString());
        return true;
    }
}
