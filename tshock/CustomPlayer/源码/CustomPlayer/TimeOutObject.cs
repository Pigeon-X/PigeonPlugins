using System;

namespace CustomPlayer;

public class TimeOutObject
{
	public string Name;

	public string Value;

	public string Type;

	public string DurationText;

	public DateTime StartTime;

	public DateTime EndTime;

	public bool TimeOuted;

	public int Id;

	public bool NoExpired
	{
		get
		{
			if (!(DurationText == "-1"))
			{
				return EndTime > DateTime.Now;
			}
			return true;
		}
	}

	public string RemainTime
	{
		get
		{
			if (DurationText == "-1")
			{
				return "无限";
			}
			TimeSpan timeSpan = EndTime - DateTime.Now;
			if (!(timeSpan < TimeSpan.Zero))
			{
				return timeSpan.ToString("d\\.hh\\:mm\\:ss");
			}
			return "已过期";
		}
	}

	public TimeOutObject(string name, string value, string type, DateTime startTime, DateTime endTime, string durationText, int id = -1)
	{
		Name = name;
		Value = value;
		Type = type;
		StartTime = startTime;
		EndTime = endTime;
		DurationText = durationText;
		Id = id;
	}

	public void Delete()
	{
		switch (Type)
		{
		case "Permission":
		case "Group":
			Utils.Query("delete from ExpirationInfo where Name = @0 AND Value = @1 AND Type = @2 AND StartTime = @3 AND EndTime = @4 AND DurationText = @5", Name, Value, Type, StartTime, EndTime, DurationText);
			break;
		case "Prefix":
		case "Suffix":
			Utils.Query("delete from " + Type + " where Name = @0 AND Value = @1 AND ID = @2 AND StartTime = @3 AND EndTime = @4 AND DurationText = @5", Name, Value, Id, StartTime, EndTime, DurationText);
			break;
		}
	}
}
