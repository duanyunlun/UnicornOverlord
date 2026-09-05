using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UnicornOverlord;

internal sealed class ModProjectState
{
	private readonly Dictionary<String, ModOptionState> mOptions = new(StringComparer.Ordinal);
	private MissionEditorState? mMissions;

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
	public double ExperienceMultiplier { get; set; } = 1;
	public MissionEditorState Missions => mMissions ??= new MissionEditorState(this);
	public JsonObject MissionEdits => mMissions?.Edits ?? new JsonObject();

	public void ImportMissionClassEdits(JsonArray entries)
	{
		foreach (JsonObject entry in entries.OfType<JsonObject>())
		{
			ClassRecordEdit record = Classes.GetRecord(MissionModCatalog.Number(entry, "class_id")) ?? throw new InvalidDataException("不支持的职业 ID。");
			foreach (ModSkillSlot slot in record.ActiveSkills.Concat(record.PassiveSkills)) slot.SelectedSkill = slot.Choices.First(choice => choice.Value == 0);
			foreach (JsonObject line in entry["lines"]?.AsArray().OfType<JsonObject>() ?? [])
			{
				int action = MissionModCatalog.Number(line, "action");
				if (action is < 3 or > 10) throw new InvalidDataException("职业战术必须引用四个主动或四个被动槽。");
				ModSkillSlot slot = action < 7 ? record.ActiveSkills[action - 3] : record.PassiveSkills[action - 7];
				int skill = MissionModCatalog.Number(line, "skill_id");
				slot.SelectedSkill = slot.Choices.FirstOrDefault(choice => choice.Value == skill) ?? throw new InvalidDataException("职业技能类型不匹配。");
				slot.Level = slot.IsFirst ? 1 : MissionModCatalog.Number(line, "learn_level", 1);
				if (skill != 0) Classes.Conditions.Set(skill, MissionModCatalog.Number(line, "if0"), MissionModCatalog.Number(line, "if1"));
			}
		}
	}

	public JsonArray ExportMissionClassEdits()
	{
		var entries = new JsonArray();
		foreach (ClassRecordEdit record in Classes.Records.Where(record => record.IsModified || record.ActiveSkills.Concat(record.PassiveSkills).Any(slot => Classes.Conditions.ModifiedRecords.ContainsKey(slot.SelectedSkill?.Value ?? 0))))
		{
			var lines = new JsonArray();
			foreach (ModSkillSlot slot in record.ActiveSkills.Concat(record.PassiveSkills).Where(slot => (slot.SelectedSkill?.Value ?? 0) > 0))
			{
				int skill = slot.SelectedSkill!.Value;
				var conditions = Classes.Conditions.Get(skill);
				lines.Add(new JsonObject { ["action"] = (slot.IsPassive ? 7 : 3) + slot.Index, ["skill_id"] = skill, ["learn_level"] = slot.Level, ["if0"] = conditions.First, ["if1"] = conditions.Second });
			}
			entries.Add(new JsonObject { ["class_id"] = record.RecordId, ["lines"] = lines });
		}
		return entries;
	}

	public JsonObject ExportMissionEdits(bool includeClasses = true, bool includeMissions = true)
	{
		var edits = includeMissions ? (JsonObject)MissionEdits.DeepClone() : new JsonObject();
		edits.Remove("class_tactics");
		edits.Remove("equiptype_items");
		if (includeClasses)
		{
			edits["class_tactics"] = ExportMissionClassEdits();
			if (MissionEdits["equiptype_items"] is JsonArray gear) edits["equiptype_items"] = gear.DeepClone();
		}
		return edits;
	}

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
		"class_editor" => new { module.Key, records = Classes.ModifiedRecords.Select(record => record.Snapshot()).ToArray(), class_tactics = ExportMissionClassEdits(), equiptype_items = MissionEdits["equiptype_items"] },
		"mission_editor" => new { module.Key, edits = MissionEdits },
		"experience_scale" => new { module.Key, multiplier = ExperienceMultiplier },
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
		foreach (ModSkillSlot slot in mRecords.Values.SelectMany(record => record.ActiveSkills.Concat(record.PassiveSkills))) slot.BindConditions(Conditions);
		SelectedRecord = mRecords.GetValueOrDefault(1) ?? mRecords.Values.First();
	}
	public ClassRecordEdit SelectedRecord { get; private set; }
	public SkillConditionState Conditions { get; } = new();
	public IEnumerable<ClassRecordEdit> Records => mRecords.Values;
	public ClassRecordEdit? GetRecord(int id) => mRecords.GetValueOrDefault(id);
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

