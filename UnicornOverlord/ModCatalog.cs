using System.Globalization;
using System.ComponentModel;

namespace UnicornOverlord;

internal sealed class ModChoice : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;
	public required int Value { get; init; }
	public required String EnglishName { get; init; }
	public required String ChineseName { get; init; }
	public NameValueInfo? Source { get; init; }
	public String Name => Source?.Name ?? (ApplicationSettings.Language == 0 && !String.IsNullOrWhiteSpace(EnglishName) ? EnglishName : ChineseName);
	public String DisplayName => $"{Name} (ID {Value})";
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
	public String TypeText => IsPassive ? "被动技能（PP）" : "主动技能（AP）";
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

internal static class ModCatalog
{
	public static IReadOnlyList<ModSkillInfo> Skills { get; } = LoadSkills();
	public static IReadOnlyList<ModChoice> SkillChoices { get; } = Skills.Select(skill => skill.Choice).ToArray();
	public static IReadOnlyList<ModChoice> ActiveSkillChoices { get; } = CreateSkillChoices(false);
	public static IReadOnlyList<ModChoice> PassiveSkillChoices { get; } = CreateSkillChoices(true);
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
	public static IReadOnlyDictionary<int, ModRecordInfo> ShopRecords { get; } = LoadRecords("shopmod.txt", 3);

	public static ModChoice? FindSkill(int id) => SkillChoices.FirstOrDefault(choice => choice.Value == id);
	public static ModChoice? FindClass(int id) => ClassChoices.FirstOrDefault(choice => choice.Value == id);
	public static ModChoice? FindItem(int id) => ItemChoices.FirstOrDefault(choice => choice.Value == id);
	public static void RefreshLocalizedNames()
	{
		foreach (ModChoice choice in SkillChoices.Concat(ClassChoices).Concat(ItemChoices).Distinct()) choice.RefreshName();
	}

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
