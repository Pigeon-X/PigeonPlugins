using System.Collections.Generic;
using Newtonsoft.Json;

namespace AntiCheatingTool;

public class BombHandling
{
	public bool Enabled { get; set; } = true;

	public string Mode { get; set; } = "RevertItem";

	[JsonProperty(/*Could not decode attribute arguments.*/)]
	public Dictionary<string, int> ProjToItem { get; set; } = Config.DefaultBombMap();
}
