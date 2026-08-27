using System.Globalization;
using System.ComponentModel;
using System.Text;

namespace UnicornOverlord;

internal sealed class ModChoice : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;
	public required int Value { get; init; }
	public required String EnglishName { get; init; }
	public required String ChineseName { get; init; }
	public NameValueInfo? Source { get; init; }
	public String Name => Source?.Name ?? (ApplicationSettings.Language == 0 && !String.IsNullOrWhiteSpace(EnglishName) ? EnglishName : ChineseName);
	public String DisplayName => Name;
	public void RefreshName()
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
	}
}

internal sealed class ModSkillInfo
{
	public required ModChoice Choice { get; init; }
	public required bool IsPassive { get; init; }
	public required int Cost { get; init; }
	public required double PhysicalPotency { get; init; }
	public required double MagicalPotency { get; init; }
	public required int Accuracy { get; init; }
	public required int TargetShape { get; init; }
	public required double EffectValue { get; init; }
	public required String ChineseDescription { get; init; }
	public String TypeText => IsPassive ? "被动技能（PP）" : "主动技能（AP）";
	public String Description => ChineseDescription;
}

internal sealed class ModClassInfo
{
	public required int Id { get; init; }
	public required int Ap { get; init; }
	public required int Pp { get; init; }
	public required double[] Growths { get; init; }
	public required int[] ActiveSkills { get; init; }
	public required int[] ActiveLevels { get; init; }
	public required int[] PassiveSkills { get; init; }
	public required int[] PassiveLevels { get; init; }
}

internal sealed record ModRecordInfo(int Id, int ValueA, int ValueB, int ValueC, int ValueD, int ValueE);

internal sealed record ModShopRecordInfo(int Id, uint Address, int ItemId, int Stock, int Price, String LocationKey, String GroupName, bool IsShared);

internal sealed class ModLocationChoice : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;
	public required String Key { get; init; }
	public required String EnglishName { get; init; }
	public required String JapaneseName { get; init; }
	public required String ChineseName { get; init; }
	public String DisplayName => ApplicationSettings.Language switch
	{
		0 => EnglishName,
		1 => JapaneseName,
		_ => ChineseName,
	};
	public void RefreshName() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
}

internal sealed class ModRecordChoice : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;
	public required int Value { get; init; }
	public required String LocationKey { get; init; }
	public required String EnglishLocation { get; init; }
	public required String JapaneseLocation { get; init; }
	public required String ChineseLocation { get; init; }
	public required ModChoice Detail { get; init; }
	public String? EnglishFacilityType { get; init; }
	public String? ChineseFacilityType { get; init; }
	public int Ordinal { get; init; }
	public int GroupCount { get; init; }

	public String DisplayName
	{
		get
		{
			bool english = ApplicationSettings.Language == 0;
			String location = ApplicationSettings.Language switch { 0 => EnglishLocation, 1 => JapaneseLocation, _ => ChineseLocation };
			String facilityType = english ? EnglishFacilityType ?? String.Empty : ChineseFacilityType ?? String.Empty;
			String prefix = String.IsNullOrEmpty(facilityType) ? location : $"{location} · {facilityType}";
			String suffix = GroupCount > 0
				? english ? $"{Detail.Name} · recruit {Ordinal}/{GroupCount}" : $"{Detail.Name} · 招募 {Ordinal}/{GroupCount}"
				: Detail.Name;
			return $"{prefix} · {suffix}";
		}
	}
	public String DetailDisplayName
	{
		get
		{
			String suffix = GroupCount > 0
				? ApplicationSettings.Language == 0 ? $"{Detail.Name} · recruit {Ordinal}/{GroupCount}" : $"{Detail.Name} · 招募 {Ordinal}/{GroupCount}"
				: Detail.Name;
			return IsShared ? $"{suffix} · {(ApplicationSettings.Language == 0 ? "shared stock" : "共享库存")}" : suffix;
		}
	}
	public bool IsShared { get; init; }

	public void RefreshName()
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DetailDisplayName)));
	}
}

