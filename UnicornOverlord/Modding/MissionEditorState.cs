using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace UnicornOverlord;

internal sealed class MissionEditorState : ObservableObject
{
	private readonly ModProjectState mProject;
	private ModChoice? mMission;
	private ModChoice? mSquad;
	private ModChoice? mPreset;
	private String mRegion = "全部";
	private String mSide = "全部";
	private String mQuery = String.Empty;
	private String mMissionSort = "原始顺序";
	private bool mPresetsForMission;
	private readonly Dictionary<int, ModChoice> mCreatedChoices = [];
	private int mSlot;
	private int? mExchangeSlot;
	private String mStatus = String.Empty;
	private readonly List<ModChoice> mMissions;
	private readonly List<ModChoice> mSquads;
	private readonly List<ModChoice> mPresets;
	private static readonly String[] ArrayKeys = ["unitsets", "charasets", "equipaiset_allocations", "equipaiset_creates", "class_tactics", "equiptype_items", "class_equiptypes"];

	public MissionEditorState(ModProjectState project)
	{
		mProject = project;
		mMissions = MissionModCatalog.Rows("missions").Select(row => Choice(N(row, "quest_id"), $"{N(row, "quest_id")} · {T(row, "stage_name")} · {T(row, "quest_symbol")}")).ToList();
		mSquads = MissionModCatalog.Rows("missions").SelectMany(row => Rows(row, "squads")).DistinctBy(row => N(row, "unitset_id"))
			.Select(row => Choice(N(row, "unitset_id"), $"{N(row, "unitset_id")} · {T(row, "side")} · {T(row, "unitset_symbol")}")).ToList();
		mPresets = [Choice(0, "0 · 职业默认 + 装备技能"), .. MissionModCatalog.Rows("equipaiset_presets").Where(row => N(row, "id") != 0).Select(row => Choice(N(row, "id"), $"{N(row, "id")} · {T(row, "symbol")}"))];
		CharacterChoices = [Choice(0, "0 · 空槽"), .. MissionModCatalog.Rows("charasets").Select(row => Choice(N(row, "id"), $"{N(row, "id")} · {T(row, "name")} · {T(row, "symbol")} · {T(row, "class_name")}"))];
		ItemChoices = [Choice(0, "0 · 无装备 / 默认装备"), .. MissionModCatalog.Rows("items").Where(row => N(row, "id") != 0).Select(row => ModCatalog.FindItem(N(row, "id")) ?? Choice(N(row, "id"), $"{N(row, "id")} · {T(row, "name", T(row, "symbol"))}"))];
		SkillChoices = [.. Enumerable.Range(3, 8).Select(id => Choice(id, $"{id} · 职业{(id < 7 ? "主动" : "被动")}槽 {(id < 7 ? id - 2 : id - 6)}")), .. MissionModCatalog.Rows("skills").Where(row => N(row, "id") > 10).Select(row => ModCatalog.FindSkill(N(row, "id")) ?? Choice(N(row, "id"), $"{N(row, "id")} · {T(row, "name", T(row, "symbol"))}"))];
		IfChoices = MissionModCatalog.Rows("equipai_if").Select(row => Choice(N(row, "id"), $"{N(row, "id")} · {T(row, "name", T(row, "symbol"))}")).ToArray();
		SelectSlotCommand = new(parameter => { if (Int32.TryParse(parameter?.ToString(), out int slot)) SelectSlot(slot); });
		ExchangeCommand = new(_ => { mExchangeSlot = mExchangeSlot.HasValue ? null : mSlot; Changed(); });
		RestoreGearCommand = new(_ => { Remove("charasets", "charaset_id", N(CurrentSlot, "charaset_id")); RebuildSlot(); });
		NewPresetCommand = new(_ => CreatePreset(false));
		CopyPrivateCommand = new(_ => CreatePreset(true));
		AssignPresetCommand = new(_ => { if (mPreset != null && N(CurrentSlot, "charaset_id") != 0) { var slot = EditableSlot(); slot["equipaiset_id"] = mPreset.Value; slot.Remove("use_duplicate"); slot.Remove("equipaiset_alloc_key"); RebuildSlot(); } });
		AddLineCommand = new(_ => { if (!CanEditPreset) return; var lines = PresetLines(); if (lines.Count >= 8) { Status = "预设最多 8 行，不能静默截断。"; return; } lines.Add(new JsonObject { ["slot"] = lines.Count, ["action"] = 3, ["skill_id"] = 3, ["ref_kind"] = "class_slot", ["if0"] = 0, ["if1"] = 0 }); CommitLines(lines); });
		ResetCommand = new(_ => Reset());
		RefreshPreviewCommand = new(_ => RebuildSlot());
		Reset();
		mProject.Classes.Conditions.PropertyChanged += (_, _) => Notify(nameof(FinalTactics));
		foreach (var record in mProject.Classes.Records)
			foreach (var slot in record.ActiveSkills.Concat(record.PassiveSkills)) slot.PropertyChanged += (_, _) => Notify(nameof(FinalTactics));
	}

