using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace UnicornOverlord;

internal sealed class TextEditorViewModel : INotifyPropertyChanged
{
	private const int MaximumResults = 500;
	private static readonly String[] SupportedTables = ["MsgSheet/UcFactorList.fms", "MsgSheet/UcScriptMsgConv.fms"];
	private static readonly Regex ListEntryRegex = new("^(?<index>[0-9]+):\\s+(?<path>.+)$", RegexOptions.Compiled | RegexOptions.Multiline);

	private readonly Window mOwner;
	private readonly Action<String> mSetStatus;
	private String mToolPath = String.Empty;
	private String mSourceCpkPath = String.Empty;
	private String mSearchText = String.Empty;
	private TextModLanguage mSelectedLanguage = TextModLanguage.All[0];
	private ModTarget mSelectedTarget = ModTarget.Asia;
	private TextTable? mSelectedTable;
	private TextEntry? mSelectedEntry;
	private String mValidationMessage = "尚未载入文本归档。";

	public event PropertyChangedEventHandler? PropertyChanged;
	public ICommand ChooseToolCommand { get; }
	public ICommand OpenCpkCommand { get; }
	public ICommand SearchCommand { get; }
	public ICommand ExportCommand { get; }
	public IReadOnlyList<TextModLanguage> Languages { get; } = TextModLanguage.All;
	public IReadOnlyList<ModTarget> Targets { get; } = ModTarget.All;
	public ObservableCollection<TextTable> Tables { get; } = [];
	public ObservableCollection<TextEntry> SearchResults { get; } = [];

	public String ToolPath { get => mToolPath; private set => SetField(ref mToolPath, value, nameof(ToolPath)); }
	public String SourceCpkPath { get => mSourceCpkPath; private set => SetField(ref mSourceCpkPath, value, nameof(SourceCpkPath)); }
	public String SourceSummary => String.IsNullOrEmpty(SourceCpkPath) ? "尚未载入 CPK" : $"{Path.GetFileName(SourceCpkPath)} · {Tables.Count} 张文本表";
	public String ToolSummary => String.IsNullOrEmpty(ToolPath) ? "尚未选择 cricodecs" : ToolPath;
	public String SearchText { get => mSearchText; set => SetField(ref mSearchText, value ?? String.Empty, nameof(SearchText)); }
	public TextModLanguage SelectedLanguage
	{
		get => mSelectedLanguage;
		set
		{
			if (value == null || mSelectedLanguage == value) return;
			mSelectedLanguage = value;
			OnPropertyChanged(nameof(SelectedLanguage));
			OnPropertyChanged(nameof(ExpectedCpkText));
		}
	}
	public String ExpectedCpkText => $"应选择 {SelectedLanguage.CpkFileName}";
	public ModTarget SelectedTarget
	{
		get => mSelectedTarget;
		set => SetField(ref mSelectedTarget, value ?? ModTarget.Asia, nameof(SelectedTarget));
	}
	public TextTable? SelectedTable
	{
		get => mSelectedTable;
		set
		{
			if (mSelectedTable == value) return;
			mSelectedTable = value;
			OnPropertyChanged(nameof(SelectedTable));
			RefreshResults();
		}
	}
	public TextEntry? SelectedEntry
	{
		get => mSelectedEntry;
		set
		{
			if (mSelectedEntry != null) mSelectedEntry.PropertyChanged -= Entry_PropertyChanged;
			mSelectedEntry = value;
			if (mSelectedEntry != null) mSelectedEntry.PropertyChanged += Entry_PropertyChanged;
			OnPropertyChanged(nameof(SelectedEntry));
			UpdateValidation();
		}
	}
	public String ValidationMessage { get => mValidationMessage; private set => SetField(ref mValidationMessage, value, nameof(ValidationMessage)); }
	public int ChangedCount => Tables.Sum(table => table.Document.ChangedCount);
	public String ChangeSummary => $"共修改 {ChangedCount} 项";

