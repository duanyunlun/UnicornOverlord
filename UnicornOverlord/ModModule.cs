using System.ComponentModel;

namespace UnicornOverlord;

internal sealed class ModModule : INotifyPropertyChanged
{
	private bool mIsSelected;

	public event PropertyChangedEventHandler? PropertyChanged;

	public required String Key { get; init; }
	public required String Category { get; init; }
	public required String Name { get; init; }
	public required String Description { get; init; }
	public required bool IsAvailable { get; init; }
	public String? TemplateFile { get; init; }
	public String? Warning { get; init; }
	public String StateText => IsAvailable ? "已接入" : "待解析";

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
}