	public JsonObject Edits { get; } = new();
	public JsonObject ExportEdits() => mProject.ExportMissionEdits();
	public IReadOnlyList<ModChoice> CharacterChoices { get; }
	public IReadOnlyList<ModChoice> ItemChoices { get; }
	public IReadOnlyList<ModChoice> SkillChoices { get; }
	public IReadOnlyList<ModChoice> IfChoices { get; }
	public IEnumerable<String> Regions => new[] { "全部" }.Concat(MissionModCatalog.Rows("missions").Select(row => T(row, "region")).Distinct());
	public IEnumerable<String> Sides => new[] { "全部" }.Concat(Rows(Mission, "squads").Select(row => T(row, "side")).Distinct());
	public IReadOnlyList<String> MissionSortChoices { get; } = ["原始顺序", "等级升序", "等级降序"];
	public String MissionSort { get => mMissionSort; set => SetField(ref mMissionSort, value, nameof(MissionSort), nameof(Missions)); }
	public IEnumerable<ModChoice> Missions
	{
		get
		{
			var filtered = mMissions.Where(choice => MissionModCatalog.Rows("missions").Any(row => N(row, "quest_id") == choice.Value && (Region == "全部" || T(row, "region") == Region)) && (String.IsNullOrWhiteSpace(Query) || choice.Name.Contains(Query, StringComparison.OrdinalIgnoreCase)));
			return MissionSort == "原始顺序" ? filtered : filtered.OrderBy(choice => Int32.TryParse(T(Find(MissionModCatalog.Root, "missions", "quest_id", choice.Value), "enemy_level"), out int level) ? MissionSort == "等级升序" ? level : -level : Int32.MaxValue);
		}
	}
	public IEnumerable<ModChoice> Squads => mSquads.Where(choice => Rows(Mission, "squads").Any(row => N(row, "unitset_id") == choice.Value && (Side == "全部" || T(row, "side") == Side)));
	public IEnumerable<ModChoice> Presets => mPresets.Concat(Rows(Edits, "equipaiset_creates").Select(row =>
	{
		int id = N(row, "temp_id");
		String name = $"{id} · {T(row, "symbol")}（导出时分配）";
		if (!mCreatedChoices.TryGetValue(id, out var choice) || choice.Name != name) mCreatedChoices[id] = choice = Choice(id, name);
		return choice;
	}));
	public bool PresetsForMission { get => mPresetsForMission; set => SetField(ref mPresetsForMission, value, nameof(PresetsForMission), nameof(FilteredPresets)); }
	public IEnumerable<ModChoice> FilteredPresets
	{
		get
		{
			if (!PresetsForMission) return Presets;
			var used = Rows(Mission, "squads").SelectMany(squad => Enumerable.Range(0, 6).Select(position => EffectiveSlot(squad, position))).Where(slot => N(slot, "charaset_id") != 0).Select(slot => N(slot, "equipaiset_id")).ToHashSet();
			return Presets.Where(choice => used.Contains(choice.Value));
		}
	}
	public bool CanRenamePreset => mPreset?.Value < 0;
	public String PresetName
	{
		get => T(CanRenamePreset ? Find(Edits, "equipaiset_creates", "temp_id", mPreset!.Value) : Find(MissionModCatalog.Root, "equipaiset_presets", "id", mPreset?.Value ?? 0), "symbol");
		set
		{
			if (!CanRenamePreset || String.IsNullOrWhiteSpace(value) || value.Length > 96) return;
			int id = mPreset!.Value;
			Find(Edits, "equipaiset_creates", "temp_id", id)!["symbol"] = value;
			mPreset = Presets.First(choice => choice.Value == id);
			Notify(nameof(Edits), nameof(Presets), nameof(FilteredPresets), nameof(SelectedPreset), nameof(PresetName));
		}
	}
	public ObservableCollection<MissionSeat> Seats { get; } = [];
	public ObservableCollection<MissionGearSlot> Gear { get; } = [];
	public ObservableCollection<MissionTacticLine> Lines { get; } = [];
	public ObservableCollection<MissionDefaultGearRow> DefaultGearRows { get; } = [];
	public ActionCommand SelectSlotCommand { get; }
	public ActionCommand ExchangeCommand { get; }
	public ActionCommand RestoreGearCommand { get; }
	public ActionCommand NewPresetCommand { get; }
	public ActionCommand CopyPrivateCommand { get; }
	public ActionCommand AssignPresetCommand { get; }
	public ActionCommand AddLineCommand { get; }
	public ActionCommand ResetCommand { get; }
	public ActionCommand RefreshPreviewCommand { get; }
	public String Status { get => mStatus; set => SetField(ref mStatus, value, nameof(Status)); }
	public String Query { get => mQuery; set { if (SetField(ref mQuery, value ?? String.Empty, nameof(Query), nameof(Missions))) SelectedMission = Missions.FirstOrDefault(); } }
	public String Region { get => mRegion; set { if (SetField(ref mRegion, value, nameof(Region), nameof(Missions))) SelectedMission = Missions.FirstOrDefault(); } }
	public String Side { get => mSide; set { if (SetField(ref mSide, value, nameof(Side), nameof(Squads))) SelectedSquad = Squads.FirstOrDefault(); } }
	public ModChoice? SelectedMission { get => mMission; set { if (!SetField(ref mMission, value, nameof(SelectedMission), nameof(Squads), nameof(Sides), nameof(FilteredPresets))) return; mSide = "全部"; Notify(nameof(Side)); SelectedSquad = Squads.FirstOrDefault(); } }
	public ModChoice? SelectedSquad { get => mSquad; set { if (SetField(ref mSquad, value, nameof(SelectedSquad))) { mExchangeSlot = null; mSlot = 0; RebuildSlot(); } } }
	public ModChoice? SelectedPreset { get => mPreset; set { if (SetField(ref mPreset, value, nameof(SelectedPreset))) RebuildLines(); } }
	public ModChoice? SelectedCharacter
	{
		get => CharacterChoices.FirstOrDefault(choice => choice.Value == N(CurrentSlot, "charaset_id"));
		set { if (value == null || mSquad == null || value.Value == N(CurrentSlot, "charaset_id")) return; EditableSlot()["charaset_id"] = value.Value; RebuildSlot(); }
	}
	public bool AllowSharedGear
	{
		get => Find(Edits, "charasets", "charaset_id", N(CurrentSlot, "charaset_id"))?["duplicate_if_shared"]?.GetValue<bool>() == false;
		set { if (N(CurrentSlot, "charaset_id") == 0 || value == AllowSharedGear) return; EditableGear()["duplicate_if_shared"] = !value; Changed(); }
	}
	public bool HasUnit => N(CurrentSlot, "charaset_id") != 0;
	public bool CanEditPreset => mPreset != null && mPreset.Value != 0;
	public String ExchangeLabel => mExchangeSlot.HasValue ? $"已选槽 {mExchangeSlot}：点击目标交换（再次点击此按钮取消）" : "交换 / 移动当前角色";
	public String UnitSummary => $"槽 {mSlot} · 角色 {N(CurrentSlot, "charaset_id")} · 职业 {N(CurrentSlot, "class_id")} · 预设 {N(CurrentSlot, "equipaiset_id")} · 等级预览 {PreviewLevel}";
	public String SharedWarning => $"当前角色在 {CharacterReferences} 个任务槽位被引用（按 UnitSet/槽去重）。装备修改作用于 CharaSet 全局；默认 duplicate_if_shared=true，导出必须拒绝共享覆盖，不会自动克隆。";
	public String PresetWarning => mPreset?.Value == 0 ? "预设 0：职业默认 + 当前装备技能。职业战术请在现有职业模块编辑。" : $"非零预设：只使用以下列表，不自动加入职业或装备技能。{(Lines.Count == 0 ? "警告：空预设将没有任何战术！" : "")} 修改现有预设影响全部引用；需要隔离请复制为私有并分配。";
	public String References => String.Join(Environment.NewLine, MissionModCatalog.Rows("missions").SelectMany(mission => Rows(mission, "squads").SelectMany(squad => Enumerable.Range(0, 6).Select(slot => (mission, squad, slot, unit: EffectiveSlot(squad, slot)))))
		.Where(entry => N(entry.unit, "charaset_id") != 0 && N(entry.unit, "equipaiset_id") == mPreset?.Value)
		.Select(entry => $"{T(entry.mission, "stage_name")} · {T(entry.squad, "side")} · {N(entry.squad, "unitset_id")} / 槽 {entry.slot} · {T(entry.unit, "chara_name")}"));
	public String CatalogReferences => String.Join(Environment.NewLine, Rows(Find(MissionModCatalog.Root, "equipaiset_presets", "id", mPreset?.Value ?? 0), "references")
		.Select(row => $"{T(row, "stage_name")} · {T(row, "context")} · {T(row, "squad_name")} / 槽 {N(row, "slot")} · {T(row, "unit")}"));
	public String FinalTactics
	{
		get
		{
			if (!HasUnit) return "空槽：无战术。";
			var lines = ResolveTactics();
			return lines.Count == 0 ? "警告：当前角色没有战术。空的非零预设不会回退职业默认。" : String.Join(Environment.NewLine, lines.Select((line, index) => $"{index + 1}. {SkillName(N(line, "skill_id"))} · IF0 {IfName(N(line, "if0"))} · IF1 {IfName(N(line, "if1"))}{(N(line, "learn_level", 1) > PreviewLevel ? " [等级未解锁]" : "")}{T(line, "preview_warning")}"));
		}
	}
	public String PreviewNotice => "预览按当前角色职业、装备和预设重算；等级取任务敌方等级，非数字/友方等级未知时按 1。可用技能不等于能在实战触发；IF 条件由游戏判断。";
	private int PreviewLevel => Int32.TryParse(T(Mission, "enemy_level"), out int level) && T(Squad, "side") != "PL" ? Math.Max(1, level) : 1;
	private JsonObject? Mission => Find(MissionModCatalog.Root, "missions", "quest_id", mMission?.Value ?? -1);
	private JsonObject? Squad => Find(Mission, "squads", "unitset_id", mSquad?.Value ?? -1);
	private JsonObject CurrentSlot => EffectiveSlot(Squad, mSlot);
	private int CharacterReferences => MissionModCatalog.Rows("missions").SelectMany(row => Rows(row, "squads")).DistinctBy(row => N(row, "unitset_id"))
		.Sum(squad => Enumerable.Range(0, 6).Count(slot => N(EffectiveSlot(squad, slot), "charaset_id") == N(CurrentSlot, "charaset_id")));

