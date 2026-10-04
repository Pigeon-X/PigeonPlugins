using System;

namespace AntiCheatingTool;

public sealed class ZoneTipState
{
	private long _lastTipTicks;

	public bool Initialized { get; private set; }

	public bool LastInZone { get; private set; }

	public bool Tick(bool inZone, int cooldownSeconds, long nowTicks, out bool tipIn)
	{
		tipIn = false;
		if (!Initialized)
		{
			Initialized = true;
			LastInZone = inZone;
			return false;
		}
		if (inZone == LastInZone)
		{
			return false;
		}
		LastInZone = inZone;
		long ticks = TimeSpan.FromSeconds((cooldownSeconds >= 0) ? cooldownSeconds : 0).Ticks;
		if (nowTicks - _lastTipTicks < ticks)
		{
			return false;
		}
		_lastTipTicks = nowTicks;
		tipIn = inZone;
		return true;
	}

	public void Reset()
	{
		Initialized = false;
		LastInZone = false;
		_lastTipTicks = 0L;
	}
}
