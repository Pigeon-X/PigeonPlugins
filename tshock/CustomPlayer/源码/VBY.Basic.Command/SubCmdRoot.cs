using TShockAPI;

namespace VBY.Basic.Command;

public class SubCmdRoot : SubCmdNodeList
{
	public SubCmdRoot(string cmdName)
		: base(cmdName, "", cmdName.ToLower())
	{
	}

	public override TShockAPI.Command GetCommand()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected Obj, but got Unknown
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected Obj, but got Unknown
		return new TShockAPI.Command(Permission, (CommandDelegate)Run, Names)
		{
			HelpText = Description
		};
	}

	public override TShockAPI.Command GetCommand(params string[] names)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected Obj, but got Unknown
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected Obj, but got Unknown
		return new TShockAPI.Command(Permission, (CommandDelegate)Run, names)
		{
			HelpText = Description
		};
	}

	public override TShockAPI.Command GetCommand(string permission, string[] names)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected Obj, but got Unknown
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected Obj, but got Unknown
		return new TShockAPI.Command(permission, (CommandDelegate)Run, names)
		{
			HelpText = Description
		};
	}
}