	public void Import(JsonObject edits)
	{
		var source = (JsonObject)((edits["edits"] as JsonObject) ?? edits).DeepClone();
		foreach (String key in ArrayKeys)
		{
			if (source[key] != null && source[key] is not JsonArray) throw new InvalidDataException($"{key} 必须为数组。");
			source[key] ??= new JsonArray();
			if (Rows(source, key).Count() != ((JsonArray)source[key]!).Count) throw new InvalidDataException($"{key} 中每项必须为对象。");
		}
		if (source["equipaiset_lines"] != null && source["equipaiset_lines"] is not JsonObject) throw new InvalidDataException("equipaiset_lines 必须为对象。");
		source["equipaiset_lines"] ??= new JsonObject();
		foreach (var pair in (JsonObject)source["equipaiset_lines"]!)
			if (!Int32.TryParse(pair.Key, out int id) || id == 0 || pair.Value is not JsonArray) throw new InvalidDataException("预设编辑必须为非零 ID 对应的行数组；预设 0 请使用职业模块。");
		ValidateShape(source);
		MissionModPatch.Generate(source, ModTarget.Western);
		foreach (var entry in Rows(source, "class_tactics"))
		{
			var record = mProject.Classes.GetRecord(N(entry, "class_id")) ?? throw new InvalidDataException("不支持的职业 ID。");
			foreach (var line in Rows(entry, "lines"))
			{
				int action = N(line, "action");
				if (action is < 3 or > 10) throw new InvalidDataException("职业技能槽必须为 3–10。");
				var slot = action < 7 ? record.ActiveSkills[action - 3] : record.PassiveSkills[action - 7];
				if (!slot.Choices.Any(choice => choice.Value == N(line, "skill_id"))) throw new InvalidDataException("职业技能类型不匹配。");
			}
		}
		mProject.ImportMissionClassEdits((JsonArray)source["class_tactics"]!);
		source.Remove("class_tactics");
		Edits.Clear();
		foreach (var pair in source) Edits[pair.Key] = pair.Value?.DeepClone();
		mExchangeSlot = null;
		RebuildDefaultGear();
		Notify(nameof(Edits), nameof(Presets));
		mPreset = Presets.FirstOrDefault(choice => choice.Value == mPreset?.Value) ?? Presets.FirstOrDefault();
		RebuildSlot();
		RebuildLines();
	}

	public void Reset()
	{
		Edits.Clear();
		foreach (String key in ArrayKeys.Where(key => key != "class_tactics")) Edits[key] = new JsonArray();
		Edits["equipaiset_lines"] = new JsonObject();
		mExchangeSlot = null;
		mMission ??= Missions.FirstOrDefault();
		mSquad = Squads.FirstOrDefault();
		mPreset = mPresets.FirstOrDefault();
		RebuildDefaultGear();
		RebuildSlot();
		RebuildLines();
		Notify(nameof(Edits), nameof(SelectedMission), nameof(SelectedSquad), nameof(Presets));
	}

