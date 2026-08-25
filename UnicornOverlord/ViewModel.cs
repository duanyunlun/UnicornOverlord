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

	private static readonly FilePickerFileType SaveFileType = new("独角兽之王存档")
	{
		Patterns = ["UCSAVEFILE*.DAT", "*.DAT", "*.dat"],
	};

	private static readonly FilePickerFileType CharacterFileType = new("独角兽之王角色数据")
	{
		Patterns = ["*.uocd"],
	};
	private static readonly FilePickerFileType ModPackageFileType = new("ZIP 压缩包")
	{
		Patterns = ["*.zip"],
	};

	private readonly Window mOwner;
	private readonly Info Info = Info.Instance();
	private bool mIsSaveLoaded;
	private String mCurrentFileName = "尚未打开存档";
	private String mFileLocation = "当前没有活动文件";
	private String mStatusMessage;
	private int mLanguageIndex;
	private int mSelectedCharacterIndex = -1;
	private int mItemCountTarget = 99;

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

	public Basic Basic { get; } = new();
	public ObservableCollection<Character> Characters { get; private set; } = [];
	public ObservableCollection<Item> Items { get; private set; } = [];
	public ObservableCollection<Item> Equipments { get; private set; } = [];
	public ObservableCollection<Unit> Units { get; } = [];
	public ObservableCollection<ModModule> ModModules { get; } = CreateModModules();
	public IReadOnlyList<String> Languages { get; } = ["英文", "日文", "简体中文"];
	public String InventorySummary => $"物品 {Items.Count} 条，装备 {Equipments.Count} 条，共 {Items.Count + Equipments.Count} / {InventoryCapacity} 条库存记录";
	public String ModTargetSummary => $"{ModPackageBuilder.GameVersion} · Title ID {ModPackageBuilder.TitleId} · Build ID {ModPackageBuilder.BuildId}";
	public int SelectedModCount => ModModules.Count(module => module.IsSelected);
	public bool CanExportMods => SelectedModCount > 0;
	public String ModSelectionSummary => CanExportMods ? $"已选择 {SelectedModCount} 个可用模块" : "请选择至少一个已接入模块";

	public bool IsSaveLoaded
	{
		get => mIsSaveLoaded;
		private set => SetField(ref mIsSaveLoaded, value, nameof(IsSaveLoaded));
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

	public int LanguageIndex
	{
		get => mLanguageIndex;
		set
		{
			int normalized = Math.Clamp(value, 0, Languages.Count - 1);
			if (mLanguageIndex == normalized) return;
			mLanguageIndex = normalized;
			ApplicationSettings.Language = normalized;
			RefreshLocalizedCollections();
			OnPropertyChanged(nameof(LanguageIndex));
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
		mLanguageIndex = ApplicationSettings.Language;
		mStatusMessage = Info.Item.Count == 0
			? "未找到名称数据，未知条目将显示为数字 ID。"
			: "就绪";

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
		foreach (ModModule module in ModModules) module.PropertyChanged += ModModule_PropertyChanged;
	}

	private static ObservableCollection<ModModule> CreateModModules()
	{
		return
		[
			new() { Key = "ability_editor", Category = "技能", Name = "技能编辑器", Description = "修改技能 AP/PP 消耗、威力、命中与目标范围。", IsAvailable = false },
			new() { Key = "battle_preview", Category = "战斗", Name = "战斗预览调整", Description = "降低战斗预览的确定性，或隐藏预览结果。", IsAvailable = false },
			new() { Key = "battle_timer_freeze", Category = "战斗", Name = "冻结战斗计时器", Description = "冻结关卡实时计时器，战斗不再受时间限制。", IsAvailable = true, TemplateFile = "battle_timer_freeze.pchtxt" },
			new() { Key = "character_randomizer", Category = "角色", Name = "角色加入随机化", Description = "随机调整剧情中角色的加入顺序。", IsAvailable = false, Warning = "实验性功能，只适用于新游戏。" },
			new() { Key = "class_growth_safe", Category = "职业", Name = "职业成长保底", Description = "保留职业成长差异，并让 Lv1 至 Lv50 每次升级的十项能力至少增加 1。", IsAvailable = true, TemplateFile = "class_growth_safe.pchtxt" },
			new() { Key = "fort_editor", Category = "据点", Name = "据点雇佣编辑器", Description = "调整各据点雇佣公会可招募的泛用职业。", IsAvailable = false },
			new() { Key = "mine_editor", Category = "采矿", Name = "采矿掉落编辑器", Description = "调整矿场掉落权重、挖掘目标与单局掉落上限。", IsAvailable = false },
			new() { Key = "shop_editor", Category = "商店", Name = "商店库存编辑器", Description = "调整商店商品、库存数量和价格。", IsAvailable = false },
			new() { Key = "six_member_units", Category = "编队", Name = "六人编队", Description = "允许 S 级声望下将部队扩充至六人。", IsAvailable = false, Warning = "移除 MOD 前必须先撤下实际的第六名成员。" },
			new() { Key = "text_editor", Category = "文本", Name = "文本编辑器", Description = "修改对话、技能条件文本与泛用角色姓名池。", IsAvailable = false },
		];
	}

	private void ModModule_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(ModModule.IsSelected)) return;
		OnPropertyChanged(nameof(SelectedModCount));
		OnPropertyChanged(nameof(CanExportMods));
		OnPropertyChanged(nameof(ModSelectionSummary));
	}

	private void InitializeData()
	{
		SelectedCharacterIndex = -1;
		Characters.Clear();
		Items.Clear();
		Equipments.Clear();
		Units.Clear();

		var bondDictionary = new Dictionary<uint, ObservableCollection<Bond>>();
		for (uint index = 0; index < 164; index++)
		{
			uint baseAddress = Util.calcBondAddress(index);
			uint id = SaveData.Instance().ReadNumber(baseAddress, 4);
			if (id == 0xFFFFFFFF) break;

			var bonds = new ObservableCollection<Bond>();
			bondDictionary.TryAdd(id, bonds);
			for (uint count = 0; count < 164; count++)
			{
				uint address = baseAddress + 4 + count * 8;
				id = SaveData.Instance().ReadNumber(address, 4);
				if (id == 0xFFFFFFFF) break;
				bonds.Add(new Bond(address));
			}
		}

		for (uint index = 0; index < 500; index++)
		{
			var character = new Character(Util.calcCharacterAddress(index));
			if (character.ID == 0xFFFFFFFF) break;
			if (bondDictionary.TryGetValue(character.ID, out var bonds)) character.Bonds = bonds;
			Characters.Add(character);
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
		try
		{
			var files = await mOwner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = "打开独角兽之王存档",
				AllowMultiple = false,
				FileTypeFilter = [SaveFileType],
			});
			String? filename = files.FirstOrDefault()?.TryGetLocalPath();
			if (String.IsNullOrEmpty(filename)) return;

			if (!SaveData.Instance().Open(filename))
			{
				StatusMessage = "所选文件不是受支持的独角兽之王存档。";
				return;
			}

			InitializeData();
			UpdateActiveFile(filename);
			StatusMessage = "存档已载入，并已在源文件旁创建备份。";
		}
		catch (Exception exception)
		{
			StatusMessage = $"打开失败：{exception.Message}";
		}
	}

	private void SaveFile(object? parameter)
	{
		try
		{
			StatusMessage = SaveData.Instance().Save() ? "存档保存成功。" : "当前未载入存档。";
		}
		catch (Exception exception)
		{
			StatusMessage = $"保存失败：{exception.Message}";
		}
	}

	private async void SaveAsFile(object? parameter)
	{
		if (!IsSaveLoaded) return;
		try
		{
			var file = await mOwner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = "将独角兽之王存档另存为",
				FileTypeChoices = [SaveFileType],
				DefaultExtension = "DAT",
				SuggestedFileName = CurrentFileName,
			});
			String? filename = file?.TryGetLocalPath();
			if (String.IsNullOrEmpty(filename)) return;

			if (SaveData.Instance().SaveAs(filename))
			{
				UpdateActiveFile(filename);
				StatusMessage = "存档副本保存成功。";
			}
		}
		catch (Exception exception)
		{
			StatusMessage = $"另存为失败：{exception.Message}";
		}
	}

	private async void ExportModPackage(object? parameter)
	{
		ModModule[] selectedModules = ModModules.Where(module => module.IsSelected && module.IsAvailable).ToArray();
		if (selectedModules.Length == 0)
		{
			StatusMessage = "请至少选择一个已接入的 MOD 模块。";
			return;
		}

		try
		{
			var file = await mOwner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = "导出独角兽之王 MOD 包",
				FileTypeChoices = [ModPackageFileType],
				DefaultExtension = "zip",
				SuggestedFileName = "UnicornOverlord-v1.0.5-Mods.zip",
			});
			String? filename = file?.TryGetLocalPath();
			if (String.IsNullOrEmpty(filename)) return;

			ModPackageBuilder.Create(filename, selectedModules);
			StatusMessage = $"MOD 包导出成功：包含 {selectedModules.Length} 个模块。";
		}
		catch (Exception exception)
		{
			StatusMessage = $"MOD 包导出失败：{exception.Message}";
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
		if (selectedIDs.Count > 0) StatusMessage = $"已添加 {selectedIDs.Count} 条物品记录。";
		NotifyInventoryChanged();
	}

	private async void AppendEquipment(object? parameter)
	{
		IReadOnlyList<uint> selectedIDs = await SelectInventoryIDsAsync(ChoiceWindow.eType.eEquipment);
		if (!EnsureInventoryCapacity(selectedIDs.Count)) return;
		foreach (uint id in selectedIDs) Equipments.Add(CreateInventoryItem(id));
		if (selectedIDs.Count > 0) StatusMessage = $"已添加 {selectedIDs.Count} 条装备记录。";
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
		StatusMessage = missingIDs.Length == 0 ? "所有安全消耗道具均已存在。" : $"已补齐 {missingIDs.Length} 种安全消耗道具，每种数量为 1。";
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
		StatusMessage = missingIDs.Length == 0 ? "所有已知装备均已存在。" : $"已添加 {missingIDs.Length} 种当前缺少的装备。";
		NotifyInventoryChanged();
	}

	private bool EnsureInventoryCapacity(int requestedCount)
	{
		if (requestedCount == 0) return true;
		int remainingCount = InventoryCapacity - Items.Count - Equipments.Count;
		if (requestedCount <= remainingCount) return true;
		StatusMessage = $"库存容量不足：需要 {requestedCount} 条空记录，当前仅剩 {remainingCount} 条。未写入任何记录。";
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
				Title = "导出角色",
				FileTypeChoices = [CharacterFileType],
				DefaultExtension = "uocd",
				SuggestedFileName = "角色.uocd",
			});
			String? filename = file?.TryGetLocalPath();
			if (String.IsNullOrEmpty(filename)) return;

			uint address = Util.calcCharacterAddress((uint)index);
			File.WriteAllBytes(filename, SaveData.Instance().ReadValue(address, 464));
			StatusMessage = "角色导出成功。";
		}
		catch (Exception exception)
		{
			StatusMessage = $"角色导出失败：{exception.Message}";
		}
	}

	private async void ImportCharacter(object? parameter)
	{
		if (!TryGetSelectedIndex(parameter, out int index)) return;
		try
		{
			var files = await mOwner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = "用文件替换当前角色",
				AllowMultiple = false,
				FileTypeFilter = [CharacterFileType],
			});
			String? filename = files.FirstOrDefault()?.TryGetLocalPath();
			if (String.IsNullOrEmpty(filename)) return;

			Byte[] buffer = File.ReadAllBytes(filename);
			if (buffer.Length != 464)
			{
				StatusMessage = "替换角色失败：角色数据必须正好为 464 字节。";
				return;
			}

			buffer = ProcessingCharacter(buffer);
			uint address = Util.calcCharacterAddress((uint)index);
			uint id = SaveData.Instance().ReadNumber(address, 4);
			Array.Copy(BitConverter.GetBytes(id), buffer, 4);
			SaveData.Instance().WriteValue(address, buffer);
			Characters[index] = new Character(address);
			StatusMessage = "当前角色替换成功。";
		}
		catch (Exception exception)
		{
			StatusMessage = $"替换角色失败：{exception.Message}";
		}
	}

	private async void InsertCharacter(object? parameter)
	{
		if (Characters.Count >= 500) return;
		try
		{
			var files = await mOwner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = "从文件新增角色",
				AllowMultiple = true,
				FileTypeFilter = [CharacterFileType],
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
			StatusMessage = $"已新增 {inserted} 条角色记录。";
		}
		catch (Exception exception)
		{
			StatusMessage = $"新增角色失败：{exception.Message}";
		}
	}

	private void ChangeItemCountMax(object? parameter)
	{
		uint targetCount = (uint)ItemCountTarget;
		foreach (var item in Items)
		{
			if (item.ID > 4) item.Count = targetCount;
		}
		StatusMessage = $"可修改的物品数量已全部设为 {targetCount}。";
	}

	private void ChangeCharacterBondMax(object? parameter)
	{
		if (parameter is not Character { Bonds: not null } character) return;
		foreach (var bond in character.Bonds) bond.Value = 1000;
		StatusMessage = "亲密度已全部设为 1000。";
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
	}

	private void RefreshLocalizedCollections()
	{
		int selectedCharacterIndex = SelectedCharacterIndex;
		Characters = new ObservableCollection<Character>(Characters);
		Items = new ObservableCollection<Item>(Items);
		Equipments = new ObservableCollection<Item>(Equipments);
		OnPropertyChanged(nameof(Characters));
		OnPropertyChanged(nameof(Items));
		OnPropertyChanged(nameof(Equipments));
		OnPropertyChanged(nameof(InventorySummary));
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
