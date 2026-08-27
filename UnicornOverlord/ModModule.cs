using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace UnicornOverlord;

internal sealed record ModCategory(String Name, IReadOnlyList<ModModule> Modules)
{
	public bool IsTextEditor => Modules.Count == 1 && Modules[0].IsTextEditor;
}

internal sealed class ModModule : INotifyPropertyChanged
{
	private bool mIsSelected;
	private int mRecordId = -1;
	private int mValueA;
	private int mValueB;
	private int mValueC;
	private double mValueD;
	private double mValueE;
	private double mValueF;
	private double mValueG;
	private double mValueH;
	private double mValueI;
	private double mValueJ;
	private double mValueK;
	private double mValueL;
	private double mValueM;
	private int mValueN;
	private bool mMixPromotionTiers;

	public event PropertyChangedEventHandler? PropertyChanged;

	public ModModule()
	{
		RerollCommand = new ActionCommand(_ => ValueA = Random.Shared.Next(1, Int32.MaxValue));
		ActiveSkills = CreateSkillSlots(false);
		PassiveSkills = CreateSkillSlots(true);
	}

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
	public bool HasNoOptions => Key == "battle_timer_freeze";
	public IReadOnlyList<String> PreviewModes { get; } = ["完全隐藏", "不完美预览"];
	public IReadOnlyList<double> MatchupValues { get; } = [0.5, 0.75, 1, 1.25, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10];
	public IReadOnlyList<ModChoice> SkillChoices => ModCatalog.SkillChoices;
	public IReadOnlyList<ModChoice> ClassChoices => ModCatalog.ClassChoices;
	public IReadOnlyList<ModChoice> ItemChoices => ModCatalog.ItemChoices;
	public IReadOnlyList<ModLocationChoice> FortLocations => ModCatalog.FortLocations;
	public IReadOnlyList<ModLocationChoice> MineLocations => ModCatalog.MineLocations;
	public IReadOnlyList<ModLocationChoice> ShopLocations => ModCatalog.ShopLocations;
	public IReadOnlyList<ModRecordChoice> FortRecordsAtLocation => FilterRecords(ModCatalog.FortRecordChoices, SelectedFortLocation);
	public IReadOnlyList<ModRecordChoice> MineRecordsAtLocation => FilterRecords(ModCatalog.MineRecordChoices, SelectedMineLocation);
	public IReadOnlyList<ModRecordChoice> ShopRecordsAtLocation => FilterRecords(ModCatalog.ShopRecordChoices, SelectedShopLocation);
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
	public ObservableCollection<ModSkillSlot> ActiveSkills { get; }
	public ObservableCollection<ModSkillSlot> PassiveSkills { get; }
	public ICommand RerollCommand { get; }
	public ModChoice? SelectedSkill { get => ModCatalog.FindSkill(RecordId); set { if (value != null) RecordId = value.Value; } }
	public ModChoice? SelectedClass { get => ModCatalog.FindClass(RecordId); set { if (value != null) RecordId = value.Value; } }
	public ModLocationChoice? SelectedFortLocation { get => FindLocation(ModCatalog.FortLocations, ModCatalog.FindFortRecord(RecordId)); set => SelectFirstRecord(value, ModCatalog.FortRecordChoices); }
	public ModLocationChoice? SelectedMineLocation { get => FindLocation(ModCatalog.MineLocations, ModCatalog.FindMineRecord(RecordId)); set => SelectFirstRecord(value, ModCatalog.MineRecordChoices); }
	public ModLocationChoice? SelectedShopLocation { get => FindLocation(ModCatalog.ShopLocations, ModCatalog.FindShopRecord(RecordId)); set => SelectFirstRecord(value, ModCatalog.ShopRecordChoices); }
	public ModRecordChoice? SelectedFortRecord { get => ModCatalog.FindFortRecord(RecordId); set { if (value != null) RecordId = value.Value; } }
	public ModRecordChoice? SelectedMineRecord { get => ModCatalog.FindMineRecord(RecordId); set { if (value != null) RecordId = value.Value; } }
	public ModRecordChoice? SelectedShopRecord { get => ModCatalog.FindShopRecord(RecordId); set { if (value != null) RecordId = value.Value; } }
	public ModChoice? SelectedFortClass { get => ModCatalog.FindClass(ValueA); set { if (value != null) ValueA = value.Value; } }
	public ModChoice? SelectedMineItem { get => ModCatalog.FindItem(ValueA); set { if (value != null) ValueA = value.Value; } }
	public ModChoice? SelectedShopItem { get => ModCatalog.FindItem(ValueA); set { if (value != null) ValueA = value.Value; } }
	public ModChoice? SelectedTargetShape { get => TargetShapes.FirstOrDefault(choice => choice.Value == ValueC); set { if (value != null) ValueC = value.Value; } }
	public String AbilityTypeText => ModCatalog.Skills.FirstOrDefault(skill => skill.Choice.Value == RecordId)?.TypeText ?? "未知";
	public String PreviewModeDescription => RecordId == 1
		? "后台模拟 5 次战斗并显示平均结果，只给出胜负趋势，不再泄露确定结果。"
		: "完全隐藏战斗预览条；编队与战术判断不再得到结果提示。";
	public bool MixPromotionTiers
	{
		get => mMixPromotionTiers;
		set => SetField(ref mMixPromotionTiers, value, nameof(MixPromotionTiers));
	}

