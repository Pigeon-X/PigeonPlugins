using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TShockAPI;

namespace PeaceMode;

public class Config
{
	public static readonly string FilePath = Path.Combine(TShock.SavePath, "PeaceMode.json");

	[JsonProperty("Enabled", Order = 1)]
	public bool Enabled { get; set; }

	[JsonProperty("ClearExistingNpcsOnEnable", Order = 2)]
	public bool ClearExistingNpcsOnEnable { get; set; } = true;

	[JsonProperty("IncludeTownNpcs", Order = 3)]
	public bool IncludeTownNpcs { get; set; }

	[JsonProperty("BanEvents", Order = 4)]
	public bool BanEvents { get; set; } = true;

	[JsonProperty("BanRain", Order = 5)]
	public bool BanRain { get; set; } = true;

	[JsonProperty("BanMeteor", Order = 6)]
	public bool BanMeteor { get; set; } = true;

	public void Write()
	{
		using FileStream stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Write);
		using StreamWriter streamWriter = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		streamWriter.Write(JsonConvert.SerializeObject((object)this, (Formatting)1));
	}

	public static Config Read()
	{
		if (!File.Exists(FilePath))
		{
			Config config = new Config();
			config.Write();
			return config;
		}
		try
		{
			return JsonConvert.DeserializeObject<Config>(File.ReadAllText(FilePath)) ?? new Config();
		}
		catch (Exception ex)
		{
			TShock.Log.ConsoleError("[PeaceMode] 配置文件解析失败，使用默认配置: " + ex.Message);
			return new Config();
		}
	}
}
