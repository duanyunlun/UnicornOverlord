using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;

namespace UnicornOverlord;

internal class ViewModel : INotifyPropertyChanged
{
	private const int InventoryCapacity = 3800;
	private const int MaximumItemCount = 999;

	private static FilePickerFileType SaveFileType() => new(T("独角兽之王存档"))
	{
		Patterns = ["UCSAVEFILE*.DAT", "*.DAT", "*.dat"],
	};

	private static FilePickerFileType CharacterFileType() => new(T("独角兽之王角色数据"))
	{
		Patterns = ["*.uocd"],
	};
	private static FilePickerFileType ModPackageFileType() => new(T("ZIP 压缩包"))
	{
		Patterns = ["*.zip"],
	};

	private readonly Window mOwner;
	private readonly Info Info = Info.Instance();
	private bool mIsSaveLoaded;
	private String mCurrentFileName = T("尚未打开存档");
	private String mFileLocation = T("当前没有活动文件");
	private String mStatusMessage;
	private int mWorkspaceIndex;
	private int mLanguageIndex;
	private int mSelectedCharacterIndex = -1;
	private int mItemCountTarget = 99;
	private ModCategory? mSelectedModCategory;
	private ModTarget mSelectedModTarget = ModTarget.Asia;
	private static String T(String source) => LocaleManager.Instance.Translate(source);
	private static String F(String source, params object?[] args) => LocaleManager.Instance.Format(source, args);

	public event PropertyChangedEventHandler? PropertyChanged;

	public ICommand OpenFileCommand { get; }
	public ICommand SaveFileCommand { get; }
	public ICommand SaveAsFileCommand { get; }
	public ICommand ChoiceItemCommand { get; }
	public ICommand ChoiceEquipmentCommand { get; }
	public ICommand ChoiceClassCommand { get; }
	public ICommand AppendItemCommand { get; }
	public ICommand AppendEquipmentCommand { get; }
	public ICommand AppendAllItemsCommand { get; }
	public ICommand AppendAllEquipmentCommand { get; }
	public ICommand ExportCharacterCommand { get; }
	public ICommand ImportCharacterCommand { get; }
	public ICommand InsertCharacterCommand { get; }
	public ICommand ChangeItemCountMaxCommand { get; }
	public ICommand ChangeCharacterBondMaxCommand { get; }
	public ICommand ExportModPackageCommand { get; }
	public TextEditorViewModel TextEditor { get; }

	public Basic Basic { get; } = new();
	public ObservableCollection<Character> Characters { get; private set; } = [];
	public ObservableCollection<Item> Items { get; private set; } = [];
	public ObservableCollection<Item> Equipments { get; private set; } = [];
	public ObservableCollection<Unit> Units { get; } = [];
	public ModProjectState ModProject { get; }
	public ObservableCollection<ModModule> ModModules { get; }
	public IReadOnlyList<ModCategory> ModCategories { get; private set; }
	public IReadOnlyList<ModTarget> ModTargets { get; } = ModTarget.All;
	public IReadOnlyList<EditorLanguage> Languages => LocaleManager.Instance.Languages;
	public String InventorySummary => LocaleManager.Instance.Format("物品 {0} 条，装备 {1} 条，共 {2} / {3} 条库存记录", Items.Count, Equipments.Count, Items.Count + Equipments.Count, InventoryCapacity);
	public String ModTargetSummary => $"{SelectedModTarget.DisplayName} · Title ID {SelectedModTarget.TitleId} · Build ID {SelectedModTarget.BuildId}";
	public int SelectedModCount => ModModules.Count(module => !module.IsTextEditor && module.IsSelected);
	public bool CanExportMods => SelectedModCount > 0;
	public String ModSelectionSummary => CanExportMods ? LocaleManager.Instance.Format("已选择 {0} 个可用模块", SelectedModCount) : LocaleManager.Instance.Translate("请选择至少一个已接入模块");
	public String ContextStatusMessage => WorkspaceIndex switch
	{
		0 => IsSaveLoaded ? LocaleManager.Instance.Format("当前存档：{0}", CurrentFileName) : LocaleManager.Instance.Translate("存档编辑 · 尚未打开存档"),
		_ when IsTextEditorSelected => $"{TextEditor.SourceSummary} · {TextEditor.ChangeSummary}",
		_ => $"{ModSelectionSummary} · {LocaleManager.Instance.Translate("仅生成模拟器 pchtxt")}",
	};
	public ModCategory? SelectedModCategory
	{
		get => mSelectedModCategory;
		set
		{
			if (mSelectedModCategory == value) return;
			SetField(ref mSelectedModCategory, value, nameof(SelectedModCategory));
			OnPropertyChanged(nameof(IsTextEditorSelected));
			OnPropertyChanged(nameof(IsGameplayModWorkspace));
			OnPropertyChanged(nameof(IsTextModWorkspace));
			OnPropertyChanged(nameof(ActiveModTarget));
			OnPropertyChanged(nameof(ContextStatusMessage));
		}
	}
	public ModTarget SelectedModTarget
	{
		get => mSelectedModTarget;
		set
		{
			if (mSelectedModTarget == value) return;
			SetField(ref mSelectedModTarget, value, nameof(SelectedModTarget));
			OnPropertyChanged(nameof(ModTargetSummary));
			OnPropertyChanged(nameof(ActiveModTarget));
		}
	}
	public ModTarget ActiveModTarget
	{
		get => IsTextEditorSelected ? TextEditor.SelectedTarget : SelectedModTarget;
		set
		{
			if (IsTextEditorSelected) TextEditor.SelectedTarget = value;
			else SelectedModTarget = value;
			OnPropertyChanged(nameof(ActiveModTarget));
		}
	}