	public void RefreshLocale()
	{
		foreach (var choice in mMissions.Concat(mSquads).Concat(mPresets).Concat(CharacterChoices).Concat(ItemChoices).Concat(SkillChoices).Concat(IfChoices)) choice.RefreshName();
		RebuildSlot();
		RebuildLines();
		RebuildDefaultGear();
	}

	public void SelectSlot(int slot)
	{
		if (slot is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(slot));
		if (mExchangeSlot is int source && mSquad != null) Exchange(mSquad.Value, source, slot);
		mExchangeSlot = null;
		mSlot = slot;
		RebuildSlot();
		SelectedPreset = Presets.FirstOrDefault(choice => choice.Value == N(CurrentSlot, "equipaiset_id"));
	}

	public void Exchange(int unitsetId, int first, int second)
	{
		if (first is < 0 or > 5 || second is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(first));
		if (first == second) return;
		var squad = MissionModCatalog.Rows("missions").SelectMany(row => Rows(row, "squads")).FirstOrDefault(row => N(row, "unitset_id") == unitsetId) ?? throw new InvalidDataException("队伍不存在。");
		var edits = UnitEdit(squad);
		var slots = (JsonArray)edits["slots"]!;
		JsonObject left = SlotEdit(squad, first), right = SlotEdit(squad, second);
		left["slot"] = second;
		right["slot"] = first;
		foreach (var old in slots.OfType<JsonObject>().Where(row => N(row, "slot") == first || N(row, "slot") == second).ToArray()) slots.Remove(old);
		slots.Add(left);
		slots.Add(right);
		foreach (var allocation in Rows(Edits, "equipaiset_allocations").Where(row => N(row, "unitset_id") == unitsetId))
		{
			int position = N(allocation, "slot");
			if (position == first || position == second) allocation["slot"] = position == first ? second : first;
		}
		RebuildSlot();
	}

	private JsonObject EffectiveSlot(JsonObject? squad, int position)
	{
		var original = Find(squad, "slots", "slot", position);
		var edit = Find(Find(Edits, "unitsets", "unitset_id", N(squad, "unitset_id", -1)), "slots", "slot", position);
		int characterId = N(edit ?? original, "charaset_id");
		var character = Find(MissionModCatalog.Root, "charasets", "id", characterId);
		bool unchanged = characterId != 0 && characterId == N(original, "charaset_id");
		var result = unchanged ? (JsonObject)original!.DeepClone() : new JsonObject { ["slot"] = position, ["class_id"] = N(character, "class_id"), ["chara_name"] = T(character, "name"), ["gear"] = character?["gear"]?.DeepClone() ?? new JsonArray() };
		result["charaset_id"] = characterId;
		result["equipaiset_id"] = N(edit ?? original, "equipaiset_id");
		result["flags"] = N(edit ?? original, "flags");
		if (edit != null) foreach (String key in new[] { "use_duplicate", "equipaiset_alloc_key" }) if (edit[key] != null) result[key] = edit[key]!.DeepClone();
		if (characterId == 0) { result["class_id"] = 0; result["gear"] = new JsonArray(); }
		var gearEdit = Find(Edits, "charasets", "charaset_id", characterId);
		if (gearEdit?["gear"] is JsonArray editedGear) result["gear"] = editedGear.DeepClone();
		return result;
	}

	private JsonObject UnitEdit(JsonObject squad)
	{
		int id = N(squad, "unitset_id");
		var edit = Find(Edits, "unitsets", "unitset_id", id);
		if (edit != null) return edit;
		edit = new JsonObject { ["unitset_id"] = id, ["unitset_symbol"] = T(squad, "unitset_symbol"), ["slots"] = new JsonArray() };
		((JsonArray)Edits["unitsets"]!).Add(edit);
		return edit;
	}
	private JsonObject SlotEdit(JsonObject squad, int position)
	{
		var existing = Find(Find(Edits, "unitsets", "unitset_id", N(squad, "unitset_id")), "slots", "slot", position);
		if (existing != null) return (JsonObject)existing.DeepClone();
		var slot = EffectiveSlot(squad, position);
		return new JsonObject { ["slot"] = position, ["charaset_id"] = N(slot, "charaset_id"), ["equipaiset_id"] = N(slot, "equipaiset_id"), ["flags"] = N(slot, "flags") };
	}
	private JsonObject EditableSlot()
	{
		var squad = Squad ?? throw new InvalidOperationException("请先选择队伍。");
		var unit = UnitEdit(squad);
		var slot = Find(unit, "slots", "slot", mSlot);
		if (slot != null) return slot;
		slot = SlotEdit(squad, mSlot);
		((JsonArray)unit["slots"]!).Add(slot);
		return slot;
	}
	private JsonObject EditableGear()
	{
		int id = N(CurrentSlot, "charaset_id");
		var edit = Find(Edits, "charasets", "charaset_id", id);
		if (edit != null) return edit;
		var gear = new JsonArray();
		var original = Rows(Find(MissionModCatalog.Root, "charasets", "id", id), "gear").ToArray();
		for (int index = 0; index < 4; index++) gear.Add(original.ElementAtOrDefault(index)?.DeepClone() ?? new JsonObject { ["item_id"] = 0 });
		edit = new JsonObject { ["charaset_id"] = id, ["gear"] = gear, ["duplicate_if_shared"] = true };
		((JsonArray)Edits["charasets"]!).Add(edit);
		return edit;
	}
	internal void SetGear(int index, int itemId)
	{
		if (!HasUnit) return;
		var gear = (JsonArray)EditableGear()["gear"]!;
		while (gear.Count < 4) gear.Add(new JsonObject { ["item_id"] = 0 });
		var metadata = Find(MissionModCatalog.Root, "items", "id", itemId);
		gear[index] = new JsonObject { ["item_id"] = itemId, ["rom_item_id"] = itemId, ["edited"] = true, ["item_symbol"] = T(metadata, "symbol"), ["item_name"] = T(metadata, "name") };
		Changed();
	}
	private void RebuildSlot()
	{
		Seats.Clear();
		foreach (int position in new[] { 5, 4, 3, 2, 1, 0 })
		{
			var slot = EffectiveSlot(Squad, position);
			Seats.Add(new MissionSeat(position, $"{(position == mSlot ? "▶ " : "")}{(position > 2 ? "后排" : "前排")} {position}\n{(N(slot, "charaset_id") == 0 ? "空槽" : T(slot, "chara_name", $"角色 {N(slot, "charaset_id")}"))}", SelectSlotCommand));
		}
		Gear.Clear();
		var gear = Rows(CurrentSlot, "gear").ToArray();
		for (int index = 0; index < 4; index++) Gear.Add(new MissionGearSlot(this, index, GearItemId(gear.ElementAtOrDefault(index))));
		Changed();
	}
	private void Changed() => Notify(nameof(Edits), nameof(SelectedCharacter), nameof(HasUnit), nameof(AllowSharedGear), nameof(UnitSummary), nameof(SharedWarning), nameof(FinalTactics), nameof(References), nameof(ExchangeLabel), nameof(FilteredPresets));

