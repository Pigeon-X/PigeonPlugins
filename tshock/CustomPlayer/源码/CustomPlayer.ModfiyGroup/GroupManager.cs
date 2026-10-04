using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using GetText;
using TShockAPI;
using TShockAPI.DB;

namespace CustomPlayer.ModfiyGroup;

public class GroupManager : IEnumerable<Group>, IEnumerable
{
	private readonly IDbConnection database;

	public readonly List<Group> groups = new List<Group>();

	public GroupManager(IDbConnection db)
	{
		database = db;
		LoadPermisions();
	}

	public bool GroupExists(string group)
	{
		if (!(group == "superadmin"))
		{
			return groups.Any((Group g) => g.Name.Equals(group));
		}
		return true;
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public IEnumerator<Group> GetEnumerator()
	{
		return groups.GetEnumerator();
	}

	public Group GetGroupByName(string name)
	{
		IEnumerable<Group> source = groups.Where((Group g) => g.Name == name);
		if (1 != source.Count())
		{
			return null;
		}
		return source.ElementAt(0);
	}

	public void AddGroup(string name, string parentname, string permissions, string chatcolor)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected Obj, but got Unknown
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Invalid comparison between Unknown and I4
		if (GroupExists(name))
		{
			throw new GroupExistsException(name);
		}
		Group val = new Group(name, (Group)null, chatcolor, (string)null)
		{
			Permissions = permissions
		};
		if (!string.IsNullOrWhiteSpace(parentname))
		{
			Group val2 = groups.FirstOrDefault((Group gp) => gp.Name == parentname);
			if (val2 == null || name == parentname)
			{
				string text = Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Invalid parent group " + parentname + " for group " + val.Name));
				TShock.Log.ConsoleError(text);
				throw new GroupManagerException(text);
			}
			val.Parent = val2;
		}
		string text2 = (((int)DbExt.GetSqlType(database) == 1) ? "INSERT OR IGNORE INTO GroupList (GroupName, Parent, Commands, ChatColor) VALUES (@0, @1, @2, @3);" : "INSERT IGNORE INTO GroupList SET GroupName=@0, Parent=@1, Commands=@2, ChatColor=@3");
		if (DbExt.Query(database, text2, new object[4] { name, parentname, permissions, chatcolor }) == 1)
		{
			groups.Add(val);
			return;
		}
		throw new GroupManagerException(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Failed to add group " + name + ".")));
	}

	public void UpdateGroup(string name, string parentname, string permissions, string chatcolor, string suffix, string prefix)
	{
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Expected Obj, but got Unknown
		Group groupByName = GetGroupByName(name);
		if (groupByName == null)
		{
			throw new GroupNotExistException(name);
		}
		Group val = null;
		if (!string.IsNullOrWhiteSpace(parentname))
		{
			val = GetGroupByName(parentname);
			if (val == null || val == groupByName)
			{
				throw new GroupManagerException(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make($"Invalid parent group {parentname} for group {name}.")));
			}
			List<Group> list = new List<Group> { groupByName, val };
			for (Group parent = val.Parent; parent != null; parent = parent.Parent)
			{
				if (list.Contains(parent))
				{
					throw new GroupManagerException(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make($"Parenting group {groupByName} to {parentname} would cause loops in the parent chain.")));
				}
				list.Add(parent);
			}
		}
		Group val2 = new Group(name, val, chatcolor, permissions)
		{
			Prefix = prefix,
			Suffix = suffix
		};
		string text = "UPDATE GroupList SET Parent=@0, Commands=@1, ChatColor=@2, Suffix=@3, Prefix=@4 WHERE GroupName=@5";
		if (DbExt.Query(database, text, new object[6] { parentname, val2.Permissions, val2.ChatColor, suffix, prefix, name }) != 1)
		{
			throw new GroupManagerException(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Failed to update group \"" + name + "\".")));
		}
		groupByName.ChatColor = chatcolor;
		groupByName.Permissions = permissions;
		groupByName.Parent = val;
		groupByName.Prefix = prefix;
		groupByName.Suffix = suffix;
	}

	public string RenameGroup(string name, string newName)
	{
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Expected Obj, but got Unknown
		if (!GroupExists(name))
		{
			throw new GroupNotExistException(name);
		}
		if (GroupExists(newName))
		{
			throw new GroupExistsException(newName);
		}
		using (IDbConnection dbConnection = DbExt.CloneEx(database))
		{
			dbConnection.Open();
			using IDbTransaction dbTransaction = dbConnection.BeginTransaction();
			try
			{
				using (IDbCommand dbCommand = dbConnection.CreateCommand())
				{
					dbCommand.CommandText = "UPDATE GroupList SET GroupName = @0 WHERE GroupName = @1";
					DbExt.AddParameter(dbCommand, "@0", (object)newName);
					DbExt.AddParameter(dbCommand, "@1", (object)name);
					dbCommand.ExecuteNonQuery();
				}
				Group oldGroup = GetGroupByName(name);
				Group val = new Group(newName, oldGroup.Parent, oldGroup.ChatColor, oldGroup.Permissions)
				{
					Prefix = oldGroup.Prefix,
					Suffix = oldGroup.Suffix
				};
				groups.Remove(oldGroup);
				groups.Add(val);
				using (IDbCommand dbCommand2 = dbConnection.CreateCommand())
				{
					dbCommand2.CommandText = "UPDATE GroupList SET Parent = @0 WHERE Parent = @1";
					DbExt.AddParameter(dbCommand2, "@0", (object)newName);
					DbExt.AddParameter(dbCommand2, "@1", (object)name);
					dbCommand2.ExecuteNonQuery();
				}
				foreach (Group item in groups.Where((Group g) => g.Parent != null && g.Parent == oldGroup))
				{
					item.Parent = val;
				}
				using (IDbCommand dbCommand3 = dbConnection.CreateCommand())
				{
					dbCommand3.CommandText = "UPDATE Users SET Usergroup = @0 WHERE Usergroup = @1";
					DbExt.AddParameter(dbCommand3, "@0", (object)newName);
					DbExt.AddParameter(dbCommand3, "@1", (object)name);
					dbCommand3.ExecuteNonQuery();
				}
				foreach (CustomPlayer item2 in CustomPlayerPluginHelpers.Players.Where((CustomPlayer p) => p?.Group == oldGroup))
				{
					item2.Group = val;
				}
				dbTransaction.Commit();
				return Utils.GetString(VBY.Basic.Extension.FsaHelper.Make($"Group {name} has been renamed to {newName}."));
			}
			catch (Exception ex)
			{
				TShock.Log.Error(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("An exception has occurred during database transaction: " + ex.Message)));
				try
				{
					dbTransaction.Rollback();
				}
				catch (Exception ex2)
				{
					TShock.Log.Error(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("An exception has occurred during database rollback: " + ex2.Message)));
				}
			}
		}
		throw new GroupManagerException(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Failed to rename group " + name + ".")));
	}

	public string DeleteGroup(string name, bool exceptions = false)
	{
		if (!GroupExists(name))
		{
			if (!exceptions)
			{
				return Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Group " + name + " doesn't exist."));
			}
			throw new GroupNotExistException(name);
		}
		if (DbExt.Query(database, "DELETE FROM GroupList WHERE GroupName=@0", new object[1] { name }) == 1)
		{
			groups.Remove(TShock.Groups.GetGroupByName(name));
			return Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Group " + name + " has been deleted successfully."));
		}
		if (!exceptions)
		{
			return Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Failed to delete group " + name + "."));
		}
		throw new GroupManagerException(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Failed to delete group " + name + ".")));
	}

	public string AddPermissions(string name, List<string> permissions)
	{
		if (!GroupExists(name))
		{
			return Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Group " + name + " doesn't exist."));
		}
		Group group = GetGroupByName(name);
		string permissions2 = group.Permissions;
		permissions.ForEach((string p) =>
		{
			group.AddPermission(p);
		});
		if (DbExt.Query(database, "UPDATE GroupList SET Commands=@0 WHERE GroupName=@1", new object[2] { group.Permissions, name }) == 1)
		{
			return "Group " + name + " has been modified successfully.";
		}
		group.Permissions = permissions2;
		return "";
	}

	public string DeletePermissions(string name, List<string> permissions)
	{
		if (!GroupExists(name))
		{
			return Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Group " + name + " doesn't exist."));
		}
		Group group = GetGroupByName(name);
		string permissions2 = group.Permissions;
		permissions.ForEach((string p) =>
		{
			group.RemovePermission(p);
		});
		if (DbExt.Query(database, "UPDATE GroupList SET Commands=@0 WHERE GroupName=@1", new object[2] { group.Permissions, name }) == 1)
		{
			return "Group " + name + " has been modified successfully.";
		}
		group.Permissions = permissions2;
		return "";
	}

	public void LoadPermisions()
	{
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Expected Obj, but got Unknown
		try
		{
			List<Group> list = new List<Group>(groups.Count);
			Dictionary<string, string> dictionary = new Dictionary<string, string>(groups.Count);
			QueryResult val = DbExt.QueryReader(database, "SELECT * FROM GroupList", Array.Empty<object>());
			try
			{
				while (val.Read())
				{
					string text = val.Get<string>("GroupName");
					list.Add(new Group(text, (Group)null, val.Get<string>("ChatColor"), val.Get<string>("Commands"))
					{
						Prefix = val.Get<string>("Prefix"),
						Suffix = val.Get<string>("Suffix")
					});
					try
					{
						dictionary.Add(text, val.Get<string>("Parent"));
					}
					catch (ArgumentException)
					{
						TShock.Log.ConsoleError(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("The group " + text + " appeared more than once. Keeping current group settings.")));
						return;
					}
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			int i;
			for (i = 0; i < groups.Count; i++)
			{
				if (list.All((Group g) => g.Name != groups[i].Name))
				{
					groups.RemoveAt(i--);
				}
			}
			foreach (Group newGroup in list)
			{
				Group val2 = groups.FirstOrDefault((Group g) => g.Name == newGroup.Name);
				if (val2 != null)
				{
					newGroup.AssignTo(val2);
				}
				else
				{
					groups.Add(newGroup);
				}
			}
			for (int num = 0; num < groups.Count; num++)
			{
				Group val3 = groups[num];
				if (!dictionary.TryGetValue(val3.Name, out var parentGroupName) || string.IsNullOrEmpty(parentGroupName))
				{
					continue;
				}
				val3.Parent = groups.FirstOrDefault((Group g) => g.Name == parentGroupName);
				if (val3.Parent == null)
				{
					TShock.Log.ConsoleError(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make($"Group {val3.Name} is referencing a non existent parent group {parentGroupName}, parent reference was removed.")));
					continue;
				}
				if (val3.Parent == val3)
				{
					TShock.Log.ConsoleWarn(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make("Group " + val3.Name + " is referencing itself as parent group; parent reference was removed.")));
				}
				List<Group> list2 = new List<Group> { val3 };
				Group val4 = val3;
				while (val4.Parent != null)
				{
					if (list2.Contains(val4.Parent))
					{
						TShock.Log.ConsoleError(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make($"Group \"{val4.Name}\" is referencing parent group {val4.Parent.Name} which is already part of the parent chain. Parent reference removed.")));
						val4.Parent = null;
						break;
					}
					list2.Add(val4);
					val4 = val4.Parent;
				}
			}
		}
		catch (Exception value)
		{
			TShock.Log.ConsoleError(Utils.GetString(VBY.Basic.Extension.FsaHelper.Make($"Error on reloading groups: {value}")));
		}
	}
}