	public bool IsSaveLoaded
	{
		get => mIsSaveLoaded;
		private set
		{
			if (mIsSaveLoaded == value) return;
			SetField(ref mIsSaveLoaded, value, nameof(IsSaveLoaded));
			OnPropertyChanged(nameof(ContextStatusMessage));
		}
	}

	public String CurrentFileName
	{
		get => mCurrentFileName;
		private set => SetField(ref mCurrentFileName, value, nameof(CurrentFileName));
	}

	public String FileLocation
	{
		get => mFileLocation;
		private set => SetField(ref mFileLocation, value, nameof(FileLocation));
	}

	public String StatusMessage
	{
		get => mStatusMessage;
		private set => SetField(ref mStatusMessage, value, nameof(StatusMessage));
	}

	public int WorkspaceIndex
	{
		get => mWorkspaceIndex;
		set
		{
			int normalized = Math.Clamp(value, 0, 1);
			if (mWorkspaceIndex == normalized) return;
			SetField(ref mWorkspaceIndex, normalized, nameof(WorkspaceIndex));
			OnPropertyChanged(nameof(IsSaveWorkspace));
			OnPropertyChanged(nameof(IsModWorkspace));
			OnPropertyChanged(nameof(IsGameplayModWorkspace));
			OnPropertyChanged(nameof(IsTextModWorkspace));
			OnPropertyChanged(nameof(ContextStatusMessage));
		}
	}
	public bool IsSaveWorkspace => WorkspaceIndex == 0;
	public bool IsModWorkspace => WorkspaceIndex == 1;
	public bool IsTextEditorSelected => SelectedModCategory?.IsTextEditor == true;
	public bool IsGameplayModWorkspace => IsModWorkspace && !IsTextEditorSelected;
	public bool IsTextModWorkspace => IsModWorkspace && IsTextEditorSelected;

	public int LanguageIndex
	{
		get => mLanguageIndex;
		set
		{
			int normalized = Math.Clamp(value, 0, Languages.Count - 1);
			if (mLanguageIndex == normalized) return;
			mLanguageIndex = normalized;
			LocaleManager.Instance.SetLanguage(normalized);
			RefreshLocalizedCollections();
			OnPropertyChanged(nameof(LanguageIndex));
			OnPropertyChanged(nameof(InventorySummary));
			OnPropertyChanged(nameof(ModSelectionSummary));
			OnPropertyChanged(nameof(ContextStatusMessage));
			OnPropertyChanged(nameof(ModTargetSummary));
			if (!IsSaveLoaded)
			{
				CurrentFileName = LocaleManager.Instance.Translate("尚未打开存档");
				FileLocation = LocaleManager.Instance.Translate("当前没有活动文件");
			}
			StatusMessage = LocaleManager.Instance.Translate("就绪");
		}
	}

