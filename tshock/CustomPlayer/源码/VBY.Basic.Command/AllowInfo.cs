namespace VBY.Basic.Command;

public class AllowInfo
{
	public bool? NeedLoggedIn { get; set; }

	public bool? AllowServer { get; set; }

	public string? Permission { get; set; }

	public AllowInfo()
	{
	}

	public AllowInfo(bool? needLoggedIn, bool? allowServer, string? permission)
	{
		NeedLoggedIn = needLoggedIn;
		AllowServer = allowServer;
		Permission = permission;
	}
}
