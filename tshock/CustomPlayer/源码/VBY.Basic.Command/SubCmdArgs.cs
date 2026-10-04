using System.Collections.Generic;
using TShockAPI;

namespace VBY.Basic.Command;

public readonly struct SubCmdArgs
{
	public readonly string CmdName;

	public readonly List<string> Parameters;

	public readonly CommandArgs commandArgs;

	public TSPlayer Player => commandArgs.Player;

	public SubCmdArgs(string cmdName, CommandArgs args, List<string> parameters)
	{
		CmdName = cmdName;
		commandArgs = args;
		Parameters = parameters;
	}
}
