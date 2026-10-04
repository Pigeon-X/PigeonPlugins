using System;

namespace AntiCheatingTool;

public sealed class ZoneTipService
{
	private const int MaxPlayers = 256;

	private readonly ZoneTipState[] _states = new ZoneTipState[256];

	private readonly bool[] _initialized = new bool[256];

	public ZoneTipService()
	{
		for (int i = 0; i < _states.Length; i++)
		{
			_states[i] = new ZoneTipState();
		}
	}

	public bool Evaluate(int playerIndex, bool inZone, Config cfg, out bool tipIn)
	{
		tipIn = false;
		if (playerIndex < 0 || playerIndex >= 256)
		{
			return false;
		}
		if (!_initialized[playerIndex])
		{
			_initialized[playerIndex] = true;
			_states[playerIndex].Tick(inZone, cfg.Protection.TipCooldownSeconds, DateTime.UtcNow.Ticks, out var _);
			return false;
		}
		return _states[playerIndex].Tick(inZone, cfg.Protection.TipCooldownSeconds, DateTime.UtcNow.Ticks, out tipIn);
	}

	public bool EvaluateAt(int playerIndex, bool inZone, int cooldownSeconds, long nowTicks, out bool tipIn)
	{
		tipIn = false;
		if (playerIndex < 0 || playerIndex >= 256)
		{
			return false;
		}
		if (!_initialized[playerIndex])
		{
			_initialized[playerIndex] = true;
			_states[playerIndex].Tick(inZone, cooldownSeconds, nowTicks, out var _);
			return false;
		}
		return _states[playerIndex].Tick(inZone, cooldownSeconds, nowTicks, out tipIn);
	}

	public bool IsInitialized(int playerIndex)
	{
		if (playerIndex >= 0 && playerIndex < 256)
		{
			return _initialized[playerIndex];
		}
		return false;
	}

	public void Reset(int playerIndex)
	{
		if (playerIndex >= 0 && playerIndex < 256)
		{
			_initialized[playerIndex] = false;
			_states[playerIndex].Reset();
		}
	}

	public void ResetAll()
	{
		for (int i = 0; i < 256; i++)
		{
			Reset(i);
		}
	}
}
