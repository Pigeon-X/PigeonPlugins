using System;
using GetText;

namespace CustomPlayer.ModfiyGroup;

[Serializable]
public class GroupNotExistException : GroupManagerException
{
	public GroupNotExistException(string name)
		: base(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Group " + name + " does not exist")))
	{
	}

	public GroupNotExistException(string message, Exception inner)
		: base(message, inner)
	{
	}

	public GroupNotExistException()
	{
	}
}
