using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;

namespace UnicornOverlord;

internal class ViewModel : INotifyPropertyChanged
{
	private static readonly FilePickerFileType SaveFileType = new("独角兽之王存档")
	{
		Patterns = ["UCSAVEFILE*.DAT", "*.DAT", "*.dat"],
	};

	private static readonly FilePickerFileType CharacterFileType = new("独角兽之王角色数据")
	{
		Patterns = ["*.uocd"],
	};

	private readonly Window mOwner;
	private readonly Info Info = Info.Instance();
	private bool mIsSaveLoaded;
	private String mCurrentFileName = "尚未打开存档";
	private String mFileLocation = "当前没有活动文件";
	private String mStatusMessage;
	private int mLanguageIndex;

	public event PropertyChangedEventHandler? PropertyChanged;

	public ICommand OpenFileCommand { get; }
	public ICommand SaveFileCommand { get; }
	public ICommand SaveAsFileCommand { get; }
	public ICommand ChoiceItemCommand { get; }
	public ICommand ChoiceEquipmentCommand { get; }
	public ICommand ChoiceClassCommand { get; }
	public ICommand AppendItemCommand { get; }
	public ICommand AppendEquipmentCommand { get; }
	public ICommand ExportCharacterCommand { get; }
	public ICommand ImportCharacterCommand { get; }
	public ICommand InsertCharacterCommand { get; }
	public ICommand ChangeItemCountMaxCommand { get; }
	public ICommand ChangeCharacterBondMaxCommand { get; }

	public Basic Basic { get; } = new();
	public ObservableCollection<Character> Characters { get; private set; } = [];
	public ObservableCollection<Item> Items { get; private set; } = [];
	public ObservableCollection<Item> Equipments { get; private set; } = [];
	public ObservableCollection<Unit> Units { get; } = [];
	public IReadOnlyList<String> Languages { get; } = ["英文", "日文", "简体中文"];

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
		ExportCharacterCommand = new ActionCommand(ExportCharacter);
		ImportCharacterCommand = new ActionCommand(ImportCharacter);
		InsertCharacterCommand = new ActionCommand(InsertCharacter);
		ChangeItemCountMaxCommand = new ActionCommand(ChangeItemCountMax);
		ChangeCharacterBondMaxCommand = new ActionCommand(ChangeCharacterBondMax);
	}

	private void InitializeData()
	{
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

		for (uint index = 0; index < 3800; index++)
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
		var item = await AppendItemAsync(ChoiceWindow.eType.eItem);
		if (item == null) return;
		item.Count = 1;
		Items.Add(item);
	}

	private async void AppendEquipment(object? parameter)
	{
		var item = await AppendItemAsync(ChoiceWindow.eType.eEquipment);
		if (item != null) Equipments.Add(item);
	}

	private async Task<Item?> AppendItemAsync(ChoiceWindow.eType type)
	{
		uint index = (uint)(Items.Count + Equipments.Count);
		if (index >= 3800)
		{
			StatusMessage = "物品栏记录已达到上限。";
			return null;
		}

		var dialog = new ChoiceWindow { Type = type };
		if (!await dialog.ShowDialog<bool>(mOwner) || dialog.ID == 0) return null;

		var item = new Item(0xA0 + index * 20)
		{
			ID = dialog.ID,
			Index = index + 1,
			Status = 2,
		};
		var info = Info.Search(Info.Kind, item.ID);
		if (info != null && uint.TryParse(info.Name, out uint status)) item.Status = status;
		return item;
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
				Title = "导入角色",
				AllowMultiple = false,
				FileTypeFilter = [CharacterFileType],
			});
			String? filename = files.FirstOrDefault()?.TryGetLocalPath();
			if (String.IsNullOrEmpty(filename)) return;

			Byte[] buffer = File.ReadAllBytes(filename);
			if (buffer.Length != 464)
			{
				StatusMessage = "角色导入失败：角色数据必须正好为 464 字节。";
				return;
			}

			buffer = ProcessingCharacter(buffer);
			uint address = Util.calcCharacterAddress((uint)index);
			uint id = SaveData.Instance().ReadNumber(address, 4);
			Array.Copy(BitConverter.GetBytes(id), buffer, 4);
			SaveData.Instance().WriteValue(address, buffer);
			Characters[index] = new Character(address);
			StatusMessage = "角色导入成功。";
		}
		catch (Exception exception)
		{
			StatusMessage = $"角色导入失败：{exception.Message}";
		}
	}

	private async void InsertCharacter(object? parameter)
	{
		if (Characters.Count >= 500) return;
		try
		{
			var files = await mOwner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = "插入角色",
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
			StatusMessage = $"已插入 {inserted} 条角色记录。";
		}
		catch (Exception exception)
		{
			StatusMessage = $"插入角色失败：{exception.Message}";
		}
	}

	private void ChangeItemCountMax(object? parameter)
	{
		foreach (var item in Items)
		{
			if (item.ID > 4) item.Count = 99;
		}
		StatusMessage = "可修改的物品数量已全部设为 99。";
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
		Characters = new ObservableCollection<Character>(Characters);
		Items = new ObservableCollection<Item>(Items);
		Equipments = new ObservableCollection<Item>(Equipments);
		OnPropertyChanged(nameof(Characters));
		OnPropertyChanged(nameof(Items));
		OnPropertyChanged(nameof(Equipments));
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