	public int SelectedCharacterIndex
	{
		get => mSelectedCharacterIndex;
		set => SetField(ref mSelectedCharacterIndex, value, nameof(SelectedCharacterIndex));
	}

	public int ItemCountTarget
	{
		get => mItemCountTarget;
		set => SetField(ref mItemCountTarget, Math.Clamp(value, 1, MaximumItemCount), nameof(ItemCountTarget));
	}

	public ViewModel(Window owner)
	{
		mOwner = owner;
		ModProject = new ModProjectState();
		ModModules = CreateModModules(ModProject);
		mLanguageIndex = ApplicationSettings.Language;
		mStatusMessage = Info.Item.Count == 0
			? T("未找到名称数据，未知条目将显示为数字 ID。")
			: T("就绪");

		OpenFileCommand = new ActionCommand(OpenFile);
		SaveFileCommand = new ActionCommand(SaveFile);
		SaveAsFileCommand = new ActionCommand(SaveAsFile);
		ChoiceItemCommand = new ActionCommand(ChoiceItem);
		ChoiceEquipmentCommand = new ActionCommand(ChoiceEquipment);
		ChoiceClassCommand = new ActionCommand(ChoiceClass);
		AppendItemCommand = new ActionCommand(AppendItem);
		AppendEquipmentCommand = new ActionCommand(AppendEquipment);
		AppendAllItemsCommand = new ActionCommand(AppendAllItems);
		AppendAllEquipmentCommand = new ActionCommand(AppendAllEquipment);
		ExportCharacterCommand = new ActionCommand(ExportCharacter);
		ImportCharacterCommand = new ActionCommand(ImportCharacter);
		InsertCharacterCommand = new ActionCommand(InsertCharacter);
		ChangeItemCountMaxCommand = new ActionCommand(ChangeItemCountMax);
		ChangeCharacterBondMaxCommand = new ActionCommand(ChangeCharacterBondMax);
		ExportModPackageCommand = new ActionCommand(ExportModPackage);
		TextEditor = new TextEditorViewModel(mOwner, message => StatusMessage = message, ModProject);
		TextEditor.PropertyChanged += TextEditor_PropertyChanged;
		foreach (ModModule module in ModModules) module.PropertyChanged += ModModule_PropertyChanged;
		ModCategories = CreateModCategories(ModModules);
		SelectedModCategory = ModCategories.FirstOrDefault();
	}

	internal static ObservableCollection<ModModule> CreateModModules() => CreateModModules(new ModProjectState());

