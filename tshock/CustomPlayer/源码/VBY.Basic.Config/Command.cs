using TShockAPI;
using VBY.Basic.Command;

namespace VBY.Basic.Config;

public class Command
{
	public CommandInfo Use = new CommandInfo();

	public CommandInfo Admin = new CommandInfo();

	public TShockAPI.Command[] GetCommands(CommandDelegate use, CommandDelegate admin)
	{
		return new TShockAPI.Command[2]
		{
			Use.GetCommand(use),
			Admin.GetCommand(admin)
		};
	}

	public TShockAPI.Command[] GetCommands(SubCmdRoot use, SubCmdRoot admin)
	{
		return new TShockAPI.Command[2]
		{
			use.GetCommand(Use.Permissions, Use.Names),
			admin.GetCommand(Admin.Permissions, Admin.Names)
		};
	}
}
