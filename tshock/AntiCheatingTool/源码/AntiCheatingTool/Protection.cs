namespace AntiCheatingTool;

public class Protection
{
	public int SpawnXRadius { get; set; } = 2500;

	public bool SkyToSurface { get; set; } = true;

	public int UndergroundDepth { get; set; } = 1500;

	public int UndergroundXRadius { get; set; } = 2500;

	public bool ShowEnterTip { get; set; } = true;

	public bool ShowLeaveTip { get; set; } = true;

	public string EnterTipText { get; set; } = "你已进入出生点保护区";

	public string LeaveTipText { get; set; } = "你已离开出生点保护区";

	public int TipCooldownSeconds { get; set; } = 5;
}
