using System;
using GetText;

namespace CustomPlayer.ModfiyGroup;

[Serializable]
public class GroupExistsException : GroupManagerException
{
	public GroupExistsException(string name)
		: base(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Group " + name + " already exists")))
	{
	}

	public GroupExistsException(string message, Exception inner)
		: base(message, inner)
	{
	}

	public GroupExistsException()
	{
	}
}
