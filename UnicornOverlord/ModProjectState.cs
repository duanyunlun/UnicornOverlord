using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;

namespace UnicornOverlord;

internal sealed class ModProjectState
{
	private readonly Dictionary<String, ModOptionState> mOptions = new(StringComparer.Ordinal);

	public ModProjectState()
	{
		Ability = new AbilityEditorState();
		Classes = new ClassEditorState();
		Fort = new FortEditorState();
		Mine = new MineEditorState();
		Shop = new ShopEditorState();
		Text = new TextModProjectState();
	}

	public AbilityEditorState Ability { get; }
	public ClassEditorState Classes { get; }
	public FortEditorState Fort { get; }
	public MineEditorState Mine { get; }
	public ShopEditorState Shop { get; }
	public TextModProjectState Text { get; }
	public BattlePreviewProjectState BattlePreview { get; } = new();
	public CharacterRandomizerProjectState CharacterRandomizer { get; } = new();
	public TypeMatchupProjectState TypeMatchups { get; } = new();
	public SixMemberProjectState SixMemberUnits { get; } = new();

	public ModOptionState Options(String key)
	{
		if (!mOptions.TryGetValue(key, out ModOptionState? state))
		{
			state = new ModOptionState();
			mOptions.Add(key, state);
		}
		return state;
	}

	public String ToJson(IReadOnlyCollection<ModModule> modules, ModTarget target)
	{
		var payload = new
		{
			schemaVersion = 1,
			target = new { target.Key, target.GameVersion, target.TitleId, target.BuildId },
			modules = modules.Select(CreateModuleSnapshot).ToArray(),
		};
		return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
	}

	public String ToTextJson(ModTarget target)
	{
		var payload = new
		{
			schemaVersion = 1,
			target = new { target.Key, target.GameVersion, target.TitleId, target.BuildId },
			module = "text_editor",
			language = Text.SelectedLanguage.Name,
			source = Path.GetFileName(Text.SourceCpkPath),
			changes = Text.Tables.SelectMany(table => Enumerable.Range(0, table.Document.Count)
				.Where(table.Document.IsChanged)
				.Select(index => new { table.ArchivePath, Index = index, Text = table.Document.GetText(index) })).ToArray(),
		};
		return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
	}

	private object CreateModuleSnapshot(ModModule module) => module.Key switch
	{
		"ability_editor" => new { module.Key, records = Ability.ModifiedRecords.Select(record => record.Snapshot()).ToArray() },
		"class_editor" => new { module.Key, records = Classes.ModifiedRecords.Select(record => record.Snapshot()).ToArray() },
		"fort_editor" => new { module.Key, records = Fort.ModifiedRecords.Select(record => record.Snapshot()).ToArray() },
		"mine_editor" => new { module.Key, records = Mine.ModifiedRecords.Select(record => new { record.RecordId, record.ItemId, record.Weight, record.DigTarget, record.RoundLimit }).ToArray() },
		"shop_editor" => new { module.Key, records = Shop.ModifiedRecords.Select(record => record.Snapshot()).ToArray() },
		"battle_preview" => new { module.Key, mode = BattlePreview.Mode },
		"character_randomizer" => new { module.Key, CharacterRandomizer.Seed, CharacterRandomizer.MixPromotionTiers },
		"type_matchups" => new { module.Key, TypeMatchups.CavalryVsInfantry, TypeMatchups.ArcherVsFlying, TypeMatchups.FlyingVsCavalry },
		"six_member_units" => new { module.Key, SixMemberUnits.HonorCost },
		_ => new { module.Key },
	};
}

internal sealed class TextModProjectState
{
	public String ToolPath { get; set; } = String.Empty;
	public String SourceCpkPath { get; set; } = String.Empty;
	public TextModLanguage SelectedLanguage { get; set; } = TextModLanguage.All[0];
	public ModTarget SelectedTarget { get; set; } = ModTarget.Asia;
	public ObservableCollection<TextTable> Tables { get; } = [];
}

internal sealed class ModOptionState
{
	public bool IsEnabled { get; set; }
}

internal sealed class BattlePreviewProjectState
{
	public int Mode { get; set; }
}

internal sealed class CharacterRandomizerProjectState
{
	public int Seed { get; set; } = 20260826;
	public bool MixPromotionTiers { get; set; }
}

internal sealed class TypeMatchupProjectState
{
	public double CavalryVsInfantry { get; set; } = 2;
	public double ArcherVsFlying { get; set; } = 2;
	public double FlyingVsCavalry { get; set; } = 2;
}

internal sealed class SixMemberProjectState
{
	public int HonorCost { get; set; } = 500;
}

