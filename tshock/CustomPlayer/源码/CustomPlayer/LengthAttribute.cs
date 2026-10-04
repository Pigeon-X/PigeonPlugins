using System;

namespace CustomPlayer;

[AttributeUsage(AttributeTargets.Field)]
public class LengthAttribute : Attribute
{
	public int Length;

	public LengthAttribute(int length)
	{
		Length = length;
	}
}
