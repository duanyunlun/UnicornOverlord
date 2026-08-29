using System.ComponentModel;

namespace UnicornOverlord;

internal sealed class TextTable : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;
	public String Name { get; }
	public String ArchivePath { get; }
	public int ArchiveIndex { get; }
	public FmsDocument Document { get; }
	public String Summary => LocaleManager.Instance.Format("{0} · {1:N0} 项 · 已修改 {2} 项", Name, Document.Count, Document.ChangedCount);

	public TextTable(String name, String archivePath, int archiveIndex, FmsDocument document)
	{
		Name = name;
		ArchivePath = archivePath;
		ArchiveIndex = archiveIndex;
		Document = document;
	}

	public void NotifyChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
}
