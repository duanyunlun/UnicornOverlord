using System.ComponentModel;

namespace UnicornOverlord;

internal sealed class MineRecordEdit : INotifyPropertyChanged
{
	private int mItemId;
	private int mWeight;
	private int mDigTarget;
	private int mRoundLimit;

	public MineRecordEdit(ModRecordChoice choice, ModRecordInfo original)
	{
		Choice = choice;
		Original = original;
		mItemId = original.ValueA;
		mWeight = original.ValueB;
		mDigTarget = original.ValueC;
		mRoundLimit = original.ValueE;
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	public ModRecordChoice Choice { get; }
	public ModRecordInfo Original { get; }
	public int RecordId => Choice.Value;
	public String DetailDisplayName => Choice.DetailDisplayName;
	public int ItemId { get => mItemId; set => SetField(ref mItemId, value, nameof(ItemId)); }
	public int Weight { get => mWeight; set => SetField(ref mWeight, value, nameof(Weight)); }
	public int DigTarget { get => mDigTarget; set => SetField(ref mDigTarget, value, nameof(DigTarget)); }
	public int RoundLimit { get => mRoundLimit; set => SetField(ref mRoundLimit, value, nameof(RoundLimit)); }
	public bool IsModified => ItemId != Original.ValueA || Weight != Original.ValueB || DigTarget != Original.ValueC || RoundLimit != Original.ValueE;

	public void RefreshLocalizedName() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DetailDisplayName)));

	private void SetField<T>(ref T field, T value, String propertyName)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return;
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsModified)));
	}
}

internal sealed class MineLocationState : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;
	public required ModLocationChoice Choice { get; init; }
	public required IReadOnlyList<MineRecordEdit> Records { get; init; }
	public String DisplayName => Choice.DisplayName;

	public void RefreshLocalizedName()
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
		foreach (MineRecordEdit record in Records) record.RefreshLocalizedName();
	}
}

internal sealed class MineEditorState : INotifyPropertyChanged
{
	private MineLocationState? mSelectedLocation;
	private MineRecordEdit? mSelectedRecord;

	public MineEditorState()
	{
		var edits = ModCatalog.MineRecordChoices.ToDictionary(
			choice => choice.Value,
			choice => new MineRecordEdit(choice, ModCatalog.MineRecords[choice.Value]));
		Locations = ModCatalog.MineLocations.Select(location => new MineLocationState
		{
			Choice = location,
			Records = ModCatalog.MineRecordChoices
				.Where(record => record.LocationKey == location.Key)
				.Select(record => edits[record.Value])
				.ToArray(),
		}).ToArray();
		mSelectedLocation = Locations.FirstOrDefault();
		mSelectedRecord = mSelectedLocation?.Records.FirstOrDefault();
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	public IReadOnlyList<MineLocationState> Locations { get; }
	public IEnumerable<MineRecordEdit> ModifiedRecords => Locations.SelectMany(location => location.Records).Where(record => record.IsModified);
	public int ModifiedCount => ModifiedRecords.Count();

	public MineLocationState? SelectedLocation
	{
		get => mSelectedLocation;
		set
		{
			if (value == null || ReferenceEquals(mSelectedLocation, value)) return;
			mSelectedLocation = value;
			Notify(nameof(SelectedLocation));
			SelectedRecord = value.Records.FirstOrDefault();
		}
	}

	public MineRecordEdit? SelectedRecord
	{
		get => mSelectedRecord;
		set
		{
			if (value == null || ReferenceEquals(mSelectedRecord, value)) return;
			mSelectedRecord = value;
			Notify(nameof(SelectedRecord), nameof(SelectedItem), nameof(Weight), nameof(DigTarget), nameof(RoundLimit));
		}
	}

	public ModChoice? SelectedItem
	{
		get => mSelectedRecord == null ? null : ModCatalog.FindItem(mSelectedRecord.ItemId);
		set
		{
			if (mSelectedRecord == null || value == null || mSelectedRecord.ItemId == value.Value) return;
			mSelectedRecord.ItemId = value.Value;
			Notify(nameof(SelectedItem), nameof(ModifiedCount));
		}
	}

	public int Weight
	{
		get => mSelectedRecord?.Weight ?? 0;
		set => UpdateSelected(record => record.Weight = value, nameof(Weight));
	}

	public int DigTarget
	{
		get => mSelectedRecord?.DigTarget ?? 0;
		set => UpdateSelected(record => record.DigTarget = value, nameof(DigTarget));
	}

	public int RoundLimit
	{
		get => mSelectedRecord?.RoundLimit ?? 1;
		set => UpdateSelected(record => record.RoundLimit = value, nameof(RoundLimit));
	}

	public void RefreshLocalizedChoices()
	{
		foreach (MineLocationState location in Locations) location.RefreshLocalizedName();
		Notify(nameof(Locations), nameof(SelectedLocation), nameof(SelectedRecord), nameof(SelectedItem));
	}

	private void UpdateSelected(Action<MineRecordEdit> update, String propertyName)
	{
		if (mSelectedRecord == null) return;
		update(mSelectedRecord);
		Notify(propertyName, nameof(ModifiedCount));
	}

	private void Notify(params String[] propertyNames)
	{
		foreach (String propertyName in propertyNames) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