internal static class ModCatalog
{
	public static IReadOnlyList<ModSkillInfo> Skills { get; } = LoadSkills();
	public static IReadOnlyList<ModChoice> SkillChoices { get; } = Skills.Select(skill => skill.Choice).ToArray();
	public static IReadOnlyList<ModChoice> ActiveSkillChoices { get; } = CreateSkillChoices(false);
	public static IReadOnlyList<ModChoice> PassiveSkillChoices { get; } = CreateSkillChoices(true);
	public static IReadOnlyList<ModChoice> ActiveSkillChoicesWithoutEmpty { get; } = ActiveSkillChoices.Where(choice => choice.Value != 0).ToArray();
	public static IReadOnlyList<ModChoice> PassiveSkillChoicesWithoutEmpty { get; } = PassiveSkillChoices.Where(choice => choice.Value != 0).ToArray();
	public static IReadOnlyList<ModChoice> ClassChoices { get; } = Info.Instance().Class
		.Where(item => item.Value is >= 1 and <= 73)
		.Select(item => new ModChoice { Value = checked((int)item.Value), EnglishName = String.Empty, ChineseName = item.Name, Source = item })
		.ToArray();
	public static IReadOnlyList<ModChoice> ItemChoices { get; } = Info.Instance().Item
		.Where(item => item.Value <= 970)
		.Select(item => new ModChoice { Value = checked((int)item.Value), EnglishName = item.Name, ChineseName = item.Name, Source = item })
		.ToArray();
	public static IReadOnlyDictionary<int, ModClassInfo> Classes { get; } = LoadClasses();
	public static IReadOnlyDictionary<int, ModRecordInfo> FortRecords { get; } = LoadRecords("fortmod.txt", 3);
	public static IReadOnlyDictionary<int, ModRecordInfo> MineRecords { get; } = LoadRecords("minemod.txt", 5);
	public static IReadOnlyDictionary<int, ModShopRecordInfo> ShopRecords { get; } = LoadShopRecords();
	public static IReadOnlyList<ModRecordChoice> FortRecordChoices { get; } = CreateFortRecordChoices();
	public static IReadOnlyList<ModRecordChoice> MineRecordChoices { get; } = CreateMineRecordChoices();
	public static IReadOnlyList<ModRecordChoice> ShopRecordChoices { get; } = CreateShopRecordChoices();
	public static IReadOnlyList<ModLocationChoice> FortLocations { get; } = CreateLocations(FortRecordChoices);
	public static IReadOnlyList<ModLocationChoice> MineLocations { get; } = CreateLocations(MineRecordChoices);
	public static IReadOnlyList<ModLocationChoice> ShopLocations { get; } = CreateLocations(ShopRecordChoices);

	public static ModChoice? FindSkill(int id) => SkillChoices.FirstOrDefault(choice => choice.Value == id);
	public static ModChoice? FindClass(int id) => ClassChoices.FirstOrDefault(choice => choice.Value == id);
	public static ModChoice? FindItem(int id) => ItemChoices.FirstOrDefault(choice => choice.Value == id);
	public static ModRecordChoice? FindFortRecord(int id) => FortRecordChoices.FirstOrDefault(choice => choice.Value == id);
	public static ModRecordChoice? FindMineRecord(int id) => MineRecordChoices.FirstOrDefault(choice => choice.Value == id);
	public static ModRecordChoice? FindShopRecord(int id) => ShopRecordChoices.FirstOrDefault(choice => choice.Value == id);
	public static void RefreshLocalizedNames()
	{
		foreach (ModChoice choice in SkillChoices.Concat(ClassChoices).Concat(ItemChoices).Distinct()) choice.RefreshName();
		foreach (ModRecordChoice choice in FortRecordChoices.Concat(MineRecordChoices).Concat(ShopRecordChoices)) choice.RefreshName();
		foreach (ModLocationChoice choice in FortLocations.Concat(MineLocations).Concat(ShopLocations)) choice.RefreshName();
	}