	private JsonArray PresetLines(int? presetId = null)
	{
		int id = presetId ?? mPreset?.Value ?? 0;
		JsonNode? lines = id < 0 ? Find(Edits, "equipaiset_creates", "temp_id", id)?["lines"] : Edits["equipaiset_lines"]?[id.ToString()] ?? Find(MissionModCatalog.Root, "equipaiset_presets", "id", id)?["lines"];
		return (JsonArray?)lines?.DeepClone() ?? new JsonArray();
	}
	internal void CommitLines(JsonArray lines)
	{
		if (!CanEditPreset) return;
		for (int index = 0; index < lines.Count; index++) lines[index]!["slot"] = index;
		if (mPreset!.Value < 0) Find(Edits, "equipaiset_creates", "temp_id", mPreset.Value)!["lines"] = lines.DeepClone();
		else Edits["equipaiset_lines"]![mPreset.Value.ToString()] = lines.DeepClone();
		RebuildLines();
		Changed();
	}
	internal void UpdateLine(int index, String key, int value)
	{
		var lines = PresetLines();
		if (index < 0 || index >= lines.Count) return;
		lines[index]![key] = value;
		if (key == "skill_id")
		{
			var line = (JsonObject)lines[index]!;
			line["ref_kind"] = value is >= 2 and <= 10 ? "class_slot" : "skill";
			line["action"] = value is >= 2 and <= 10 ? value : ModCatalog.Skills.FirstOrDefault(skill => skill.Choice.Value == value)?.IsPassive == true ? 7 : 3;
			foreach (String stale in new[] { "skill_name", "skill_symbol", "resolved_skill_id", "resolved_skill_name", "resolved_skill_symbol", "marker_id", "marker_label" }) line.Remove(stale);
		}
		else ((JsonObject)lines[index]!).Remove(key + "_symbol");
		if (mPreset!.Value < 0) Find(Edits, "equipaiset_creates", "temp_id", mPreset.Value)!["lines"] = lines;
		else Edits["equipaiset_lines"]![mPreset.Value.ToString()] = lines;
		Changed();
	}
	internal void MoveLine(int index, int offset)
	{
		var lines = PresetLines();
		int target = index + offset;
		if (index < 0 || index >= lines.Count || target < 0 || target >= lines.Count) return;
		var line = lines[index]; lines.RemoveAt(index); lines.Insert(target, line); CommitLines(lines);
	}
	internal void DeleteLine(int index) { var lines = PresetLines(); if (index >= 0 && index < lines.Count) { lines.RemoveAt(index); CommitLines(lines); } }
	private void RebuildLines()
	{
		Lines.Clear();
		if (CanEditPreset) foreach (var line in PresetLines().OfType<JsonObject>()) Lines.Add(new MissionTacticLine(this, Lines.Count, line));
		Notify(nameof(SelectedPreset), nameof(CanEditPreset), nameof(CanRenamePreset), nameof(PresetName), nameof(PresetWarning), nameof(References), nameof(CatalogReferences), nameof(FinalTactics));
	}
	private void CreatePreset(bool copyAndAssign)
	{
		if (copyAndAssign && !HasUnit) return;
		int source = copyAndAssign ? N(CurrentSlot, "equipaiset_id") : 0;
		int id = Math.Min(0, Rows(Edits, "equipaiset_creates").Select(row => N(row, "temp_id")).DefaultIfEmpty(0).Min()) - 1;
		var lines = copyAndAssign ? source == 0 ? new JsonArray(ResolveTactics().Select(line => (JsonNode)line.DeepClone()).ToArray()) : CurrentPresetLines() : new JsonArray();
		if (lines.Count > 8) { Status = "当前默认战术超过 8 行，不能无损复制为预设；请新建并明确选择最多 8 行。"; return; }
		for (int index = 0; index < lines.Count; index++) { lines[index]!["slot"] = index; if (source == 0) lines[index]!["ref_kind"] = "skill"; }
		((JsonArray)Edits["equipaiset_creates"]!).Add(new JsonObject { ["key"] = $"create:{-id}", ["temp_id"] = id, ["source_id"] = Math.Max(0, source), ["symbol"] = $"NEW_PRESET_{-id}", ["lines"] = lines });
		Notify(nameof(Presets));
		PresetsForMission = false;
		SelectedPreset = Presets.First(choice => choice.Value == id);
		if (copyAndAssign) AssignPresetCommand.Execute(null);
		Status = copyAndAssign ? "已复制当前角色的战术为私有预设并分配；负 ID 在导出时分配为真实 ID。" : "已创建未分配的空预设；先添加战术再分配。";
	}
	private JsonArray CurrentPresetLines()
	{
		String key = T(CurrentSlot, "equipaiset_alloc_key");
		var allocation = Rows(Edits, "equipaiset_allocations").FirstOrDefault(row => T(row, "key") == key && key.Length > 0);
		return allocation?["lines"] is JsonArray lines ? (JsonArray)lines.DeepClone() : PresetLines(N(CurrentSlot, "equipaiset_id"));
	}

