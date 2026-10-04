namespace AntiCheatingTool;

public class AntiItemFlood
{
	public int PacketSlot { get; set; } = 400;

	public int DistanceTiles { get; set; } = 3;

	public ViolationRule Rule { get; set; } = new ViolationRule
	{
		Threshold = 9,
		WindowSeconds = 5
	};

	public string KickTip { get; set; } = "疑似使用物品洪水攻击";
}