internal sealed class AbilityRecordEdit
{
	public AbilityRecordEdit(ModSkillInfo original)
	{
		Original = original;
		Cost = original.Cost;
		Accuracy = original.Accuracy;
		TargetShape = original.TargetShape;
		PhysicalPotency = original.PhysicalPotency;
		MagicalPotency = original.MagicalPotency;
		EffectValue = original.EffectValue;
	}

	public ModSkillInfo Original { get; }
	public int RecordId => Original.Choice.Value;
	public int Cost { get; set; }
	public int Accuracy { get; set; }
	public int TargetShape { get; set; }
	public double PhysicalPotency { get; set; }
	public double MagicalPotency { get; set; }
	public double EffectValue { get; set; }
	public bool IsModified => Cost != Original.Cost || Accuracy != Original.Accuracy || TargetShape != Original.TargetShape ||
		PhysicalPotency != Original.PhysicalPotency || MagicalPotency != Original.MagicalPotency || EffectValue != Original.EffectValue;
	public object Snapshot() => new { RecordId, Cost, Accuracy, TargetShape, PhysicalPotency, MagicalPotency, EffectValue };
}

internal sealed class AbilityEditorState
{
	private readonly IReadOnlyDictionary<int, AbilityRecordEdit> mRecords;

	public AbilityEditorState()
	{
		mRecords = ModCatalog.Skills.ToDictionary(skill => skill.Choice.Value, skill => new AbilityRecordEdit(skill));
		SelectedRecord = mRecords.GetValueOrDefault(372) ?? mRecords.Values.First();
	}

	public AbilityRecordEdit SelectedRecord { get; private set; }
	public int FilterIndex { get; set; }
	public IEnumerable<AbilityRecordEdit> ModifiedRecords => mRecords.Values.Where(record => record.IsModified);
	public void Select(int recordId)
	{
		if (mRecords.TryGetValue(recordId, out AbilityRecordEdit? record)) SelectedRecord = record;
	}
}

internal sealed class ClassRecordEdit
{
	public ClassRecordEdit(ModClassInfo original)
	{
		Original = original;
		Ap = original.Ap;
		Pp = original.Pp;
		Growths = [.. original.Growths];
		ActiveSkills = CreateSlots(false, original.ActiveSkills, original.ActiveLevels);
		PassiveSkills = CreateSlots(true, original.PassiveSkills, original.PassiveLevels);
	}

	public ModClassInfo Original { get; }
	public int RecordId => Original.Id;
	public int Ap { get; set; }
	public int Pp { get; set; }
	public double[] Growths { get; }
	public ObservableCollection<ModSkillSlot> ActiveSkills { get; }
	public ObservableCollection<ModSkillSlot> PassiveSkills { get; }
	public bool IsModified => Ap != Original.Ap || Pp != Original.Pp || !Growths.SequenceEqual(Original.Growths) ||
		SlotsModified(ActiveSkills, Original.ActiveSkills, Original.ActiveLevels) || SlotsModified(PassiveSkills, Original.PassiveSkills, Original.PassiveLevels);
	public object Snapshot() => new
	{
		RecordId, Ap, Pp, Growths,
		ActiveSkills = ActiveSkills.Select(slot => new { SkillId = slot.SelectedSkill?.Value ?? 0, slot.Level }).ToArray(),
		PassiveSkills = PassiveSkills.Select(slot => new { SkillId = slot.SelectedSkill?.Value ?? 0, slot.Level }).ToArray(),
	};

	private static ObservableCollection<ModSkillSlot> CreateSlots(bool passive, IReadOnlyList<int> skills, IReadOnlyList<int> levels) =>
		[.. Enumerable.Range(0, 4).Select(index => new ModSkillSlot
		{
			Index = index,
			IsPassive = passive,
			SelectedSkill = (passive ? ModCatalog.PassiveSkillChoices : ModCatalog.ActiveSkillChoices).FirstOrDefault(choice => choice.Value == skills[index]),
			Level = Math.Max(1, levels[index]),
		})];

	private static bool SlotsModified(IReadOnlyList<ModSkillSlot> slots, IReadOnlyList<int> skills, IReadOnlyList<int> levels) =>
		slots.Where((slot, index) => (slot.SelectedSkill?.Value ?? 0) != skills[index] || slot.Level != Math.Max(1, levels[index])).Any();
}