	private static IReadOnlyList<ModLocationChoice> CreateLocations(IReadOnlyList<ModRecordChoice> records) => records
		.GroupBy(record => record.LocationKey)
		.Select(group =>
		{
			ModRecordChoice first = group.First();
			String english = String.IsNullOrEmpty(first.EnglishFacilityType) ? first.EnglishLocation : $"{first.EnglishLocation} · {first.EnglishFacilityType}";
			String japanese = String.IsNullOrEmpty(first.ChineseFacilityType) ? first.JapaneseLocation : $"{first.JapaneseLocation} · {first.ChineseFacilityType}";
			String chinese = String.IsNullOrEmpty(first.ChineseFacilityType) ? first.ChineseLocation : $"{first.ChineseLocation} · {first.ChineseFacilityType}";
			return new ModLocationChoice { Key = group.Key, EnglishName = english, JapaneseName = japanese, ChineseName = chinese };
		}).ToArray();

	private static IReadOnlyList<ModRecordChoice> CreateFortRecordChoices()
	{
		(String English, String Chinese, int Start, int Count)[] locations =
		[
			("Fort Soligie", "索力吉堡垒", 1, 3), ("Fort Thessalon", "泰泽隆堡垒", 4, 3),
			("Fort Chandelis", "香杜里堡垒", 7, 3), ("Fort Mainteneaut", "曼图诺堡垒", 10, 3),
			("Fort Rimitz", "利米茨堡垒", 13, 3), ("Fort Groux", "格鲁堡垒", 16, 3),
			("Fort Paradis", "帕拉迪斯堡垒", 19, 3), ("Fort Veille", "威尔堡垒", 22, 3),
			("Walled City of Barbatimo", "要塞都市巴巴奇莫", 25, 6), ("Fort Colmarre", "科尔马堡垒", 31, 3),
			("Fort Gromond", "格罗蒙德堡垒", 34, 3), ("Fort Lonteria", "隆特利亚堡垒", 37, 4),
			("Fort Zelkova", "泽尔库巴堡垒", 41, 3), ("Walled City of Eucuit", "要塞都市尤奎特", 44, 6),
			("Fort Istania", "伊斯塔尼亚堡垒", 50, 3), ("Fort Longeraige", "隆格拉吉堡垒", 53, 3),
			("Fort Cypla", "西普利堡垒", 56, 3), ("Fort Plaine", "普雷努堡垒", 59, 3),
			("Fort Epine Morceaux", "埃匹摩尔松堡垒", 62, 3), ("Fort Herstann", "赫尔修坦堡垒", 65, 3),
			("Bandit's Keep", "盗贼堡垒", 68, 4), ("Walled City of Adopti", "要塞都市亚德普提", 72, 6),
			("Fort Neumont", "诺伊莫特堡垒", 78, 3), ("Baumratte", "鲍姆拉特斗技场", 81, 6),
			("Fort Farzieg", "法吉格堡垒", 87, 3), ("Fort Hossent", "霍赞特堡垒", 90, 3),
			("Fort Asterweiss", "亚斯特维兹堡垒", 93, 3), ("Pritzlasse Fortress", "普里茨特拉泽要塞", 96, 3),
			("Castle Soldraga", "索尔德拉迦城", 99, 6), ("Fort Schusse", "邱扎堡垒", 105, 3),
			("Fort Pikkimp", "皮耶金普堡垒", 108, 3), ("Fort Joperse", "乔佩尔塞堡垒", 111, 3),
			("Fort Mettza", "梅扎堡垒", 114, 3), ("Castle Laurhal", "劳尔哈尔城", 117, 6),
			("Fort Kolkkea", "科尔凯亚堡垒", 123, 3), ("Fort Aras", "阿拉斯堡垒", 126, 3),
			("Fort Payvakea", "派瓦凯亚堡垒", 129, 3), ("Voryatan Citadel", "要塞都市沃里坦", 132, 6),
			("Fort Korim", "科尔梅堡垒", 138, 3), ("Fort Kolmerengas", "科尔梅雷佳斯堡垒", 141, 3),
			("Fort Souill", "索伊尔堡垒", 144, 3), ("Palanspelt Palace", "地下宫殿帕兰希佩尔特", 147, 6),
			("Ancient City of Bastoritza", "古都巴斯塔利札", 153, 6), ("Fort Perzmost", "佩里斯莫斯特堡垒", 159, 4),
			("Fort Sedorosha", "塞多罗夏堡垒", 163, 4), ("Fort Kannadeo", "康纳迪欧堡垒", 167, 4),
			("Fort Kharodetz", "卡洛杰茨堡垒", 171, 4), ("Fort Servel", "赛培尔堡垒", 175, 4),
			("Dracodorina Citadel", "要塞都市德拉科多利纳", 179, 6), ("Fort Garava", "加拉瓦堡垒", 185, 4),
			("Fort Sebatshet", "瑟巴托谢特堡垒", 189, 4), ("Fortified City of Solvaquad", "要塞都市索尔巴库夸多", 193, 6),
			("Fort Veterana", "贝特拉纳堡垒", 199, 4), ("Fort Terrarosa", "特拉罗沙堡垒", 203, 4),
			("Largion Citadel", "要塞都市拉简", 207, 6), ("Fort Viridian Hill", "比里疆希尔堡垒", 213, 4),
			("Fort Foxwell", "福克斯维尔堡垒", 217, 4), ("Fort Autumnhill", "奥特姆希尔堡垒", 221, 4),
			("Fort Kingsrock", "金斯洛克堡垒", 225, 4), ("Walled City of Peyston", "要塞都市佩兹顿", 229, 6),
			("Bisfaine Basilica", "比斯法因大教堂", 235, 6), ("Fort Greyhill", "格雷希尔堡垒", 241, 4),
			("Fort Worchester", "沃切斯塔堡垒", 245, 4),
		];
		var result = new List<ModRecordChoice>(248);
		foreach ((String english, String chinese, int start, int count) in locations)
		{
			for (int offset = 0; offset < count; offset++)
			{
				int id = start + offset;
				result.Add(new ModRecordChoice
				{
					Value = id, LocationKey = english, EnglishLocation = english, JapaneseLocation = chinese, ChineseLocation = chinese,
					Detail = FindClass(FortRecords[id].ValueA) ?? throw new InvalidDataException($"据点记录 {id} 的原版职业不存在。"),
					Ordinal = offset + 1, GroupCount = count,
				});
			}
		}
		return result;
	}

