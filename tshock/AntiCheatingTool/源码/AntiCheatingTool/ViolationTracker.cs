using System;
using System.Collections.Generic;

namespace AntiCheatingTool;

public sealed class ViolationTracker
{
	private readonly Dictionary<int, List<long>> _hits = new Dictionary<int, List<long>>();

	public int Add(int playerIndex, int windowSeconds)
	{
		if (playerIndex < 0)
		{
			return 0;
		}
		long now = DateTime.UtcNow.Ticks;
		long windowTicks = TimeSpan.FromSeconds((windowSeconds >= 0) ? windowSeconds : 0).Ticks;
		if (!_hits.TryGetValue(playerIndex, out List<long> value))
		{
			value = new List<long>();
			_hits[playerIndex] = value;
		}
		value.RemoveAll((long t) => now - t > windowTicks);
		value.Add(now);
		return value.Count;
	}

	public int CountInWindow(int playerIndex, int windowSeconds)
	{
		if (!_hits.TryGetValue(playerIndex, out List<long> value))
		{
			return 0;
		}
		long now = DateTime.UtcNow.Ticks;
		long windowTicks = TimeSpan.FromSeconds((windowSeconds >= 0) ? windowSeconds : 0).Ticks;
		value.RemoveAll((long t) => now - t > windowTicks);
		return value.Count;
	}

	public void Reset(int playerIndex)
	{
		_hits.Remove(playerIndex);
	}

	public void Clear()
	{
		_hits.Clear();
	}
}