	public int RecordId
	{
		get => mRecordId;
		set
		{
			if (mRecordId == value) return;
			mRecordId = value;
			LoadRecordDefaults();
			Notify(nameof(RecordId), nameof(SelectedSkill), nameof(SelectedClass), nameof(SelectedFortLocation), nameof(SelectedMineLocation), nameof(SelectedShopLocation),
				nameof(FortRecordsAtLocation), nameof(MineRecordsAtLocation), nameof(ShopRecordsAtLocation), nameof(SelectedFortRecord), nameof(SelectedMineRecord), nameof(SelectedShopRecord), nameof(AbilityTypeText), nameof(PreviewModeDescription));
		}
	}
	public int ValueA { get => mValueA; set { SetField(ref mValueA, value, nameof(ValueA)); Notify(nameof(SelectedFortClass), nameof(SelectedMineItem), nameof(SelectedShopItem)); } }
	public int ValueB { get => mValueB; set => SetField(ref mValueB, value, nameof(ValueB)); }
	public int ValueC { get => mValueC; set { SetField(ref mValueC, value, nameof(ValueC)); Notify(nameof(SelectedTargetShape)); } }
	public double ValueD { get => mValueD; set => SetField(ref mValueD, value, nameof(ValueD)); }
	public double ValueE { get => mValueE; set => SetField(ref mValueE, value, nameof(ValueE)); }
	public double ValueF { get => mValueF; set => SetField(ref mValueF, value, nameof(ValueF)); }
	public double ValueG { get => mValueG; set => SetField(ref mValueG, value, nameof(ValueG)); }
	public double ValueH { get => mValueH; set => SetField(ref mValueH, value, nameof(ValueH)); }
	public double ValueI { get => mValueI; set => SetField(ref mValueI, value, nameof(ValueI)); }
	public double ValueJ { get => mValueJ; set => SetField(ref mValueJ, value, nameof(ValueJ)); }
	public double ValueK { get => mValueK; set => SetField(ref mValueK, value, nameof(ValueK)); }
	public double ValueL { get => mValueL; set => SetField(ref mValueL, value, nameof(ValueL)); }
	public double ValueM { get => mValueM; set => SetField(ref mValueM, value, nameof(ValueM)); }
	public int ValueN { get => mValueN; set => SetField(ref mValueN, value, nameof(ValueN)); }

	public bool IsSelected
	{
		get => mIsSelected;
		set
		{
			if (!IsAvailable || mIsSelected == value) return;
			mIsSelected = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
		}
	}

	private void SetField<T>(ref T field, T value, String propertyName)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return;
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public void RefreshLocalizedChoices()
	{
		Notify(nameof(SkillChoices), nameof(ClassChoices), nameof(ItemChoices), nameof(FortLocations), nameof(MineLocations), nameof(ShopLocations), nameof(FortRecordsAtLocation), nameof(MineRecordsAtLocation), nameof(ShopRecordsAtLocation),
			nameof(SelectedSkill), nameof(SelectedClass), nameof(SelectedFortLocation), nameof(SelectedMineLocation), nameof(SelectedShopLocation), nameof(SelectedFortRecord), nameof(SelectedMineRecord), nameof(SelectedShopRecord),
			nameof(SelectedFortClass), nameof(SelectedMineItem), nameof(SelectedShopItem), nameof(TargetShapes), nameof(SelectedTargetShape));
		foreach (ModSkillSlot slot in ActiveSkills.Concat(PassiveSkills))
		{
			ModChoice? choice = slot.SelectedSkill;
			slot.SelectedSkill = null;
			slot.SelectedSkill = choice;
		}
	}

