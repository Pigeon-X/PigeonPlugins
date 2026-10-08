using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Permabuffs_V2;

public class Config
{
	public BuffGroup[] buffgroups = new BuffGroup[3]
	{
		new BuffGroup
		{
			groupName = "probuffs",
			groupPerm = "probuffs",
			buffIDs = new List<int>
			{
				1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
				11, 12, 13, 14, 15, 16, 17, 18, 26, 29,
				43, 48, 58, 59, 63, 71, 73, 74, 75, 76,
				77, 78, 79, 87, 89, 93, 104, 105, 106, 107,
				108, 109, 110, 111, 112, 113, 114, 115, 116, 117,
				119, 121, 122, 123, 124, 146, 150, 151, 157, 158,
				159, 165, 173, 174, 175, 176, 177, 178, 179, 180,
				181, 192, 198, 205, 206, 207, 215, 257, 311, 314,
				321
			}
		},
		new BuffGroup
		{
			groupName = "petbuffs",
			groupPerm = "petbuffs",
			buffIDs = new List<int>
			{
				19, 27, 40, 41, 42, 45, 49, 50, 51, 52,
				53, 54, 55, 56, 57, 61, 64, 65, 66, 81,
				82, 84, 85, 90, 91, 92, 101, 102, 127, 128,
				129, 130, 131, 132, 136, 141, 142, 143, 152, 154,
				155, 162, 168, 190, 191, 193, 200, 201, 202, 212,
				217, 218, 219, 230, 258, 259, 260, 261, 262, 263,
				264, 265, 266, 267, 268, 274, 275, 276, 277, 278,
				279, 280, 281, 282, 283, 284, 285, 286, 287, 288,
				289, 290, 291, 292, 293, 294, 295, 296, 297, 298,
				299, 300, 301, 302, 303, 304, 305, 317, 318
			}
		},
		new BuffGroup
		{
			groupName = "debuffs",
			groupPerm = "debuffs",
			buffIDs = new List<int>
			{
				21, 24, 25, 39, 47, 67, 69, 70, 72, 80,
				86, 88, 94, 103, 137, 144, 145, 148, 149, 156,
				160, 163, 164, 194, 195, 196, 197, 320
			}
		}
	};

	public RegionBuff[] regionbuffs = new RegionBuff[1]
	{
		new RegionBuff
		{
			regionName = "spawn",
			buffs = new Dictionary<int, int> { { 11, 10 } }
		}
	};

	public void Write(string path)
	{
		File.WriteAllText(path, JsonConvert.SerializeObject((object)this, (Formatting)1));
	}

	public static Config Read(string path)
	{
		if (!File.Exists(path))
		{
			Config config = new Config();
			config.Write(path);
			return config;
		}
		return JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
	}
}
