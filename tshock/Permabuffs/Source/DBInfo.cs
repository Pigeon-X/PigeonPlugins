using System.Collections.Generic;

namespace Permabuffs_V2;

public class DBInfo
{
	public List<int> bufflist;

	public DBInfo(string activeBuffs)
	{
		bufflist = new List<int>();
		if (!(activeBuffs != ""))
		{
			return;
		}
		string[] array = activeBuffs.Split(',');
		for (int i = 0; i < array.Length; i++)
		{
			if (int.TryParse(array[i], out var result))
			{
				bufflist.Add(result);
			}
		}
	}
}
