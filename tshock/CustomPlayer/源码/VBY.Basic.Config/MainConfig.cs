using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using TShockAPI;

namespace VBY.Basic.Config;

public class MainConfig<T> where T : MainRoot, new()
{
	public string ConfigDirectory;

	public string FileName;

	public T Root = new T();

	public bool Normal;

	public string StateString = "正常";

	public string ErrorString = "无";

	public virtual string ConfigPath => Path.Combine(ConfigDirectory, FileName);

	public virtual string ConfigString
	{
		get
		{
			Type type = GetType();
			using StreamReader streamReader = new StreamReader(type.Assembly.GetManifestResourceStream(type.Namespace + "." + FileName) ?? throw new Exception(type.Namespace + "." + FileName + " resource no find"));
			return streamReader.ReadToEnd();
		}
	}

	public MainConfig(string configDirectory, string fileName = "config.json")
	{
		ConfigDirectory = configDirectory;
		FileName = fileName;
		if (!Directory.Exists(configDirectory))
		{
			Directory.CreateDirectory(configDirectory);
		}
		if (File.Exists(ConfigPath))
		{
			Read(null, readKey: true);
		}
		else
		{
			WriteAndRead(null, readKey: true);
		}
	}

	public virtual bool Read(TSPlayer? ply = null, bool readKey = false, bool log = false, T? obj = null)
	{
		//IL_005a: Expected Obj, but got Unknown
		Normal = true;
		try
		{
			Root = JsonConvert.DeserializeObject<T>(File.ReadAllText(ConfigPath)) ?? new T();
		}
		catch (FileNotFoundException ex)
		{
			StateString = FileName + "未找到";
			Normal = false;
			ErrorString = ex.ToString();
		}
		catch (JsonReaderException ex2)
		{
			JsonReaderException ex3 = ex2;
			StateString = "读取" + FileName + "错误";
			Normal = false;
			ErrorString = ((object)ex3).ToString();
		}
		catch (Exception ex4)
		{
			StateString = "未知错误";
			Normal = false;
			ErrorString = ex4.ToString();
		}
		if (!Normal)
		{
			LogAndOut(ErrorString, ply, log, TraceLevel.Error);
			T val = obj ?? JsonConvert.DeserializeObject<T>(ConfigString);
			if (val == null)
			{
				throw new Exception("配置读取错误");
			}
			Root = val;
			if (readKey)
			{
				Console.WriteLine("请按任意键继续...");
				Console.ReadKey();
			}
		}
		return Normal;
	}

	public virtual bool Write(TSPlayer? ply = null, bool readKey = false, bool log = false)
	{
		Normal = true;
		try
		{
			string contents = ((Environment.OSVersion.Platform != PlatformID.Win32NT) ? ConfigString.Replace("\r\n", "\n") : ConfigString);
			File.WriteAllText(ConfigPath, contents);
		}
		catch (Exception ex)
		{
			StateString = "写入默认配置文件出错";
			ErrorString = ex.ToString();
			Normal = false;
		}
		if (!Normal)
		{
			LogAndOut(ErrorString, ply, log, TraceLevel.Error);
			if (readKey)
			{
				Console.ReadKey();
			}
		}
		return Normal;
	}

	public virtual void WriteAndRead(TSPlayer? ply = null, bool readKey = false)
	{
		if (Write(ply, readKey))
		{
			Read(ply, readKey);
		}
	}

	public virtual void LogAndOut(string message, TSPlayer? ply = null, bool log = false, TraceLevel level = TraceLevel.Info)
	{
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		if (ply == null)
		{
			switch (level)
			{
			case TraceLevel.Error:
				Utils.WriteColorLine(message);
				break;
			case TraceLevel.Warning:
				Utils.WriteColorLine(message, ConsoleColor.DarkYellow);
				break;
			case TraceLevel.Info:
				Utils.WriteInfoLine(message);
				break;
			default:
				Console.WriteLine(message);
				break;
			}
		}
		else
		{
			switch (level)
			{
			case TraceLevel.Error:
				ply.SendErrorMessage(message);
				break;
			case TraceLevel.Warning:
				ply.SendWarningMessage(message);
				break;
			case TraceLevel.Info:
				ply.SendInfoMessage(message);
				break;
			default:
				ply.SendMessage(message, Color.White);
				break;
			}
		}
		if (log)
		{
			TShock.Log.Write(message, level);
		}
	}
}
