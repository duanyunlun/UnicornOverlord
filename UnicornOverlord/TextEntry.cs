namespace UnicornOverlord;

internal sealed class TextEntry : ObservableObject
{
	private readonly FmsDocument mDocument;
	private String mText;

	public int Index { get; }
	public String OriginalText => mDocument.GetOriginalText(Index);
	public bool IsChanged => mDocument.IsChanged(Index);
	public String StateText => LocaleManager.Instance.Translate(IsChanged ? "已修改" : "原文");

	public String Text
	{
		get => mText;
		set
		{
			if (mText == value) return;
			mDocument.SetText(Index, value ?? String.Empty);
			mText = value ?? String.Empty;
			Notify(nameof(Text), nameof(IsChanged), nameof(StateText));
		}
	}

	public TextEntry(FmsDocument document, int index)
	{
		mDocument = document;
		Index = index;
		mText = document.GetText(index);
	}

	public void RefreshLocale() => Notify(nameof(StateText));
}