	private List<JsonObject> ClassLines(int classId)
	{
		var source = Find(MissionModCatalog.Root, "class_tactics", "class_id", classId);
		var lines = Rows(source, "lines").Select(row => (JsonObject)row.DeepClone()).ToList();
		var record = mProject.Classes.GetRecord(classId);
		if (record == null) return lines;
		foreach (ModSkillSlot slot in record.ActiveSkills.Concat(record.PassiveSkills))
		{
			int action = (slot.IsPassive ? 7 : 3) + slot.Index;
			var line = lines.FirstOrDefault(row => N(row, "action") == action);
			int skillId = slot.SelectedSkill?.Value ?? 0;
			if (skillId == 0) { if (line != null) lines.Remove(line); continue; }
			if (line == null) { line = new JsonObject { ["action"] = action, ["if0"] = 0, ["if1"] = 0 }; lines.Add(line); }
			line["skill_id"] = skillId;
			line["learn_level"] = slot.Level;
			line["if0"] = slot.SelectedCondition0?.Value ?? 0;
			line["if1"] = slot.SelectedCondition1?.Value ?? 0;
		}
		return lines;
	}
	private IEnumerable<int> EffectiveGearIds()
	{
		var unit = CurrentSlot;
		var gear = Rows(unit, "gear").ToArray();
		var bases = Find(MissionModCatalog.Root, "class_equiptypes", "class_id", N(unit, "class_id"));
		int characterId = N(unit, "charaset_id");
		var reference = MissionModCatalog.Rows("missions").SelectMany(row => Rows(row, "squads")).SelectMany(row => Rows(row, "slots")).FirstOrDefault(row => N(row, "charaset_id") == characterId);
		int tier = N(unit, "equip_param", N(Squad, "paramset"));
		if (tier == 0 && Int32.TryParse(T(Squad, "exptype"), out int experienceType) && experienceType < 5) tier = 2;
		int overrideTier = N(reference, "chara_param_override");
		if (overrideTier != 0) tier = overrideTier;
		if (tier is < 2 or > 4) tier = 0;
		for (int index = 0; index < 4; index++)
		{
			var item = gear.ElementAtOrDefault(index);
			int raw = GearItemId(item);
			if (raw != 0) { yield return raw; continue; }
			int equiptype = N(item, "equiptype_id", -1);
			if (bases?["slots"] is JsonArray slots && index < slots.Count)
			{
				int baseId = slots[index] is JsonObject row ? N(row, "equiptype_id") : slots[index]?.GetValue<int>() ?? 0;
				equiptype = baseId + tier * 11;
			}
			int column = N(item, "equiptype_col", PreviewLevel < 15 ? 0 : PreviewLevel < 28 ? 1 : 2);
			var table = Find(Edits, "equiptype_items", "equiptype_id", equiptype) ?? Find(MissionModCatalog.Root, "equiptype_items", "id", equiptype);
			int itemId = N(table, $"item_col{column}_id", N(item, "item_id"));
			yield return characterId is >= 1 and <= 560 && itemId is >= 783 and < 983 ? 0 : itemId;
		}
	}
	private List<JsonObject> ResolveTactics()
	{
		var classes = ClassLines(N(CurrentSlot, "class_id"));
		if (N(CurrentSlot, "equipaiset_id") != 0 || T(CurrentSlot, "equipaiset_alloc_key").Length > 0)
		{
			var result = new List<JsonObject>();
			foreach (var preset in CurrentPresetLines().OfType<JsonObject>())
			{
				var line = (JsonObject)preset.DeepClone();
				int reference = N(line, "skill_id");
				if (reference is >= 2 and <= 10 || T(line, "ref_kind") == "class_slot")
				{
					var resolved = classes.FirstOrDefault(row => N(row, "action") == reference);
					line["skill_id"] = N(resolved, "skill_id"); line["learn_level"] = N(resolved, "learn_level", 1);
					if (resolved == null) line["preview_warning"] = $" [职业槽 {reference} 无技能，不会产生有效战术]";
				}
				result.Add(line);
			}
			return result;
		}
		var have = classes.Select(row => N(row, "skill_id")).ToHashSet();
		var equipment = new List<JsonObject>();
		foreach (int itemId in EffectiveGearIds())
		{
			var metadata = Find(MissionModCatalog.Root, "item_skills", "id", itemId);
			int skillId = N(metadata, "skill_id");
			if (skillId == 0 || !have.Add(skillId)) continue;
			var line = (JsonObject)metadata!.DeepClone();
			line["action"] = T(metadata, "skill_symbol").StartsWith("PAS_", StringComparison.Ordinal) ? 7 : 3;
			line["learn_level"] = 1;
			var conditions = mProject.Classes.Conditions.Get(skillId);
			line["if0"] = conditions.First;
			line["if1"] = conditions.Second;
			equipment.Add(line);
		}
		return [.. classes.Where(row => N(row, "action") < 7), .. equipment.OrderBy(row => N(row, "action")), .. classes.Where(row => N(row, "action") >= 7)];
	}
	private String SkillName(int id) => ModCatalog.FindSkill(id)?.Name ?? T(Find(MissionModCatalog.Root, "skills", "id", id), "name", $"技能 {id}");
	private static int GearItemId(JsonObject? item) => item?["edited"] is JsonValue edited && edited.TryGetValue<bool>(out bool changed) && changed ? N(item, "item_id") : N(item, "rom_item_id", N(item, "item_id"));
	private String IfName(int id) => IfChoices.FirstOrDefault(choice => choice.Value == id)?.Name ?? id.ToString();
	private void RebuildDefaultGear()
	{
		DefaultGearRows.Clear();
		foreach (var row in MissionModCatalog.Rows("equiptype_items")) DefaultGearRows.Add(new MissionDefaultGearRow(this, row, Find(Edits, "equiptype_items", "equiptype_id", N(row, "id")) ?? row));
	}
	internal void SetDefaultGear(int id, int column, int itemId)
	{
		var original = Find(MissionModCatalog.Root, "equiptype_items", "id", id)!;
		var edit = Find(Edits, "equiptype_items", "equiptype_id", id);
		if (edit == null)
		{
			edit = new JsonObject { ["equiptype_id"] = id, ["equiptype_symbol"] = T(original, "symbol"), ["item_col0_id"] = N(original, "item_col0_id"), ["item_col1_id"] = N(original, "item_col1_id"), ["item_col2_id"] = N(original, "item_col2_id") };
			((JsonArray)Edits["equiptype_items"]!).Add(edit);
		}
		edit[$"item_col{column}_id"] = itemId;
		if (Enumerable.Range(0, 3).All(index => N(edit, $"item_col{index}_id") == N(original, $"item_col{index}_id"))) Remove("equiptype_items", "equiptype_id", id);
		Changed();
	}
	internal void RestoreDefaultGear(int id) { Remove("equiptype_items", "equiptype_id", id); RebuildDefaultGear(); Changed(); }
	private void Remove(String table, String key, int id) { var row = Find(Edits, table, key, id); if (row != null) ((JsonArray)Edits[table]!).Remove(row); }
	internal static int N(JsonNode? row, String key, int fallback = 0) => MissionModCatalog.Number(row, key, fallback);
	internal static String T(JsonNode? row, String key, String fallback = "") => MissionModCatalog.Text(row, key, fallback);
	private static IEnumerable<JsonObject> Rows(JsonNode? root, String key) => (root?[key] as JsonArray)?.OfType<JsonObject>() ?? [];
	private static JsonObject? Find(JsonNode? root, String table, String key, int id) => Rows(root, table).FirstOrDefault(row => N(row, key) == id);
	private static ModChoice Choice(int id, String name) => new() { Value = id, EnglishName = name, JapaneseName = name, ChineseName = name };
	private static void ValidateShape(JsonObject source)
	{
		foreach (var unit in Rows(source, "unitsets"))
		{
			if (unit["slots"] is not JsonArray slots || slots.Any(row => row is not JsonObject || N(row, "slot", -1) is < 0 or > 5) || slots.Select(row => N(row, "slot")).Distinct().Count() != slots.Count) throw new InvalidDataException("队伍槽位必须为不重复的 0–5。");
		}
		foreach (var character in Rows(source, "charasets"))
		{
			if (character["gear"] is not JsonArray gear || gear.Count > 4 || gear.Any(row => row is not JsonObject)) throw new InvalidDataException("装备必须为最多四个对象。");
			if (character["duplicate_if_shared"] is JsonNode flag && (flag is not JsonValue value || !value.TryGetValue<bool>(out _))) throw new InvalidDataException("duplicate_if_shared 必须为布尔值。");
		}
		foreach (var create in Rows(source, "equipaiset_creates")) if (N(create, "temp_id") >= 0 || create["lines"] is not JsonArray) throw new InvalidDataException("新预设必须使用负 ID 和战术数组。");
		var ids = Rows(source, "equipaiset_creates").Select(row => N(row, "temp_id")).ToArray();
		if (ids.Distinct().Count() != ids.Length) throw new InvalidDataException("新预设负 ID 重复。");
		foreach (var lines in ((JsonObject)source["equipaiset_lines"]!).Select(pair => pair.Value).Concat(Rows(source, "equipaiset_creates").Select(row => row["lines"])).Concat(Rows(source, "equipaiset_allocations").Select(row => row["lines"])).Concat(Rows(source, "class_tactics").Select(row => row["lines"])))
			if (lines is not JsonArray array || array.Any(row => row is not JsonObject)) throw new InvalidDataException("战术列表必须为对象数组。");
	}

