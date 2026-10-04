using VBY.Basic.Config;

namespace CustomPlayer;

public class Config : MainConfig<Root>
{
	public Config(string configDirectory, string fileName = "CustomPlayer.json")
		: base(configDirectory, fileName)
	{
	}
}
