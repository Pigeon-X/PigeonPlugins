using System;

namespace CustomPlayer.ModfiyGroup;

[Serializable]
public class GroupManagerException : Exception
{
	public GroupManagerException(string message)
		: base(message)
	{
	}

	public GroupManagerException(string message, Exception inner)
		: base(message, inner)
	{
	}

	public GroupManagerException()
	{
	}
}