	private static ObservableCollection<ModModule> CreateModModules(ModProjectState project)
	{
		return
		[
			new() { Project = project, Key = "ability_editor", Category = "技能", Name = "技能编辑器", Description = "从 441 个已校准技能中选择主动或被动技能，修改其 AP/PP 消耗、威力、命中、目标范围和首个效果参数。技能类型由游戏数据决定，不能手动互换。", IsAvailable = true, CalibrationState = "441 个技能已校准" },
			new() { Project = project, Key = "battle_preview", Category = "战斗", Name = "战斗预览调整", Description = "“不完美预览”用 5 次模拟的平均值展示大致趋势；“完全隐藏”则移除整个预览条。", IsAvailable = true, CalibrationState = "亚洲版已重定位", TemplateFile = "battle_preview_hidden.pchtxt" },
			new() { Project = project, Key = "battle_timer_freeze", Category = "战斗", Name = "冻结战斗计时器", Description = "冻结关卡实时计时器，战斗不再受时间限制。", IsAvailable = true, TemplateFile = "battle_timer_freeze.pchtxt" },
			new() { Project = project, Key = "type_matchups", Category = "战斗", Name = "类型克制", Description = "设置游戏内三种固有兵种克制倍率。它会作用于对应单位的所有攻击，并与技能自身的“对某类型威力加成”叠加；不写入存档，可随时启停。", IsAvailable = true, CalibrationState = "三项已校准" },
			new() { Project = project, Key = "character_randomizer", Category = "角色", Name = "角色加入随机化", Description = "随机改变教程五人以外的 63 名剧情角色加入顺序；过场、地图事件和能力触发时点不变。", IsAvailable = true, CalibrationState = "亚洲版已重定位", TemplateFile = "character_randomizer_base.pchtxt", Warning = "实验性功能：只用于新游戏，全流程保持启用并备份存档；中途移除可能使剧情读取不同步。" },
			new() { Project = project, Key = "class_editor", Category = "职业", Name = "职业编辑器", Description = "按职业名称修改 73 个职业的十项成长率、AP/PP，以及 4 个主动和 4 个被动技能及习得等级。", IsAvailable = true, CalibrationState = "73 个职业字段已校准" },
			new() { Project = project, Key = "fort_editor", Category = "据点", Name = "据点雇佣编辑器", Description = "按 63 个具体据点选择全部 248 个招募位置并修改可招募职业；选择后会载入原版职业，手动选择不受转职阶段限制。", IsAvailable = true, CalibrationState = "63 个据点 / 248 项已校准", Warning = "仅写职业字段，亚洲版记录中的性别与附加类型保持不变。" },
			new() { Project = project, Key = "mine_editor", Category = "采矿", Name = "采矿掉落编辑器", Description = "按五个地区采掘场选择 63 条具体原版掉落，修改物品、相对权重、挖掘目标和单局上限。", IsAvailable = true, CalibrationState = "5 个采掘场 / 63 项已校准", Warning = "藏宝图等一次性物品由游戏另行限制；提高权重时也要检查单局上限。" },
			new() { Project = project, Key = "shop_editor", Category = "商店", Name = "商店库存编辑器", Description = "按科尔尼亚的具体地图地点选择武具店和原版商品，修改商品、库存与金币价格；共享库存会明确标识。", IsAvailable = true, CalibrationState = "25 个武具店 / 211 个地点条目已校准", Warning = "当前接入科尔尼亚普通武具店；兑换所价格结构不同，不会错误套用金币价格。" },
			new() { Project = project, Key = "six_member_units", Category = "编队", Name = "六人编队", Description = "允许 S 级声望下将部队扩充至六人，并可设置荣誉费用。", IsAvailable = true, CalibrationState = "亚洲版已重定位", TemplateFile = "six_member_units.pchtxt", Warning = "卸载前必须先撤下所有部队的第六名成员。" },
			new() { Project = project, Key = "text_editor", Category = "文本", Name = "文本编辑器", Description = "基于所选语言 CPK 的原始 FMS 按索引修改文本，不改动源归档。", IsAvailable = true, CalibrationState = "CPK 文本表编辑" },
		];
	}

	private static IReadOnlyList<ModCategory> CreateModCategories(IReadOnlyList<ModModule> modules)
	{
		ModModule Find(String key) => modules.Single(module => module.Key == key);
		return
		[
			new("技能", [Find("ability_editor")]),
			new("战斗", [Find("battle_preview"), Find("battle_timer_freeze"), Find("type_matchups")]),
			new("角色", [Find("character_randomizer")]),
			new("职业", [Find("class_editor")]),
			new("据点", [Find("fort_editor")]),
			new("采矿", [Find("mine_editor")]),
			new("商店", [Find("shop_editor")]),
			new("编队", [Find("six_member_units")]),
			new("文本", [Find("text_editor")]),
		];
	}

