using System;
using System.Collections.Generic;

namespace VBY.Basic.Extension;

public static class VBYExt
{
	public static void RemoveRange<T>(this List<T> list, IEnumerable<T> collection)
	{
		foreach (T item in collection)
		{
			list.Remove(item);
		}
	}

	public static T? Find<T>(this T[] array, Predicate<T> predicate)
	{
		foreach (T val in array)
		{
			if (predicate(val))
			{
				return val;
			}
		}
		return default;
	}
}
