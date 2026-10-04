using System;

namespace CustomPlayer;

public static class TableInfo
{
	public class Prefix
	{
		public string Name;

		public int Id;

		public string Value;

		public DateTime StartTime;

		public DateTime EndTime;

		public string DurationText;

		public Prefix(string name, int id, string value, DateTime startTime, DateTime endTime, string durationText)
		{
			Name = name;
			Id = id;
			Value = value;
			StartTime = startTime;
			EndTime = endTime;
			DurationText = durationText;
		}
	}

	public class Suffix : Prefix
	{
		public Suffix(string name, int id, string value, DateTime startTime, DateTime endTime, string durationText)
			: base(name, id, value, startTime, endTime, durationText)
		{
		}
	}

	public class Useing
	{
		public string Name;

		public int ServerId;

		public int PrefixId;

		public int SuffixId;
	}

	public class PlayerList
	{
		public string Name;

		public string Commands;

		public string Group;

		public string ChatColor;
	}

	public class GroupList
	{
		public string GroupName;

		public string Parent;

		public string Commands;

		public string ChatColor;

		public string Prefix;

		public string Suffix;
	}

	public class ExpirationInfo
	{
		public string Name;

		public string Value;

		public string Type;

		public DateTime StartTime;

		public DateTime EndTime;

		public string DurationText;
	}
}