	private void ModModule_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(ModModule.IsSelected)) return;
		OnPropertyChanged(nameof(SelectedModCount));
		OnPropertyChanged(nameof(CanExportMods));
		OnPropertyChanged(nameof(ModSelectionSummary));
		OnPropertyChanged(nameof(ContextStatusMessage));
	}

	private void TextEditor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(TextEditorViewModel.SelectedTarget)) OnPropertyChanged(nameof(ActiveModTarget));
		OnPropertyChanged(nameof(ContextStatusMessage));
	}

	private void InitializeData()
	{
		SelectedCharacterIndex = -1;
		Characters.Clear();
		Items.Clear();
		Equipments.Clear();
		Units.Clear();

		var characterDictionary = new Dictionary<uint, Character>();
		for (uint index = 0; index < 500; index++)
		{
			var character = new Character(Util.calcCharacterAddress(index));
			if (character.ID == 0xFFFFFFFF) break;
			characterDictionary.TryAdd(character.ID, character);
			Characters.Add(character);
		}

		for (uint index = 0; index < 164; index++)
		{
			uint baseAddress = Util.calcBondAddress(index);
			uint ownerID = SaveData.Instance().ReadNumber(baseAddress, 4);
			if (ownerID == 0xFFFFFFFF) break;

			var bonds = new ObservableCollection<Bond>();
			for (uint count = 0; count < 164; count++)
			{
				uint address = baseAddress + 4 + count * 8;
				uint targetID = SaveData.Instance().ReadNumber(address, 4);
				if (targetID == 0xFFFFFFFF) break;
				uint? nameID = characterDictionary.TryGetValue(targetID, out Character? target) ? target.Name : null;
				bonds.Add(new Bond(address, nameID));
			}

			if (characterDictionary.TryGetValue(ownerID, out Character? owner)) owner.Bonds = bonds;
		}

		for (uint index = 0; index < InventoryCapacity; index++)
		{
			var item = new Item(0xA0 + index * 20);
			if (item.Index == 0) break;
			if (item.Count == 0) Equipments.Add(item);
			else Items.Add(item);
		}

		for (uint index = 0; index < 10; index++)
		{
			Units.Add(new Unit(0x10D89A + index * 1720));
		}

		OnPropertyChanged(nameof(Basic));
		SelectedCharacterIndex = Characters.Count > 0 ? 0 : -1;
		OnPropertyChanged(nameof(InventorySummary));
	}

	private async void OpenFile(object? parameter)
	{
		if (!EnsureSaveWorkspace()) return;
		try
		{
			var files = await mOwner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = T("打开独角兽之王存档"),
				AllowMultiple = false,
				FileTypeFilter = [SaveFileType()],
			});
			String? filename = files.FirstOrDefault()?.TryGetLocalPath();
			if (String.IsNullOrEmpty(filename)) return;

			if (!SaveData.Instance().Open(filename))
			{
				StatusMessage = T("所选文件不是受支持的独角兽之王存档。");
				return;
			}

			InitializeData();
			UpdateActiveFile(filename);
			StatusMessage = T("存档已载入，并已在源文件旁创建备份。");
		}
		catch (Exception exception)
		{
			StatusMessage = F("打开失败：{0}", exception.Message);
		}
	}

	private void SaveFile(object? parameter)
	{
		if (!EnsureSaveWorkspace()) return;
		try
		{
			StatusMessage = SaveData.Instance().Save() ? T("存档保存成功。") : T("当前未载入存档。");
		}
		catch (Exception exception)
		{
			StatusMessage = F("保存失败：{0}", exception.Message);
		}
	}

	private async void SaveAsFile(object? parameter)
	{
		if (!EnsureSaveWorkspace()) return;
		if (!IsSaveLoaded) return;
		try
		{
			var file = await mOwner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
					Title = T("将独角兽之王存档另存为"),
				FileTypeChoices = [SaveFileType()],
				DefaultExtension = "DAT",
				SuggestedFileName = CurrentFileName,
			});
			String? filename = file?.TryGetLocalPath();
			if (String.IsNullOrEmpty(filename)) return;

			if (SaveData.Instance().SaveAs(filename))
			{
				UpdateActiveFile(filename);
					StatusMessage = T("存档副本保存成功。");
			}
		}
		catch (Exception exception)
		{
			StatusMessage = F("另存为失败：{0}", exception.Message);
		}
	}

	private bool EnsureSaveWorkspace()
	{
		if (WorkspaceIndex == 0) return true;
		StatusMessage = T("当前位于 MOD 制作工作区，未执行存档操作。");
		return false;
	}

	private async void ExportModPackage(object? parameter)
	{
		ModModule[] selectedModules = ModModules.Where(module => !module.IsTextEditor && module.IsSelected && module.IsAvailable).ToArray();
		if (selectedModules.Length == 0)
		{
			StatusMessage = T("请至少选择一个已接入的 MOD 模块。");
			return;
		}

		try
		{
			var file = await mOwner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
					Title = T("导出独角兽之王 MOD 包"),
				FileTypeChoices = [ModPackageFileType()],
				DefaultExtension = "zip",
				SuggestedFileName = $"UnicornOverlord-{SelectedModTarget.Key}-{SelectedModTarget.GameVersion}-Mods.zip",
			});
			String? filename = file?.TryGetLocalPath();
			if (String.IsNullOrEmpty(filename)) return;

			ModPackageBuilder.Create(filename, selectedModules, SelectedModTarget);
			StatusMessage = F("MOD 包导出成功：包含 {0} 个模块。", selectedModules.Length);
		}
		catch (Exception exception)
		{
			StatusMessage = F("MOD 包导出失败：{0}", exception.Message);
		}
	}

	private async void ChoiceItem(object? parameter)
	{
		if (parameter is not Item item) return;
		await ChoiceItemAsync(ChoiceWindow.eType.eItem, item);
	}

	private async void ChoiceEquipment(object? parameter)
	{
		if (parameter is not Item item) return;
		if (!await ChoiceItemAsync(ChoiceWindow.eType.eEquipment, item)) return;
		var info = Info.Search(Info.Kind, item.ID);
		if (info != null && uint.TryParse(info.Name, out uint status)) item.Status = status;
	}

	private async Task<bool> ChoiceItemAsync(ChoiceWindow.eType type, Item item)
	{
		var dialog = new ChoiceWindow { Type = type, ID = item.ID };
		if (!await dialog.ShowDialog<bool>(mOwner)) return false;
		item.ID = dialog.ID;
		item.Status = 2;
		return true;
	}

	private async void ChoiceClass(object? parameter)
	{
		if (parameter is not Character character) return;
		var dialog = new ChoiceWindow { Type = ChoiceWindow.eType.eClass, ID = character.Class };
		if (await dialog.ShowDialog<bool>(mOwner)) character.Class = dialog.ID;
	}

	private async void AppendItem(object? parameter)
	{
		IReadOnlyList<uint> selectedIDs = await SelectInventoryIDsAsync(ChoiceWindow.eType.eItem);
		if (!EnsureInventoryCapacity(selectedIDs.Count)) return;
		foreach (uint id in selectedIDs)
		{
			var item = CreateInventoryItem(id);
			item.Count = 1;
			Items.Add(item);
		}
		if (selectedIDs.Count > 0) StatusMessage = F("已添加 {0} 条物品记录。", selectedIDs.Count);
		NotifyInventoryChanged();
	}

	private async void AppendEquipment(object? parameter)
	{
		IReadOnlyList<uint> selectedIDs = await SelectInventoryIDsAsync(ChoiceWindow.eType.eEquipment);
		if (!EnsureInventoryCapacity(selectedIDs.Count)) return;
		foreach (uint id in selectedIDs) Equipments.Add(CreateInventoryItem(id));
		if (selectedIDs.Count > 0) StatusMessage = F("已添加 {0} 条装备记录。", selectedIDs.Count);
		NotifyInventoryChanged();
	}

	private async Task<IReadOnlyList<uint>> SelectInventoryIDsAsync(ChoiceWindow.eType type)
	{
		var dialog = new ChoiceWindow { Type = type, AllowMultiple = true };
		if (!await dialog.ShowDialog<bool>(mOwner)) return [];
		return dialog.SelectedIDs.Where(id => id != 0).Distinct().ToArray();
	}

	private Item CreateInventoryItem(uint id)
	{
		uint index = (uint)(Items.Count + Equipments.Count);
		var item = new Item(0xA0 + index * 20)
		{
			ID = id,
			Index = index + 1,
			Status = 2,
		};
		var info = Info.Search(Info.Kind, item.ID);
		if (info != null && uint.TryParse(info.Name, out uint status)) item.Status = status;
		return item;
	}

	private void AppendAllItems(object? parameter)
	{
		HashSet<uint> existingIDs = Items.Select(item => item.ID).ToHashSet();
		uint[] missingIDs = Info.Item
			.Where(info => IsSafeBulkItem(info.Value) && !existingIDs.Contains(info.Value))
			.Select(info => info.Value)
			.Distinct()
			.ToArray();
		if (!EnsureInventoryCapacity(missingIDs.Length)) return;

		foreach (uint id in missingIDs)
		{
			var item = CreateInventoryItem(id);
			item.Count = 1;
			Items.Add(item);
		}
		StatusMessage = missingIDs.Length == 0 ? T("所有安全消耗道具均已存在。") : F("已补齐 {0} 种安全消耗道具，每种数量为 1。", missingIDs.Length);
		NotifyInventoryChanged();
	}

	private void AppendAllEquipment(object? parameter)
	{
		HashSet<uint> existingIDs = Equipments.Select(item => item.ID).ToHashSet();
		uint[] missingIDs = Info.Item
			.Where(info => Info.Search(Info.Kind, info.Value) != null && !existingIDs.Contains(info.Value))
			.Select(info => info.Value)
			.Distinct()
			.ToArray();
		if (!EnsureInventoryCapacity(missingIDs.Length)) return;

		foreach (uint id in missingIDs) Equipments.Add(CreateInventoryItem(id));
		StatusMessage = missingIDs.Length == 0 ? T("所有已知装备均已存在。") : F("已添加 {0} 种当前缺少的装备。", missingIDs.Length);
		NotifyInventoryChanged();
	}

	private bool EnsureInventoryCapacity(int requestedCount)
	{
		if (requestedCount == 0) return true;
		int remainingCount = InventoryCapacity - Items.Count - Equipments.Count;
		if (requestedCount <= remainingCount) return true;
		StatusMessage = F("库存容量不足：需要 {0} 条空记录，当前仅剩 {1} 条。未写入任何记录。", requestedCount, remainingCount);
		return false;
	}

	private static bool IsSafeBulkItem(uint id)
	{
		return id is >= 8 and <= 69 or 71 or >= 73 and <= 171;
	}

	private void NotifyInventoryChanged()
	{
		OnPropertyChanged(nameof(InventorySummary));
	}

	private async void ExportCharacter(object? parameter)
	{
		if (!TryGetSelectedIndex(parameter, out int index)) return;
		try
		{
			var file = await mOwner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
					Title = T("导出角色"),
				FileTypeChoices = [CharacterFileType()],
				DefaultExtension = "uocd",
				SuggestedFileName = "角色.uocd",
			});
			String? filename = file?.TryGetLocalPath();
			if (String.IsNullOrEmpty(filename)) return;

			uint address = Util.calcCharacterAddress((uint)index);
			File.WriteAllBytes(filename, SaveData.Instance().ReadValue(address, 464));
			StatusMessage = T("角色导出成功。");
		}
		catch (Exception exception)
		{
			StatusMessage = F("角色导出失败：{0}", exception.Message);
		}
	}

	private async void ImportCharacter(object? parameter)
	{
		if (!TryGetSelectedIndex(parameter, out int index)) return;
		try
		{
			var files = await mOwner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
					Title = T("用文件替换当前角色"),
				AllowMultiple = false,
				FileTypeFilter = [CharacterFileType()],
			});
			String? filename = files.FirstOrDefault()?.TryGetLocalPath();
			if (String.IsNullOrEmpty(filename)) return;

			Byte[] buffer = File.ReadAllBytes(filename);
			if (buffer.Length != 464)
			{
					StatusMessage = T("替换角色失败：角色数据必须正好为 464 字节。");
				return;
			}

			buffer = ProcessingCharacter(buffer);
			uint address = Util.calcCharacterAddress((uint)index);
			uint id = SaveData.Instance().ReadNumber(address, 4);
			Array.Copy(BitConverter.GetBytes(id), buffer, 4);
			SaveData.Instance().WriteValue(address, buffer);
			Characters[index] = new Character(address);
			StatusMessage = T("当前角色替换成功。");
		}
		catch (Exception exception)
		{
			StatusMessage = F("替换角色失败：{0}", exception.Message);
		}
	}

	private async void InsertCharacter(object? parameter)
	{
		if (Characters.Count >= 500) return;
		try
		{
			var files = await mOwner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
					Title = T("从文件新增角色"),
				AllowMultiple = true,
				FileTypeFilter = [CharacterFileType()],
			});

			int inserted = 0;
			foreach (var file in files)
			{
				if (Characters.Count >= 500) break;
				String? filename = file.TryGetLocalPath();
				if (String.IsNullOrEmpty(filename)) continue;
				Byte[] buffer = File.ReadAllBytes(filename);
				if (buffer.Length != 464) continue;

				buffer = ProcessingCharacter(buffer);
				uint id = SaveData.Instance().ReadNumber(0x63980, 4) + 1;
				Array.Copy(BitConverter.GetBytes(id), buffer, 4);
				uint address = Util.calcCharacterAddress((uint)Characters.Count);
				SaveData.Instance().WriteValue(address, buffer);
				SaveData.Instance().WriteNumber(0x63980, 4, id);
				uint count = SaveData.Instance().ReadNumber(0x63984, 4);
				SaveData.Instance().WriteNumber(0x63984, 4, count + 1);
				InsertFriendship(id);

				var character = new Character(address);
				if (character.ID == 0xFFFFFFFF) continue;
				Characters.Add(character);
				inserted++;
			}
			StatusMessage = F("已新增 {0} 条角色记录。", inserted);
		}
		catch (Exception exception)
		{
			StatusMessage = F("新增角色失败：{0}", exception.Message);
		}
	}

	private void ChangeItemCountMax(object? parameter)
	{
		uint targetCount = (uint)ItemCountTarget;
		foreach (var item in Items)
		{
			if (item.ID > 4) item.Count = targetCount;
		}
		StatusMessage = F("可修改的物品数量已全部设为 {0}。", targetCount);
	}

	private void ChangeCharacterBondMax(object? parameter)
	{
		if (parameter is not Character { Bonds: not null } character) return;
		foreach (var bond in character.Bonds) bond.Value = 1000;
		StatusMessage = T("亲密度已全部设为 1000。");
	}

	private static Byte[] ProcessingCharacter(Byte[] buffer)
	{
		// 清除编队归属和装备引用，避免导入后引用原存档记录。
		Array.Copy(BitConverter.GetBytes(0xFFFFFFFF), 0, buffer, 4, 4);
		buffer[32] = 0xFF;
		buffer[460] &= 0xFE;
		Array.Clear(buffer, 76, 16);
		return buffer;
	}

	private void InsertFriendship(uint id)
	{
		for (uint index = 0; index < 164; index++)
		{
			uint baseAddress = Util.calcBondAddress(index);
			uint currentId = SaveData.Instance().ReadNumber(baseAddress, 4);
			if (currentId == 0xFFFFFFFF)
			{
				SaveData.Instance().WriteNumber(baseAddress, 4, id);
				for (uint count = 0; count < Characters.Count; count++)
				{
					uint address = baseAddress + 4 + count * 8;
					SaveData.Instance().WriteNumber(address, 4, Characters[(int)count].ID);
				}
				return;
			}

			for (uint count = 0; count < 164; count++)
			{
				uint address = baseAddress + 4 + count * 8;
				if (SaveData.Instance().ReadNumber(address, 4) != 0xFFFFFFFF) continue;
				SaveData.Instance().WriteNumber(address, 4, id);
				break;
			}
		}
	}

	private static bool TryGetSelectedIndex(object? parameter, out int index)
	{
		index = parameter == null ? -1 : Convert.ToInt32(parameter);
		return index >= 0;
	}

	private void UpdateActiveFile(String filename)
	{
		CurrentFileName = Path.GetFileName(filename);
		FileLocation = filename;
		IsSaveLoaded = true;
		OnPropertyChanged(nameof(ContextStatusMessage));
	}

	private void RefreshLocalizedCollections()
	{
		ModCatalog.RefreshLocalizedNames();
		TextEditor.RefreshLocale();
		int selectedCharacterIndex = SelectedCharacterIndex;
		Characters = new ObservableCollection<Character>(Characters);
		Items = new ObservableCollection<Item>(Items);
		Equipments = new ObservableCollection<Item>(Equipments);
		OnPropertyChanged(nameof(Characters));
		OnPropertyChanged(nameof(Items));
		OnPropertyChanged(nameof(Equipments));
		OnPropertyChanged(nameof(InventorySummary));
		foreach (ModModule module in ModModules) module.RefreshLocalizedChoices();
		String? selectedCategory = SelectedModCategory?.SourceName;
		ModCategories = CreateModCategories(ModModules);
		SelectedModCategory = ModCategories.FirstOrDefault(category => category.SourceName == selectedCategory) ?? ModCategories.FirstOrDefault();
		OnPropertyChanged(nameof(ModCategories));
		OnPropertyChanged(nameof(ModTargets));
		OnPropertyChanged(nameof(ActiveModTarget));
		SelectedCharacterIndex = selectedCharacterIndex;
	}

	private void SetField<T>(ref T field, T value, String propertyName)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return;
		field = value;
		OnPropertyChanged(propertyName);
	}

	private void OnPropertyChanged(String propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