	public TextEditorViewModel(Window owner, Action<String> setStatus)
	{
		mOwner = owner;
		mSetStatus = setStatus;
		ChooseToolCommand = new ActionCommand(ChooseTool);
		OpenCpkCommand = new ActionCommand(OpenCpk);
		SearchCommand = new ActionCommand(_ => RefreshResults());
		ExportCommand = new ActionCommand(Export);
		ToolPath = FindToolOnPath() ?? String.Empty;
	}

	private async void ChooseTool(object? parameter)
	{
		var files = await mOwner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = "选择 cricodecs 可执行文件",
			AllowMultiple = false,
		});
		String? path = files.FirstOrDefault()?.TryGetLocalPath();
		if (String.IsNullOrEmpty(path)) return;
		try
		{
			TextModPackageBuilder.RunTool(path, ["--version"]);
			ToolPath = path;
			OnPropertyChanged(nameof(ToolSummary));
			mSetStatus("CPK 工具校验通过。");
		}
		catch (Exception exception)
		{
			mSetStatus($"CPK 工具不可用：{exception.Message}");
		}
	}

	private async void OpenCpk(object? parameter)
	{
		if (String.IsNullOrEmpty(ToolPath))
		{
			mSetStatus("请先选择 cricodecs 可执行文件。");
			return;
		}
		var files = await mOwner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = $"选择 {SelectedLanguage.CpkFileName} 的副本",
			AllowMultiple = false,
			FileTypeFilter = [new FilePickerFileType("CRI CPK 归档") { Patterns = ["*.CPK", "*.cpk"] }],
		});
		String? path = files.FirstOrDefault()?.TryGetLocalPath();
		if (String.IsNullOrEmpty(path)) return;
		if (!String.Equals(Path.GetFileName(path), SelectedLanguage.CpkFileName, StringComparison.OrdinalIgnoreCase))
		{
			mSetStatus($"文件名不匹配：当前语言需要 {SelectedLanguage.CpkFileName}。");
			return;
		}

		String cache = Path.Combine(Path.GetTempPath(), $"uo-text-read-{Guid.NewGuid():N}");
		Directory.CreateDirectory(cache);
		try
		{
			String listing = TextModPackageBuilder.RunTool(ToolPath, [path, "--list"]);
			var indexes = ListEntryRegex.Matches(listing).ToDictionary(
				match => match.Groups["path"].Value.Trim().Replace('\\', '/'),
				match => Int32.Parse(match.Groups["index"].Value), StringComparer.OrdinalIgnoreCase);

			var loaded = new List<TextTable>();
			foreach (String archivePath in SupportedTables)
			{
				if (!indexes.TryGetValue(archivePath, out int archiveIndex))
					throw new InvalidDataException($"CPK 中缺少 {archivePath}。");
				TextModPackageBuilder.RunTool(ToolPath, [path, "--raw", "--index", archiveIndex.ToString(), "-o", Path.Combine(cache, "?e")]);
				String extracted = Path.Combine(cache, Path.GetFileName(archivePath));
				loaded.Add(new TextTable(Path.GetFileNameWithoutExtension(archivePath), archivePath, archiveIndex, FmsDocument.Load(extracted)));
			}

			Tables.Clear();
			foreach (TextTable table in loaded) Tables.Add(table);
			SourceCpkPath = path;
			SelectedTable = Tables.FirstOrDefault();
			OnPropertyChanged(nameof(SourceSummary));
			OnPropertyChanged(nameof(ChangedCount));
			OnPropertyChanged(nameof(ChangeSummary));
			mSetStatus($"文本归档载入成功：共 {Tables.Sum(table => table.Document.Count):N0} 个索引。");
		}
		catch (Exception exception)
		{
			mSetStatus($"载入文本归档失败：{exception.Message}");
		}
		finally
		{
			Directory.Delete(cache, true);
		}
	}

	private async void Export(object? parameter)
	{
		if (String.IsNullOrEmpty(SourceCpkPath) || String.IsNullOrEmpty(ToolPath) || ChangedCount == 0)
		{
			mSetStatus("请先载入 CPK 并修改至少一个文本条目。");
			return;
		}
		ModTarget target = SelectedTarget;
		var file = await mOwner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
		{
			Title = "导出文本 MOD 包",
			DefaultExtension = "zip",
			SuggestedFileName = $"UnicornOverlord-{target.Key}-{SelectedLanguage.CpkFileName[..^4]}-TextMod.zip",
			FileTypeChoices = [new FilePickerFileType("ZIP 压缩包") { Patterns = ["*.zip"] }],
		});
		String? path = file?.TryGetLocalPath();
		if (String.IsNullOrEmpty(path)) return;
		try
		{
			TextModPackageBuilder.Create(path, ToolPath, SourceCpkPath, SelectedLanguage, target, Tables);
			mSetStatus($"文本 MOD 导出成功：{ChangedCount} 项修改，目标 {target.DisplayName}。");
		}
		catch (Exception exception)
		{
			mSetStatus($"文本 MOD 导出失败：{exception.Message}");
		}
	}

	private void RefreshResults()
	{
		SearchResults.Clear();
		SelectedEntry = null;
		if (SelectedTable == null) return;
		FmsDocument document = SelectedTable.Document;
		String query = SearchText.Trim();
		IEnumerable<int> indexes;
		if (Int32.TryParse(query, out int exactIndex))
			indexes = exactIndex >= 0 && exactIndex < document.Count ? [exactIndex] : [];
		else if (String.IsNullOrEmpty(query))
			indexes = Enumerable.Range(0, document.Count).Where(index => document.IsChanged(index) || !String.IsNullOrEmpty(document.GetText(index)));
		else
			indexes = Enumerable.Range(0, document.Count).Where(index => document.GetText(index).Contains(query, StringComparison.CurrentCultureIgnoreCase));

		foreach (int index in indexes.Take(MaximumResults)) SearchResults.Add(new TextEntry(document, index));
		SelectedEntry = SearchResults.FirstOrDefault();
		mSetStatus(SearchResults.Count == MaximumResults ? "显示前 500 项，请缩小搜索范围。" : $"找到 {SearchResults.Count} 项。");
	}

	private void Entry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(TextEntry.Text)) return;
		UpdateValidation();
		OnPropertyChanged(nameof(ChangedCount));
		OnPropertyChanged(nameof(ChangeSummary));
		SelectedTable?.NotifyChanged();
	}

	private void UpdateValidation()
	{
		if (SelectedEntry == null)
		{
			ValidationMessage = "请选择一个文本条目。";
			return;
		}
		String originalTokens = String.Join(' ', ExtractMarkupTokens(SelectedEntry.OriginalText));
		String editedTokens = String.Join(' ', ExtractMarkupTokens(SelectedEntry.Text));
		ValidationMessage = originalTokens == editedTokens
			? "格式标记与运行时占位符保持一致。"
			: "注意：格式标记或运行时占位符已变化，请确认这是有意修改。";
	}

	private static IEnumerable<String> ExtractMarkupTokens(String text)
	{
		return Regex.Matches(text, "%%|%s|#\\([^)]*\\)|#/?[a-zA-Z](?:\\([^)]*\\))?")
			.Select(match => match.Value);
	}

	private static String? FindToolOnPath()
	{
		String? path = Environment.GetEnvironmentVariable("PATH");
		if (String.IsNullOrEmpty(path)) return null;
		foreach (String directory in path.Split(Path.PathSeparator))
		{
			String candidate = Path.Combine(directory, OperatingSystem.IsWindows() ? "cricodecs.exe" : "cricodecs");
			if (File.Exists(candidate)) return candidate;
		}
		return null;
	}

	private void SetField<T>(ref T field, T value, String propertyName)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return;
		field = value;
		OnPropertyChanged(propertyName);
	}

	private void OnPropertyChanged(String propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
