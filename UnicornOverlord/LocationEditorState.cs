using System.Diagnostics.CodeAnalysis;

namespace UnicornOverlord;

internal interface ILocationState<TRecord>
{
	ModLocationChoice Choice { get; }
	IReadOnlyList<TRecord> Records { get; }
	void RefreshLocalizedName();
}

internal abstract class LocationEditorState<TLocation, TRecord> : ObservableObject
	where TLocation : class, ILocationState<TRecord>
	where TRecord : class
{
	private TLocation mSelectedLocation;
	private TRecord mSelectedRecord;

	protected LocationEditorState(IReadOnlyList<TLocation> locations)
	{
		if (locations.Count == 0 || locations[0].Records.Count == 0)
			throw new InvalidDataException("编辑器地点目录不能为空。");
		Locations = locations;
		mSelectedLocation = locations[0];
		mSelectedRecord = mSelectedLocation.Records[0];
	}

	public IReadOnlyList<TLocation> Locations { get; }
	public IReadOnlyList<TRecord> RecordsAtLocation => mSelectedLocation.Records;

	[AllowNull]
	public TLocation SelectedLocation
	{
		get => mSelectedLocation;
		set
		{
			if (value == null || ReferenceEquals(mSelectedLocation, value)) return;
			mSelectedLocation = value;
			Notify(nameof(SelectedLocation), nameof(RecordsAtLocation));
			SelectedRecord = value.Records[0];
		}
	}

	[AllowNull]
	public TRecord SelectedRecord
	{
		get => mSelectedRecord;
		set
		{
			if (value == null || ReferenceEquals(mSelectedRecord, value) || !mSelectedLocation.Records.Contains(value)) return;
			mSelectedRecord = value;
			Notify(nameof(SelectedRecord));
			Notify(SelectedRecordPropertyNames);
		}
	}

	protected abstract int RecordId(TRecord record);
	protected abstract String[] SelectedRecordPropertyNames { get; }
	protected virtual String[] LocalizedPropertyNames => [];

	public void SelectLocation(ModLocationChoice? location)
	{
		if (location != null && Locations.FirstOrDefault(item => ReferenceEquals(item.Choice, location) || item.Choice.Key == location.Key) is TLocation selected)
			SelectedLocation = selected;
	}

	public void SelectRecord(ModRecordChoice? choice)
	{
		if (choice != null && RecordsAtLocation.FirstOrDefault(record => RecordId(record) == choice.Value) is TRecord selected)
			SelectedRecord = selected;
	}

	public void RefreshLocalizedChoices()
	{
		foreach (TLocation location in Locations) location.RefreshLocalizedName();
		Notify(nameof(Locations), nameof(SelectedLocation), nameof(SelectedRecord));
		Notify(LocalizedPropertyNames);
	}
}