	public static void Validate()
	{
		var state = new MissionEditorState(new ModProjectState());
		var squad = MissionModCatalog.Rows("missions").SelectMany(row => Rows(row, "squads")).FirstOrDefault(row => Rows(row, "slots").Any(slot => N(slot, "charaset_id") != 0)) ?? throw new InvalidDataException("自检需要真实任务资源。");
		int first = N(Rows(squad, "slots").First(row => N(row, "charaset_id") != 0), "slot");
		int second = (first + 1) % 6;
		var beforeFirst = state.SlotEdit(squad, first); var beforeSecond = state.SlotEdit(squad, second);
		state.Exchange(N(squad, "unitset_id"), first, second);
		var swapped = state.SlotEdit(squad, second); swapped["slot"] = first;
		if (!JsonNode.DeepEquals(beforeFirst, swapped)) throw new InvalidDataException("交换未保留角色、预设和标志。");
		state.Exchange(N(squad, "unitset_id"), first, second);
		if (!JsonNode.DeepEquals(beforeFirst, state.SlotEdit(squad, first)) || !JsonNode.DeepEquals(beforeSecond, state.SlotEdit(squad, second))) throw new InvalidDataException("交换往返失败。");
		var snapshot = (JsonObject)state.Edits.DeepClone();
		state.Import(snapshot);
		if (!JsonNode.DeepEquals(snapshot, state.Edits)) throw new InvalidDataException("Edits 导入往返失败。");
		state.Reset();
		if (ArrayKeys.Any(key => (state.Edits[key] as JsonArray)?.Count > 0)) throw new InvalidDataException("重置失败。");
		state.SelectedMission = state.mMissions.First(choice => Rows(Find(MissionModCatalog.Root, "missions", "quest_id", choice.Value), "squads").Any(row => N(row, "unitset_id") == N(squad, "unitset_id")));
		state.SelectedSquad = state.Squads.First(choice => choice.Value == N(squad, "unitset_id"));
		state.SelectSlot(first);
		state.CreatePreset(false);
		state.PresetName = "STATE_VALIDATE_PRIVATE";
		if (T(Rows(state.Edits, "equipaiset_creates").Single(), "symbol") != "STATE_VALIDATE_PRIVATE") throw new InvalidDataException("私有预设改名未写入 Edits。");
		state.AssignPresetCommand.Execute(null);
		state.PresetsForMission = true;
		if (!state.FilteredPresets.Any(choice => choice.Value == state.SelectedPreset?.Value)) throw new InvalidDataException("任务过滤遗漏了新分配的私有预设。");
		if (N(state.CurrentSlot, "equipaiset_id") >= 0 || state.ResolveTactics().Count != 0) throw new InvalidDataException("空的非零预设错误地回退了职业战术。");
		state.AddLineCommand.Execute(null);
		if (state.Lines.Count != 1 || state.ResolveTactics().Count != 1) throw new InvalidDataException("职业槽标记解析失败。");
		state.UpdateLine(0, "if0", 1);
		if (N(state.ResolveTactics()[0], "if0") != 1) throw new InvalidDataException("预设 IF 未实时更新。");
		var row = state.DefaultGearRows.First();
		int originalItem = row.Low?.Value ?? 0;
		state.SetDefaultGear(row.Id, 0, state.ItemChoices.First(choice => choice.Value != originalItem).Value);
		if (!Rows(state.Edits, "equiptype_items").Any()) throw new InvalidDataException("默认装备修改未写入 Edits。");
		snapshot = (JsonObject)state.Edits.DeepClone();
		state.Import(new JsonObject { ["edits"] = snapshot.DeepClone() });
		if (!JsonNode.DeepEquals(snapshot, state.Edits)) throw new InvalidDataException("私有预设/默认装备 wrapper 往返失败。");
		state.RestoreDefaultGear(row.Id);
		if (Rows(state.Edits, "equiptype_items").Any()) throw new InvalidDataException("默认装备恢复失败。");
		var invalid = (JsonObject)state.Edits.DeepClone(); invalid["unitsets"] = new JsonArray(new JsonObject { ["unitset_id"] = N(squad, "unitset_id"), ["slots"] = new JsonArray(new JsonObject { ["slot"] = 6 }) });
		snapshot = (JsonObject)state.Edits.DeepClone();
		try { state.Import(invalid); throw new InvalidOperationException("非法槽位未被拒绝。"); } catch (InvalidDataException) { }
		if (!JsonNode.DeepEquals(snapshot, state.Edits)) throw new InvalidDataException("失败的导入改变了 Edits。");
	}
}

