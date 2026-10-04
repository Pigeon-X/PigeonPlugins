namespace AntiCheatingTool;

public class ViolationRule
{
	public bool Enabled { get; set; } = true;

	public int Threshold { get; set; } = 20;

	public int WindowSeconds { get; set; } = 10;
}
