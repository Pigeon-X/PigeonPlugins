using System;
using System.Data;

namespace VBY.Basic.Extension;

public static class VBYDbExt
{
	public static int GetInt32(this IDataReader reader, string name)
	{
		return reader.GetInt32(reader.GetOrdinal(name));
	}

	public static string GetString(this IDataReader reader, string name)
	{
		return reader.GetString(reader.GetOrdinal(name));
	}

	public static DateTime GetDateTime(this IDataReader reader, string name)
	{
		return reader.GetDateTime(reader.GetOrdinal(name));
	}

	/// <summary>数据库不可用时 Reader 会是 null，这里直接当「没有数据」返回，不能让它抛空引用。</summary>
	public static void ForEach(this IDataReader args, Action<IDataReader> action)
	{
		if (args == null)
		{
			return;
		}
		while (args.Read())
		{
			action(args);
		}
	}

	public static void DoForEach(this IDataReader args, Action<IDataReader> action)
	{
		if (args == null)
		{
			return;
		}
		action(args);
		while (args.Read())
		{
			action(args);
		}
	}
}