internal sealed record MissionSeat(int Index, String Label, ActionCommand SelectCommand);

internal sealed class MissionGearSlot : ObservableObject
{
	private readonly MissionEditorState mState;
	private ModChoice? mSelectedItem;
	public MissionGearSlot(MissionEditorState state, int index, int itemId) { mState = state; Index = index; mSelectedItem = Choices.FirstOrDefault(choice => choice.Value == itemId); }
	public int Index { get; }
	public String Label => $"装备 {Index + 1}";
	public IReadOnlyList<ModChoice> Choices => mState.ItemChoices;
	public ModChoice? SelectedItem { get => mSelectedItem; set { if (value != null && SetField(ref mSelectedItem, value, nameof(SelectedItem))) mState.SetGear(Index, value.Value); } }
}

internal sealed class MissionTacticLine : ObservableObject
{
	private readonly MissionEditorState mState;
	private ModChoice? mSkill;
	private ModChoice? mIf0;
	private ModChoice? mIf1;
	public MissionTacticLine(MissionEditorState state, int index, JsonObject line)
	{
		mState = state; Index = index;
		mSkill = SkillChoices.FirstOrDefault(choice => choice.Value == MissionEditorState.N(line, "skill_id"));
		mIf0 = IfChoices.FirstOrDefault(choice => choice.Value == MissionEditorState.N(line, "if0"));
		mIf1 = IfChoices.FirstOrDefault(choice => choice.Value == MissionEditorState.N(line, "if1"));
		UpCommand = new(_ => state.MoveLine(Index, -1)); DownCommand = new(_ => state.MoveLine(Index, 1)); DeleteCommand = new(_ => state.DeleteLine(Index));
	}
	public int Index { get; }
	public String Label => $"#{Index + 1}";
	public IReadOnlyList<ModChoice> SkillChoices => mState.SkillChoices;
	public IReadOnlyList<ModChoice> IfChoices => mState.IfChoices;
	public ModChoice? Skill { get => mSkill; set { if (value != null && SetField(ref mSkill, value, nameof(Skill))) mState.UpdateLine(Index, "skill_id", value.Value); } }
	public ModChoice? If0 { get => mIf0; set { if (value != null && SetField(ref mIf0, value, nameof(If0))) mState.UpdateLine(Index, "if0", value.Value); } }
	public ModChoice? If1 { get => mIf1; set { if (value != null && SetField(ref mIf1, value, nameof(If1))) mState.UpdateLine(Index, "if1", value.Value); } }
	public ActionCommand UpCommand { get; }
	public ActionCommand DownCommand { get; }
	public ActionCommand DeleteCommand { get; }
}

internal sealed class MissionDefaultGearRow : ObservableObject
{
	private readonly MissionEditorState mState;
	private ModChoice? mLow;
	private ModChoice? mMiddle;
	private ModChoice? mHigh;
	public MissionDefaultGearRow(MissionEditorState state, JsonObject original, JsonObject effective)
	{
		mState = state; Id = MissionEditorState.N(original, "id"); Label = $"{Id} · {MissionEditorState.T(original, "symbol")}";
		mLow = Choices.FirstOrDefault(choice => choice.Value == MissionEditorState.N(effective, "item_col0_id"));
		mMiddle = Choices.FirstOrDefault(choice => choice.Value == MissionEditorState.N(effective, "item_col1_id"));
		mHigh = Choices.FirstOrDefault(choice => choice.Value == MissionEditorState.N(effective, "item_col2_id"));
		RestoreCommand = new(_ => state.RestoreDefaultGear(Id));
	}
	public int Id { get; }
	public String Label { get; }
	public IReadOnlyList<ModChoice> Choices => mState.ItemChoices;
	public ModChoice? Low { get => mLow; set { if (value != null && SetField(ref mLow, value, nameof(Low))) mState.SetDefaultGear(Id, 0, value.Value); } }
	public ModChoice? Middle { get => mMiddle; set { if (value != null && SetField(ref mMiddle, value, nameof(Middle))) mState.SetDefaultGear(Id, 1, value.Value); } }
	public ModChoice? High { get => mHigh; set { if (value != null && SetField(ref mHigh, value, nameof(High))) mState.SetDefaultGear(Id, 2, value.Value); } }
	public ActionCommand RestoreCommand { get; }
}
