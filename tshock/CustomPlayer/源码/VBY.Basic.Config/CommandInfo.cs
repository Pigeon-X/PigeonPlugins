using TShockAPI;

namespace VBY.Basic.Config;

public class CommandInfo
{
	public string Permissions;

	public string[] Names;

	public CommandInfo()
	{
	}

	public CommandInfo(string permissions, string[] names)
	{
		Permissions = permissions;
		Names = names;
	}

	public TShockAPI.Command GetCommand(CommandDelegate cmd)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected Obj, but got Unknown
		return new TShockAPI.Command(Permissions, cmd, Names);
	}
}
