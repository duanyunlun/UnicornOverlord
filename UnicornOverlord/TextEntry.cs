using System.ComponentModel;

namespace UnicornOverlord;

internal sealed class TextEntry : INotifyPropertyChanged
{
	private readonly FmsDocument mDocument;
	private String mText;

	public event PropertyChangedEventHandler? PropertyChanged;
	public int Index { get; }
	public String OriginalText => mDocument.GetOriginalText(Index);
	public bool IsChanged => mDocument.IsChanged(Index);
	public String StateText => IsChanged ? "已修改" : "原文";

	public String Text
	{
		get => mText;
		set
		{
			if (mText == value) return;
			mDocument.SetText(Index, value ?? String.Empty);
			mText = value ?? String.Empty;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChanged)));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateText)));
		}
	}

	public TextEntry(FmsDocument document, int index)
	{
		mDocument = document;
		Index = index;
		mText = document.GetText(index);
	}
}
