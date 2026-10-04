using TShockAPI;

namespace VBY.Basic.Command;

public abstract class SubCmdNode
{
	public bool AllowServer = true;

	public bool DescCmd;

	public string CmdName;

	public string FullCmdName;

	public bool NeedLoggedIn;

	public string Permission = string.Empty;

	public bool Enabled = true;

	public string[] Names;

	public string Description;

	public int CmdIndex { get; internal set; }

	public NodeType NodeType { get; protected set; }

	public SubCmdNodeList? Parent { get; internal set; }

	internal SubCmdNode(string cmdName, string description, params string[] names)
	{
		CmdName = cmdName;
		FullCmdName = cmdName;
		Names = names;
		Description = description;
	}

	internal virtual bool NoCanRun(TSPlayer player)
	{
		if (!AllowCheck(player, NeedLoggedIn && !player.IsLoggedIn, "[" + FullCmdName + "]请登陆使用此命令") && !AllowCheck(player, !AllowServer && !player.RealPlayer, "[" + FullCmdName + "]服务器不允许执行此命令"))
		{
			return AllowCheck(player, !string.IsNullOrEmpty(Permission) && !player.HasPermission(Permission), "[" + FullCmdName + "]权限不足");
		}
		return true;
	}

	internal static bool AllowCheck(TSPlayer player, bool noAllow, string error)
	{
		if (noAllow)
		{
			player.SendErrorMessage(error);
			return true;
		}
		return false;
	}

	public abstract void Run(CommandArgs args);

	internal void OutCmdRun(CommandArgs args)
	{
		OutCmdRun(args, CmdIndex);
	}

	internal abstract void OutCmdRun(CommandArgs args, int outCount);

	public void SetAllowInfo(AllowInfo info)
	{
		if (info.Permission != null)
		{
			Permission = info.Permission;
		}
		if (info.AllowServer.HasValue)
		{
			AllowServer = info.AllowServer.Value;
		}
		if (info.NeedLoggedIn.HasValue)
		{
			NeedLoggedIn = info.NeedLoggedIn.Value;
		}
	}

	public virtual TShockAPI.Command GetCommand()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected Obj, but got Unknown
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected Obj, but got Unknown
		return new TShockAPI.Command(Permission, (CommandDelegate)OutCmdRun, Names);
	}

	public virtual TShockAPI.Command GetCommand(params string[] names)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected Obj, but got Unknown
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected Obj, but got Unknown
		return new TShockAPI.Command(Permission, (CommandDelegate)OutCmdRun, names);
	}

	public virtual TShockAPI.Command GetCommand(string permission, string[] names)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected Obj, but got Unknown
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected Obj, but got Unknown
		return new TShockAPI.Command(permission, (CommandDelegate)OutCmdRun, names);
	}
}