internal sealed class ClassEditorState
{
	private readonly IReadOnlyDictionary<int, ClassRecordEdit> mRecords;
	public ClassEditorState()
	{
		mRecords = ModCatalog.Classes.ToDictionary(pair => pair.Key, pair => new ClassRecordEdit(pair.Value));
		SelectedRecord = mRecords.GetValueOrDefault(1) ?? mRecords.Values.First();
	}
	public ClassRecordEdit SelectedRecord { get; private set; }
	public IEnumerable<ClassRecordEdit> ModifiedRecords => mRecords.Values.Where(record => record.IsModified);
	public void Select(int recordId)
	{
		if (mRecords.TryGetValue(recordId, out ClassRecordEdit? record)) SelectedRecord = record;
	}
}

internal sealed class FortRecordEdit
{
	public required ModRecordChoice Choice { get; init; }
	public required int OriginalClassId { get; init; }
	public int ClassId { get; set; }
	public int RecordId => Choice.Value;
	public bool IsModified => ClassId != OriginalClassId;
	public object Snapshot() => new { RecordId, ClassId };
}

internal sealed class FortEditorState
{
	private readonly IReadOnlyDictionary<int, FortRecordEdit> mRecords;
	public FortEditorState()
	{
		mRecords = ModCatalog.FortRecordChoices.ToDictionary(choice => choice.Value, choice => new FortRecordEdit
		{
			Choice = choice,
			OriginalClassId = ModCatalog.FortRecords[choice.Value].ValueA,
			ClassId = ModCatalog.FortRecords[choice.Value].ValueA,
		});
		SelectedLocation = ModCatalog.FortLocations.First();
		SelectedRecord = RecordsAtLocation.First();
	}
	public ModLocationChoice SelectedLocation { get; private set; }
	public FortRecordEdit SelectedRecord { get; private set; }
	public IReadOnlyList<FortRecordEdit> RecordsAtLocation => ModCatalog.FortRecordChoices.Where(choice => choice.LocationKey == SelectedLocation.Key).Select(choice => mRecords[choice.Value]).ToArray();
	public IEnumerable<FortRecordEdit> ModifiedRecords => mRecords.Values.Where(record => record.IsModified);
	public void SelectLocation(ModLocationChoice? location)
	{
		if (location == null || ReferenceEquals(location, SelectedLocation)) return;
		SelectedLocation = location;
		SelectedRecord = RecordsAtLocation.First();
	}
	public void SelectRecord(ModRecordChoice? choice)
	{
		if (choice != null && mRecords.TryGetValue(choice.Value, out FortRecordEdit? record)) SelectedRecord = record;
	}
}

internal sealed class ShopRecordEdit
{
	public required ModRecordChoice Choice { get; init; }
	public required ModShopRecordInfo Original { get; init; }
	public int RecordId => Choice.Value;
	public int ItemId { get; set; }
	public int Stock { get; set; }
	public int Price { get; set; }
	public bool IsModified => ItemId != Original.ItemId || Stock != Original.Stock || Price != Original.Price;
	public object Snapshot() => new { RecordId, ItemId, Stock, Price };
}

internal sealed class ShopEditorState
{
	private readonly IReadOnlyDictionary<int, ShopRecordEdit> mRecords;
	private readonly IReadOnlyDictionary<String, IReadOnlyList<ShopRecordEdit>> mRecordsByLocation;
	public ShopEditorState()
	{
		mRecords = ModCatalog.ShopRecordChoices.ToDictionary(choice => choice.Value, choice =>
		{
			ModShopRecordInfo original = ModCatalog.ShopRecords[choice.Value];
			return new ShopRecordEdit { Choice = choice, Original = original, ItemId = original.ItemId, Stock = original.Stock, Price = original.Price };
		});
		mRecordsByLocation = ModCatalog.ShopRecordChoices
			.GroupBy(choice => choice.LocationKey)
			.ToDictionary(group => group.Key, group => (IReadOnlyList<ShopRecordEdit>)group.Select(choice => mRecords[choice.Value]).ToArray());
		SelectedLocation = ModCatalog.ShopLocations.First();
		SelectedRecord = RecordsAtLocation.First();
	}
	public ModLocationChoice SelectedLocation { get; private set; }
	public ShopRecordEdit SelectedRecord { get; private set; }
	public IReadOnlyList<ShopRecordEdit> RecordsAtLocation => mRecordsByLocation[SelectedLocation.Key];
	public IEnumerable<ShopRecordEdit> ModifiedRecords => mRecords.Values.Where(record => record.IsModified);
	public void SelectLocation(ModLocationChoice? location)
	{
		if (location == null || ReferenceEquals(location, SelectedLocation)) return;
		SelectedLocation = location;
		SelectedRecord = RecordsAtLocation.First();
	}
	public void SelectRecord(ModRecordChoice? choice)
	{
		if (choice != null && mRecords.TryGetValue(choice.Value, out ShopRecordEdit? record)) SelectedRecord = record;
	}
}
