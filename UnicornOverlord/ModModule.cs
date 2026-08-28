using System.ComponentModel;
using System.Windows.Input;

namespace UnicornOverlord;

internal sealed record ModCategory(String Name, IReadOnlyList<ModModule> Modules)
{
	public bool IsTextEditor => Modules.Count == 1 && Modules[0].IsTextEditor;
}

internal sealed class ModModule : INotifyPropertyChanged
{
	public ModModule() => RerollCommand = new ActionCommand(_ => ValueA = Random.Shared.Next(1, Int32.MaxValue));

	public event PropertyChangedEventHandler? PropertyChanged;
	public required ModProjectState Project { get; init; }
	public required String Key { get; init; }
	public required String Category { get; init; }
	public required String Name { get; init; }
	public required String Description { get; init; }
	public required bool IsAvailable { get; init; }
	public String? TemplateFile { get; init; }
	public String? Warning { get; init; }
	public String? CalibrationState { get; init; }
	public String StateText => CalibrationState ?? (IsAvailable ? "已接入" : "待解析");
	public bool IsAbilityEditor => Key == "ability_editor";
	public bool IsBattlePreview => Key == "battle_preview";
	public bool IsCharacterRandomizer => Key == "character_randomizer";
	public bool IsClassEditor => Key == "class_editor";
	public bool IsFortEditor => Key == "fort_editor";
	public bool IsMineEditor => Key == "mine_editor";
	public bool IsShopEditor => Key == "shop_editor";
	public bool IsSixMemberUnits => Key == "six_member_units";
	public bool IsTypeMatchups => Key == "type_matchups";
	public bool IsTextEditor => Key == "text_editor";
	public bool ShowHeaderEnableToggle => !IsTextEditor;
	public bool ShowContentSeparator => Key is "battle_preview" or "battle_timer_freeze";
	public bool HasNoOptions => Key == "battle_timer_freeze";
	public IReadOnlyList<String> PreviewModes { get; } = ["完全隐藏", "不完美预览"];
	public IReadOnlyList<String> AbilityFilters { get; } = ["全部技能", "主动技能（AP）", "被动技能（PP）"];
	public IReadOnlyList<double> MatchupValues { get; } = [0.5, 0.75, 1, 1.25, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10];
	public IReadOnlyList<ModChoice> SkillChoices => ModCatalog.SkillChoices;
	public IReadOnlyList<ModChoice> FilteredSkillChoices => AbilityFilterIndex switch
	{
		1 => ModCatalog.ActiveSkillChoicesWithoutEmpty,
		2 => ModCatalog.PassiveSkillChoicesWithoutEmpty,
		_ => ModCatalog.SkillChoices,
	};
	public IReadOnlyList<ModChoice> ClassChoices => ModCatalog.ClassChoices;
	public IReadOnlyList<ModChoice> ItemChoices => ModCatalog.ItemChoices;
	public IReadOnlyList<ModLocationChoice> FortLocations => ModCatalog.FortLocations;
	public IReadOnlyList<ModLocationChoice> ShopLocations => ModCatalog.ShopLocations;
	public IReadOnlyList<ModRecordChoice> FortRecordsAtLocation => Project.Fort.RecordsAtLocation.Select(record => record.Choice).ToArray();
	public IReadOnlyList<ModRecordChoice> ShopRecordsAtLocation => Project.Shop.RecordsAtLocation.Select(record => record.Choice).ToArray();
	public IReadOnlyList<ModChoice> TargetShapes { get; } =
	[
		new() { Value = 0, EnglishName = "Original/none", ChineseName = "原始/无目标" },
		new() { Value = 1, EnglishName = "Single", ChineseName = "单体" },
		new() { Value = 2, EnglishName = "Double", ChineseName = "2 个目标" },
		new() { Value = 3, EnglishName = "Triple", ChineseName = "3 个目标" },
		new() { Value = 5, EnglishName = "All", ChineseName = "全体" },
		new() { Value = 6, EnglishName = "Row", ChineseName = "一排" },
		new() { Value = 7, EnglishName = "Front-back", ChineseName = "前后纵列" },
	];
	public IReadOnlyList<ModSkillSlot> ActiveSkills => Project.Classes.SelectedRecord.ActiveSkills;
	public IReadOnlyList<ModSkillSlot> PassiveSkills => Project.Classes.SelectedRecord.PassiveSkills;
	public MineEditorState Mine => Project.Mine;
	public ICommand RerollCommand { get; }

	public bool IsSelected
	{
		get => Options.IsEnabled;
		set
		{
			if (!IsAvailable || Options.IsEnabled == value) return;
			Options.IsEnabled = value;
			Notify(nameof(IsSelected));
		}
	}

	public int AbilityFilterIndex
	{
		get => Project.Ability.FilterIndex;
		set
		{
			int normalized = Math.Clamp(value, 0, AbilityFilters.Count - 1);
			if (Project.Ability.FilterIndex == normalized) return;
			Project.Ability.FilterIndex = normalized;
			if (IsAbilityEditor && !FilteredSkillChoices.Any(choice => choice.Value == RecordId) && FilteredSkillChoices.FirstOrDefault() is ModChoice first)
				Project.Ability.Select(first.Value);
			NotifyEditor();
		}
	}