	private static IReadOnlyList<ModRecordChoice> CreateMineRecordChoices()
	{
		(String English, String Chinese, int Start, int Count)[] locations =
		[
			("Cornia Quarry", "科尔尼亚采掘场", 0, 11), ("Drakenhold Quarry", "德拉肯加德采掘场", 11, 11),
			("Elheim Quarry", "艾尔海姆采掘场", 22, 11), ("Bastorias Quarry", "巴斯特利亚斯采掘场", 33, 12),
			("Albion Quarry", "阿尔比昂采掘场", 45, 18),
		];
		return locations.SelectMany(location => Enumerable.Range(location.Start, location.Count).Select(id => new ModRecordChoice
		{
			Value = id, LocationKey = location.English, EnglishLocation = location.English, JapaneseLocation = location.Chinese, ChineseLocation = location.Chinese,
			Detail = FindItem(MineRecords[id].ValueA) ?? throw new InvalidDataException($"采矿记录 {id} 的原版物品不存在。"),
		})).ToArray();
	}

	private static IReadOnlyList<ModRecordChoice> CreateShopRecordChoices() => ShopRecords.Values.OrderBy(record => record.Id).Select(record => new ModRecordChoice
	{
		Value = record.Id, LocationKey = record.LocationKey,
		EnglishLocation = record.LocationKey.Split('|')[0], JapaneseLocation = record.LocationKey.Split('|')[1], ChineseLocation = record.LocationKey.Split('|')[2],
		EnglishFacilityType = "Armorer", ChineseFacilityType = "武具店",
		Detail = FindItem(record.ItemId) ?? throw new InvalidDataException($"商店记录 {record.Id} 的原版物品不存在。"),
		IsShared = record.IsShared,
	}).ToArray();

	private static IReadOnlyList<ModChoice> CreateSkillChoices(bool passive)
	{
		var choices = new List<ModChoice>
		{
			new() { Value = 0, EnglishName = "(empty)", ChineseName = "（空）" },
		};
		choices.AddRange(Skills.Where(skill => skill.IsPassive == passive).Select(skill => skill.Choice));
		return choices;
	}

