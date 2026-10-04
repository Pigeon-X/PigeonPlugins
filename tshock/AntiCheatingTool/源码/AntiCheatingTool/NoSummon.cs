namespace AntiCheatingTool;

public class NoSummon
{
	public int[] Projs { get; set; } = Config.DefaultProjs();

	public int[] SummonNpcs { get; set; } = new int[1] { 661 };

	public ViolationRule Rule { get; set; } = new ViolationRule();

	public string KickTip { get; set; } = "多次在保护区召唤或使用炸药";
}