	private void LoadRecordDefaults()
	{
		if (IsAbilityEditor)
		{
			ModSkillInfo? skill = ModCatalog.Skills.FirstOrDefault(item => item.Choice.Value == RecordId);
			if (skill == null) return;
			mValueN = skill.IsPassive ? 1 : 0;
			ValueA = skill.Cost;
			ValueB = skill.Accuracy;
			ValueC = skill.TargetShape;
			ValueD = skill.PhysicalPotency;
			ValueE = skill.MagicalPotency;
			ValueF = skill.EffectValue;
			return;
		}
		if (IsClassEditor && ModCatalog.Classes.TryGetValue(RecordId, out ModClassInfo? classInfo))
		{
			ValueA = classInfo.Ap;
			ValueB = classInfo.Pp;
			ValueD = classInfo.Growths[0]; ValueE = classInfo.Growths[1]; ValueF = classInfo.Growths[2]; ValueG = classInfo.Growths[3]; ValueH = classInfo.Growths[4];
			ValueI = classInfo.Growths[5]; ValueJ = classInfo.Growths[6]; ValueK = classInfo.Growths[7]; ValueL = classInfo.Growths[8]; ValueM = classInfo.Growths[9];
			ApplySkillDefaults(ActiveSkills, classInfo.ActiveSkills, classInfo.ActiveLevels);
			ApplySkillDefaults(PassiveSkills, classInfo.PassiveSkills, classInfo.PassiveLevels);
			return;
		}
		if (IsFortEditor && ModCatalog.FortRecords.TryGetValue(RecordId, out ModRecordInfo? fort)) ValueA = fort.ValueA;
		if (IsMineEditor && ModCatalog.MineRecords.TryGetValue(RecordId, out ModRecordInfo? mine))
		{
			ValueA = mine.ValueA; ValueB = mine.ValueB; ValueC = mine.ValueC; ValueD = mine.ValueE;
		}
		if (IsShopEditor && ModCatalog.ShopRecords.TryGetValue(RecordId, out ModShopRecordInfo? shop))
		{
			ValueA = shop.ItemId; ValueB = shop.Stock; ValueC = shop.Price;
		}
	}

	private static ModLocationChoice? FindLocation(IReadOnlyList<ModLocationChoice> locations, ModRecordChoice? record) =>
		record == null ? null : locations.FirstOrDefault(location => location.Key == record.LocationKey);

	private void SelectFirstRecord(ModLocationChoice? location, IReadOnlyList<ModRecordChoice> records)
	{
		ModRecordChoice? first = location == null ? null : records.FirstOrDefault(record => record.LocationKey == location.Key);
		if (first != null) RecordId = first.Value;
	}

	private static IReadOnlyList<ModRecordChoice> FilterRecords(IReadOnlyList<ModRecordChoice> records, ModLocationChoice? location) =>
		location == null ? [] : records.Where(record => record.LocationKey == location.Key).ToArray();

	private static ObservableCollection<ModSkillSlot> CreateSkillSlots(bool passive) =>
		[.. Enumerable.Range(0, 4).Select(index => new ModSkillSlot { Index = index, IsPassive = passive, Level = 1 })];

	private static void ApplySkillDefaults(IReadOnlyList<ModSkillSlot> slots, IReadOnlyList<int> skills, IReadOnlyList<int> levels)
	{
		for (int index = 0; index < slots.Count; index++)
		{
			slots[index].SelectedSkill = (slots[index].IsPassive ? ModCatalog.PassiveSkillChoices : ModCatalog.ActiveSkillChoices)
				.FirstOrDefault(choice => choice.Value == skills[index]);
			slots[index].Level = Math.Max(1, levels[index]);
		}
	}

	private void Notify(params String[] propertyNames)
	{
		foreach (String propertyName in propertyNames) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