	public int RecordId
	{
		get => Key switch
		{
			"ability_editor" => Project.Ability.SelectedRecord.RecordId,
			"class_editor" => Project.Classes.SelectedRecord.RecordId,
			"fort_editor" => Project.Fort.SelectedRecord.RecordId,
			"shop_editor" => Project.Shop.SelectedRecord.RecordId,
			_ => IsBattlePreview ? Project.BattlePreview.Mode : -1,
		};
		set
		{
			if (RecordId == value) return;
			switch (Key)
			{
				case "ability_editor": Project.Ability.Select(value); break;
				case "class_editor": Project.Classes.Select(value); break;
				case "fort_editor": Project.Fort.SelectRecord(ModCatalog.FindFortRecord(value)); break;
				case "shop_editor": Project.Shop.SelectRecord(ModCatalog.FindShopRecord(value)); break;
				default: if (IsBattlePreview) Project.BattlePreview.Mode = value; break;
			}
			NotifyEditor();
		}
	}

	public int ValueA
	{
		get => Key switch { "ability_editor" => Project.Ability.SelectedRecord.Cost, "class_editor" => Project.Classes.SelectedRecord.Ap, "fort_editor" => Project.Fort.SelectedRecord.ClassId, "shop_editor" => Project.Shop.SelectedRecord.ItemId, "character_randomizer" => Project.CharacterRandomizer.Seed, "six_member_units" => Project.SixMemberUnits.HonorCost, _ => 0 };
		set { switch (Key) { case "ability_editor": Project.Ability.SelectedRecord.Cost = value; break; case "class_editor": Project.Classes.SelectedRecord.Ap = value; break; case "fort_editor": Project.Fort.SelectedRecord.ClassId = value; break; case "shop_editor": Project.Shop.SelectedRecord.ItemId = value; break; case "character_randomizer": Project.CharacterRandomizer.Seed = value; break; case "six_member_units": Project.SixMemberUnits.HonorCost = value; break; } Notify(nameof(ValueA), nameof(SelectedFortClass), nameof(SelectedShopItem)); }
	}
	public int ValueB
	{
		get => Key switch { "ability_editor" => Project.Ability.SelectedRecord.Accuracy, "class_editor" => Project.Classes.SelectedRecord.Pp, "shop_editor" => Project.Shop.SelectedRecord.Stock, _ => 0 };
		set { switch (Key) { case "ability_editor": Project.Ability.SelectedRecord.Accuracy = value; break; case "class_editor": Project.Classes.SelectedRecord.Pp = value; break; case "shop_editor": Project.Shop.SelectedRecord.Stock = value; break; } Notify(nameof(ValueB)); }
	}
	public int ValueC
	{
		get => Key switch { "ability_editor" => Project.Ability.SelectedRecord.TargetShape, "shop_editor" => Project.Shop.SelectedRecord.Price, _ => 0 };
		set { switch (Key) { case "ability_editor": Project.Ability.SelectedRecord.TargetShape = value; break; case "shop_editor": Project.Shop.SelectedRecord.Price = value; break; } Notify(nameof(ValueC), nameof(SelectedTargetShape)); }
	}
	public double ValueD { get => ReadDouble(0); set => WriteDouble(0, value, nameof(ValueD)); }
	public double ValueE { get => ReadDouble(1); set => WriteDouble(1, value, nameof(ValueE)); }
	public double ValueF { get => ReadDouble(2); set => WriteDouble(2, value, nameof(ValueF)); }
	public double ValueG { get => ReadDouble(3); set => WriteDouble(3, value, nameof(ValueG)); }
	public double ValueH { get => ReadDouble(4); set => WriteDouble(4, value, nameof(ValueH)); }
	public double ValueI { get => ReadDouble(5); set => WriteDouble(5, value, nameof(ValueI)); }
	public double ValueJ { get => ReadDouble(6); set => WriteDouble(6, value, nameof(ValueJ)); }
	public double ValueK { get => ReadDouble(7); set => WriteDouble(7, value, nameof(ValueK)); }
	public double ValueL { get => ReadDouble(8); set => WriteDouble(8, value, nameof(ValueL)); }
	public double ValueM { get => ReadDouble(9); set => WriteDouble(9, value, nameof(ValueM)); }
	public int ValueN { get => IsAbilityEditor && Project.Ability.SelectedRecord.Original.IsPassive ? 1 : 0; set => Notify(nameof(ValueN)); }
	public bool MixPromotionTiers { get => Project.CharacterRandomizer.MixPromotionTiers; set { Project.CharacterRandomizer.MixPromotionTiers = value; Notify(nameof(MixPromotionTiers)); } }