internal sealed class FortLocationState : ObservableObject, ILocationState<FortRecordEdit>
{
	public required ModLocationChoice Choice { get; init; }
	public required IReadOnlyList<FortRecordEdit> Records { get; init; }
	public String DisplayName => Choice.DisplayName;
	public void RefreshLocalizedName() => Notify(nameof(DisplayName));
}

internal sealed class FortEditorState : LocationEditorState<FortLocationState, FortRecordEdit>
{
	private static readonly String[] SelectionProperties = [nameof(SelectedClass), nameof(ClassId)];

	public FortEditorState() : base(CreateLocations()) { }

	public IEnumerable<FortRecordEdit> ModifiedRecords => Locations.SelectMany(location => location.Records).Where(record => record.IsModified);

	public int ClassId
	{
		get => SelectedRecord.ClassId;
		set
		{
			if (SelectedRecord.ClassId == value) return;
			SelectedRecord.ClassId = value;
			Notify(nameof(ClassId), nameof(SelectedClass));
		}
	}

	public ModChoice? SelectedClass
	{
		get => ModCatalog.FindClass(ClassId);
		set { if (value != null) ClassId = value.Value; }
	}

	protected override int RecordId(FortRecordEdit record) => record.RecordId;
	protected override String[] SelectedRecordPropertyNames => SelectionProperties;
	protected override String[] LocalizedPropertyNames => [nameof(SelectedClass)];

	private static IReadOnlyList<FortLocationState> CreateLocations()
	{
		var edits = ModCatalog.FortRecordChoices.ToDictionary(choice => choice.Value, choice => new FortRecordEdit
		{
			Choice = choice,
			OriginalClassId = ModCatalog.FortRecords[choice.Value].ValueA,
			ClassId = ModCatalog.FortRecords[choice.Value].ValueA,
		});
		return ModCatalog.FortLocations.Select(location => new FortLocationState
		{
			Choice = location,
			Records = ModCatalog.FortRecordChoices.Where(record => record.LocationKey == location.Key).Select(record => edits[record.Value]).ToArray(),
		}).ToArray();
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

internal sealed class ShopLocationState : ObservableObject, ILocationState<ShopRecordEdit>
{
	public required ModLocationChoice Choice { get; init; }
	public required IReadOnlyList<ShopRecordEdit> Records { get; init; }
	public String DisplayName => Choice.DisplayName;
	public void RefreshLocalizedName() => Notify(nameof(DisplayName));
}

internal sealed class ShopEditorState : LocationEditorState<ShopLocationState, ShopRecordEdit>
{
	private static readonly String[] SelectionProperties = [nameof(SelectedItem), nameof(ItemId), nameof(Stock), nameof(Price)];

	public ShopEditorState() : base(CreateLocations()) { }

	public IEnumerable<ShopRecordEdit> ModifiedRecords => Locations.SelectMany(location => location.Records).Where(record => record.IsModified);

	public int ItemId
	{
		get => SelectedRecord.ItemId;
		set
		{
			if (SelectedRecord.ItemId == value) return;
			SelectedRecord.ItemId = value;
			Notify(nameof(ItemId), nameof(SelectedItem));
		}
	}

	public ModChoice? SelectedItem
	{
		get => ModCatalog.FindItem(ItemId);
		set { if (value != null) ItemId = value.Value; }
	}

	public int Stock
	{
		get => SelectedRecord.Stock;
		set { if (SelectedRecord.Stock != value) { SelectedRecord.Stock = value; Notify(nameof(Stock)); } }
	}

	public int Price
	{
		get => SelectedRecord.Price;
		set { if (SelectedRecord.Price != value) { SelectedRecord.Price = value; Notify(nameof(Price)); } }
	}

	protected override int RecordId(ShopRecordEdit record) => record.RecordId;
	protected override String[] SelectedRecordPropertyNames => SelectionProperties;
	protected override String[] LocalizedPropertyNames => [nameof(SelectedItem)];

	private static IReadOnlyList<ShopLocationState> CreateLocations()
	{
		var edits = ModCatalog.ShopRecordChoices.ToDictionary(choice => choice.Value, choice =>
		{
			ModShopRecordInfo original = ModCatalog.ShopRecords[choice.Value];
			return new ShopRecordEdit { Choice = choice, Original = original, ItemId = original.ItemId, Stock = original.Stock, Price = original.Price };
		});
		return ModCatalog.ShopLocations.Select(location => new ShopLocationState
		{
			Choice = location,
			Records = ModCatalog.ShopRecordChoices.Where(record => record.LocationKey == location.Key).Select(record => edits[record.Value]).ToArray(),
		}).ToArray();
	}
}
