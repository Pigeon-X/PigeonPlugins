namespace System;

public static class StringExt
{
	public static string LastWord(this string str)
	{
		for (int num = str.Length - 1; num >= 0; num--)
		{
			if (char.IsUpper(str[num]))
			{
				return str.Substring(num);
			}
		}
		return str;
	}
}