	public ModChoice? SelectedSkill { get => Project.Ability.SelectedRecord.Original.Choice; set { if (value != null) RecordId = value.Value; } }
	public ModChoice? SelectedClass { get => ModCatalog.FindClass(Project.Classes.SelectedRecord.RecordId); set { if (value != null) RecordId = value.Value; } }
	public ModLocationChoice SelectedFortLocation { get => Project.Fort.SelectedLocation; set { Project.Fort.SelectLocation(value); NotifyEditor(); } }
	public ModLocationChoice SelectedShopLocation { get => Project.Shop.SelectedLocation; set { Project.Shop.SelectLocation(value); NotifyEditor(); } }
	public ModRecordChoice SelectedFortRecord { get => Project.Fort.SelectedRecord.Choice; set { Project.Fort.SelectRecord(value); NotifyEditor(); } }
	public ModRecordChoice SelectedShopRecord { get => Project.Shop.SelectedRecord.Choice; set { Project.Shop.SelectRecord(value); NotifyEditor(); } }
	public ModChoice? SelectedFortClass { get => ModCatalog.FindClass(Project.Fort.SelectedRecord.ClassId); set { if (value != null) ValueA = value.Value; } }
	public ModChoice? SelectedShopItem { get => ModCatalog.FindItem(Project.Shop.SelectedRecord.ItemId); set { if (value != null) ValueA = value.Value; } }
	public ModChoice? SelectedTargetShape { get => TargetShapes.FirstOrDefault(choice => choice.Value == ValueC); set { if (value != null) ValueC = value.Value; } }
	public String AbilityTypeText => Project.Ability.SelectedRecord.Original.TypeText;
	public bool IsActiveAbility => !Project.Ability.SelectedRecord.Original.IsPassive;
	public bool IsPassiveAbility => Project.Ability.SelectedRecord.Original.IsPassive;
	public String AbilityDescription => Project.Ability.SelectedRecord.Original.Description;
	public String PreviewModeDescription => RecordId == 1
		? "后台模拟 5 次战斗并显示平均结果，只给出胜负趋势，不再泄露确定结果。"
		: "完全隐藏战斗预览条；编队与战术判断不再得到结果提示。";

	public void RefreshLocalizedChoices()
	{
		Project.Mine.RefreshLocalizedChoices();
		NotifyEditor();
	}

	private ModOptionState Options => Project.Options(Key);
	private double ReadDouble(int index)
	{
		if (IsAbilityEditor) return index switch { 0 => Project.Ability.SelectedRecord.PhysicalPotency, 1 => Project.Ability.SelectedRecord.MagicalPotency, 2 => Project.Ability.SelectedRecord.EffectValue, _ => 0 };
		if (IsClassEditor) return Project.Classes.SelectedRecord.Growths[index];
		if (IsTypeMatchups) return index switch { 0 => Project.TypeMatchups.CavalryVsInfantry, 1 => Project.TypeMatchups.ArcherVsFlying, 2 => Project.TypeMatchups.FlyingVsCavalry, _ => 0 };
		return 0;
	}

	private void WriteDouble(int index, double value, String propertyName)
	{
		if (IsAbilityEditor)
		{
			switch (index) { case 0: Project.Ability.SelectedRecord.PhysicalPotency = value; break; case 1: Project.Ability.SelectedRecord.MagicalPotency = value; break; case 2: Project.Ability.SelectedRecord.EffectValue = value; break; }
		}
		else if (IsClassEditor) Project.Classes.SelectedRecord.Growths[index] = value;
		else if (IsTypeMatchups)
		{
			switch (index) { case 0: Project.TypeMatchups.CavalryVsInfantry = value; break; case 1: Project.TypeMatchups.ArcherVsFlying = value; break; case 2: Project.TypeMatchups.FlyingVsCavalry = value; break; }
		}
		Notify(propertyName);
	}

	private void NotifyEditor() => Notify(nameof(RecordId), nameof(AbilityFilterIndex), nameof(FilteredSkillChoices), nameof(SelectedSkill), nameof(SelectedClass),
		nameof(SelectedFortLocation), nameof(SelectedShopLocation), nameof(FortRecordsAtLocation), nameof(ShopRecordsAtLocation), nameof(SelectedFortRecord), nameof(SelectedShopRecord),
		nameof(SelectedFortClass), nameof(SelectedShopItem), nameof(SelectedTargetShape), nameof(AbilityTypeText), nameof(IsActiveAbility), nameof(IsPassiveAbility), nameof(AbilityDescription),
		nameof(PreviewModeDescription), nameof(ValueA), nameof(ValueB), nameof(ValueC), nameof(ValueD), nameof(ValueE), nameof(ValueF), nameof(ValueG), nameof(ValueH), nameof(ValueI),
		nameof(ValueJ), nameof(ValueK), nameof(ValueL), nameof(ValueM), nameof(ValueN), nameof(ActiveSkills), nameof(PassiveSkills));

	private void Notify(params String[] propertyNames)
	{
		foreach (String propertyName in propertyNames) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
