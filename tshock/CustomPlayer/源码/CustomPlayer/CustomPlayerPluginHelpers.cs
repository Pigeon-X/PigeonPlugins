using System.Collections.Generic;
using System.Data;
using System.Linq;
using CustomPlayer.ModfiyGroup;
using TShockAPI;
using VBY.Basic;

namespace CustomPlayer;

public static class CustomPlayerPluginHelpers
{
	public static GroupManager Groups;

	public static CustomPlayer[] Players = new CustomPlayer[255];

	public static IDbConnection DB;

	public static List<TimeOutObject> TimeOutList = new List<TimeOutObject>();

	public static Dictionary<string, int> GroupGrade = new Dictionary<string, int>();

	public static void GroupLevelSet()
	{
		List<Group> list = new List<Group>(Groups.groups);
		while (list.Count > 0)
		{
			int num = 0;
			int count = list.Count;
			for (int i = 0; i < count; i++)
			{
				Group val = list[i - num];
				int value;
				if (val.Parent == null)
				{
					GroupGrade.Add(val.Name, 0);
					list.RemoveAt(i - num);
					num++;
				}
				else if (GroupGrade.TryGetValue(val.ParentName, out value))
				{
					GroupGrade.TryAdd(val.Name, value + 1);
					list.RemoveAt(i - num);
					num++;
				}
			}
			if (num != 0)
			{
				continue;
			}
			foreach (Group item in list)
			{
				GroupGrade.TryAdd(item.Name, 0);
			}
			VBY.Basic.Utils.WriteInfoLine($"组等级解析未完成,剩余 {list.Count} 个组可能是父子关系成环: {string.Join(",", list.Select((Group x) => x.Name))}");
			break;
		}
	}
}