	private static IReadOnlyList<ModSkillInfo> LoadSkills()
	{
		IReadOnlyDictionary<int, String> descriptions = ReadRows("skilldesc-cn.txt").ToDictionary(values => ParseInt(values[0]),
			values => Encoding.UTF8.GetString(Convert.FromBase64String(values[1])));
		var result = new List<ModSkillInfo>();
		foreach (String[] values in ReadRows("skill.txt"))
		{
			if (values.Length < 10) continue;
			result.Add(new ModSkillInfo
			{
				Choice = new ModChoice { Value = ParseInt(values[0]), EnglishName = values[1], ChineseName = values[2] },
				IsPassive = values[3] == "P",
				Cost = ParseInt(values[4]),
				PhysicalPotency = ParseDouble(values[5]),
				MagicalPotency = ParseDouble(values[6]),
				Accuracy = ParseInt(values[7]),
				TargetShape = ParseInt(values[8]),
				EffectValue = ParseDouble(values[9]),
				ChineseDescription = descriptions.GetValueOrDefault(ParseInt(values[0]), String.Empty),
			});
		}
		return result;
	}

	private static IReadOnlyDictionary<int, ModClassInfo> LoadClasses()
	{
		var result = new Dictionary<int, ModClassInfo>();
		foreach (String[] values in ReadRows("classmod.txt"))
		{
			if (values.Length < 29) continue;
			int id = ParseInt(values[0]);
			result[id] = new ModClassInfo
			{
				Id = id,
				Ap = ParseInt(values[1]),
				Pp = ParseInt(values[2]),
				Growths = values.Skip(3).Take(10).Select(ParseDouble).ToArray(),
				ActiveSkills = [ParseInt(values[13]), ParseInt(values[15]), ParseInt(values[17]), ParseInt(values[19])],
				ActiveLevels = [ParseInt(values[14]), ParseInt(values[16]), ParseInt(values[18]), ParseInt(values[20])],
				PassiveSkills = [ParseInt(values[21]), ParseInt(values[23]), ParseInt(values[25]), ParseInt(values[27])],
				PassiveLevels = [ParseInt(values[22]), ParseInt(values[24]), ParseInt(values[26]), ParseInt(values[28])],
			};
		}
		return result;
	}

	private static IReadOnlyDictionary<int, ModRecordInfo> LoadRecords(String fileName, int valueCount)
	{
		var result = new Dictionary<int, ModRecordInfo>();
		foreach (String[] values in ReadRows(fileName))
		{
			if (values.Length < valueCount + 1) continue;
			int[] parsed = values.Select(ParseInt).ToArray();
			result[parsed[0]] = new ModRecordInfo(parsed[0], parsed.ElementAtOrDefault(1), parsed.ElementAtOrDefault(2), parsed.ElementAtOrDefault(3), parsed.ElementAtOrDefault(4), parsed.ElementAtOrDefault(5));
		}
		return result;
	}

	private static IReadOnlyDictionary<int, ModShopRecordInfo> LoadShopRecords()
	{
		var result = new Dictionary<int, ModShopRecordInfo>();
		foreach (String[] values in ReadRows("shopmod.txt"))
		{
			if (values.Length < 10) continue;
			int id = ParseInt(values[0]);
			uint address = UInt32.Parse(values[1].AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			String locationKey = $"{values[5]}|{values[6]}|{values[7]}";
			result[id] = new ModShopRecordInfo(id, address, ParseInt(values[2]), ParseInt(values[3]), ParseInt(values[4]), locationKey, values[8], values[9] == "1");
		}
		return result;
	}

	private static IEnumerable<String[]> ReadRows(String fileName)
	{
		String path = Path.Combine(AppContext.BaseDirectory, "info", fileName);
		if (!File.Exists(path)) yield break;
		foreach (String line in File.ReadLines(path))
		{
			if (String.IsNullOrWhiteSpace(line) || line[0] == '#') continue;
			yield return line.Split('\t');
		}
	}

	private static int ParseInt(String value) => Int32.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
	private static double ParseDouble(String value) => Double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
}
