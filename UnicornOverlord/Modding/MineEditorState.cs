namespace UnicornOverlord;

internal sealed class MineRecordEdit : ObservableObject
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

	public ModRecordChoice Choice { get; }
	public ModRecordInfo Original { get; }
	public int RecordId => Choice.Value;
	public String DetailDisplayName => Choice.DetailDisplayName;
	public int ItemId { get => mItemId; set => SetField(ref mItemId, value, nameof(ItemId), nameof(IsModified)); }
	public int Weight { get => mWeight; set => SetField(ref mWeight, value, nameof(Weight), nameof(IsModified)); }
	public int DigTarget { get => mDigTarget; set => SetField(ref mDigTarget, value, nameof(DigTarget), nameof(IsModified)); }
	public int RoundLimit { get => mRoundLimit; set => SetField(ref mRoundLimit, value, nameof(RoundLimit), nameof(IsModified)); }
	public bool IsModified => ItemId != Original.ValueA || Weight != Original.ValueB || DigTarget != Original.ValueC || RoundLimit != Original.ValueE;

	public void RefreshLocalizedName() => Notify(nameof(DetailDisplayName));
}

internal sealed class MineLocationState : ObservableObject, ILocationState<MineRecordEdit>
{
	public required ModLocationChoice Choice { get; init; }
	public required IReadOnlyList<MineRecordEdit> Records { get; init; }
	public String DisplayName => Choice.DisplayName;

	public void RefreshLocalizedName()
	{
		Notify(nameof(DisplayName));
		foreach (MineRecordEdit record in Records) record.RefreshLocalizedName();
	}
}

internal sealed class MineEditorState : LocationEditorState<MineLocationState, MineRecordEdit>
{
	private static readonly String[] SelectionProperties = [nameof(SelectedItem), nameof(Weight), nameof(DigTarget), nameof(RoundLimit)];

	public MineEditorState() : base(CreateLocations()) { }

	public IEnumerable<MineRecordEdit> ModifiedRecords => Locations.SelectMany(location => location.Records).Where(record => record.IsModified);
	public int ModifiedCount => ModifiedRecords.Count();

	public ModChoice? SelectedItem
	{
		get => ModCatalog.FindItem(SelectedRecord.ItemId);
		set
		{
			if (value == null || SelectedRecord.ItemId == value.Value) return;
			SelectedRecord.ItemId = value.Value;
			Notify(nameof(SelectedItem), nameof(ModifiedCount));
		}
	}

	public int Weight
	{
		get => SelectedRecord.Weight;
		set => UpdateSelected(record => record.Weight = value, nameof(Weight));
	}

	public int DigTarget
	{
		get => SelectedRecord.DigTarget;
		set => UpdateSelected(record => record.DigTarget = value, nameof(DigTarget));
	}

	public int RoundLimit
	{
		get => SelectedRecord.RoundLimit;
		set => UpdateSelected(record => record.RoundLimit = value, nameof(RoundLimit));
	}

	protected override int RecordId(MineRecordEdit record) => record.RecordId;
	protected override String[] SelectedRecordPropertyNames => SelectionProperties;
	protected override String[] LocalizedPropertyNames => [nameof(SelectedItem)];

	private static IReadOnlyList<MineLocationState> CreateLocations()
	{
		var edits = ModCatalog.MineRecordChoices.ToDictionary(
			choice => choice.Value,
			choice => new MineRecordEdit(choice, ModCatalog.MineRecords[choice.Value]));
		return ModCatalog.MineLocations.Select(location => new MineLocationState
		{
			Choice = location,
			Records = ModCatalog.MineRecordChoices
				.Where(record => record.LocationKey == location.Key)
				.Select(record => edits[record.Value])
				.ToArray(),
		}).ToArray();
	}

	private void UpdateSelected(Action<MineRecordEdit> update, String propertyName)
	{
		update(SelectedRecord);
		Notify(propertyName, nameof(ModifiedCount));
	}
}
