using System;
using System.Linq;
using TShockAPI;

namespace VBY.Basic.Command;

public class SubCmdNodeRun : SubCmdNode
{
	internal SubCmdD RunCmd;

	internal bool DireRun;

	internal int MinArgsCount;

	public string? ArgsHelpText;

	public string? HelpText;

	internal SubCmdNodeRun(SubCmdD runCmd, string cmdName, string description, string[] names, string? argsHelpText = null, string? helpText = null, int minArgsCount = 0)
		: base(cmdName, description, names)
	{
		NodeType = NodeType.Run;
		RunCmd = runCmd;
		if (argsHelpText == null && helpText == null)
		{
			DireRun = true;
		}
		else
		{
			ArgsHelpText = argsHelpText;
			HelpText = helpText;
		}
		MinArgsCount = minArgsCount;
	}

	public override void Run(CommandArgs args)
	{
		if (NoCanRun(args.Player))
		{
			return;
		}
		if (DireRun || args.Parameters.Count >= MinArgsCount)
		{
			RunCmd(new SubCmdArgs(CmdName, args, args.Parameters.Skip(CmdIndex).ToList()));
			return;
		}
		args.Player.SendInfoMessage($"参数不足,最少需要{MinArgsCount - CmdIndex}个参数");
		bool flag = !string.IsNullOrEmpty(ArgsHelpText);
		if (flag)
		{
			TSPlayer player = args.Player;
			_003C_003Ey__InlineArray2<string> buffer = default;
			buffer[0] = args.Message.Substring(0, args.Message.IndexOf(' '));
			buffer[1] = string.Join(' ', args.Parameters.GetRange(0, CmdIndex));
			player.SendInfoMessage("/" + string.Join(' ', (ReadOnlySpan<string?>)buffer) + " " + ArgsHelpText);
		}
		bool flag2 = !string.IsNullOrEmpty(HelpText);
		if (flag2)
		{
			args.Player.SendInfoMessage(HelpText);
		}
		if (!(flag | flag2))
		{
			args.Player.SendErrorMessage("此命令没有帮助文本!");
		}
	}

	internal override void OutCmdRun(CommandArgs args, int outCount)
	{
		if (DireRun || args.Parameters.Count >= MinArgsCount - outCount)
		{
			RunCmd(new SubCmdArgs(CmdName, args, args.Parameters.Skip(CmdIndex - outCount).ToList()));
			return;
		}
		args.Player.SendInfoMessage($"参数不足,最少需要{MinArgsCount - CmdIndex}个参数");
		bool flag = !string.IsNullOrEmpty(ArgsHelpText);
		if (flag)
		{
			TSPlayer player = args.Player;
			string text;
			if (CmdIndex != outCount)
			{
				_003C_003Ey__InlineArray2<string> buffer = default;
				buffer[0] = args.Message.Substring(0, args.Message.IndexOf(' '));
				buffer[1] = string.Join(' ', args.Parameters.GetRange(0, CmdIndex - outCount));
				text = "/" + string.Join(' ', (ReadOnlySpan<string?>)buffer) + " " + ArgsHelpText;
			}
			else
			{
				text = "/" + args.Message.Trim() + " " + ArgsHelpText;
			}
			player.SendInfoMessage(text);
		}
		bool flag2 = !string.IsNullOrEmpty(HelpText);
		if (flag2)
		{
			args.Player.SendInfoMessage(HelpText);
		}
		if (!(flag | flag2))
		{
			args.Player.SendErrorMessage("此命令没有帮助文本!");
		}
	}
}
