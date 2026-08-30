using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace UnicornOverlord;

internal sealed class TextEditorViewModel : ObservableObject
{
	private const int MaximumResults = 500;
	private static readonly String[] SupportedTables = ["MsgSheet/UcFactorList.fms", "MsgSheet/UcScriptMsgConv.fms"];
	private static readonly Regex ListEntryRegex = new("^(?<index>[0-9]+):\\s+(?<path>.+)$", RegexOptions.Compiled | RegexOptions.Multiline);

	private readonly Window mOwner;
	private readonly Action<String> mSetStatus;
	private readonly ModProjectState mProject;
	private readonly TextModProjectState mState;
	private String mSearchText = String.Empty;
	private TextTable? mSelectedTable;
	private TextEntry? mSelectedEntry;
	private String mValidationMessage = "尚未载入文本归档。";

	public ICommand ChooseToolCommand { get; }
	public ICommand OpenCpkCommand { get; }
	public ICommand SearchCommand { get; }
	public ICommand ExportCommand { get; }
	public IReadOnlyList<TextModLanguage> Languages { get; } = TextModLanguage.All;
	public IReadOnlyList<ModTarget> Targets { get; } = ModTarget.All;
	public ObservableCollection<TextTable> Tables => mState.Tables;
	public ObservableCollection<TextEntry> SearchResults { get; } = [];

	public String ToolPath { get => mState.ToolPath; private set { if (mState.ToolPath == value) return; mState.ToolPath = value; OnPropertyChanged(nameof(ToolPath)); } }
	public String SourceCpkPath { get => mState.SourceCpkPath; private set { if (mState.SourceCpkPath == value) return; mState.SourceCpkPath = value; OnPropertyChanged(nameof(SourceCpkPath)); } }
	public String SourceSummary => String.IsNullOrEmpty(SourceCpkPath) ? T("尚未载入 CPK") : F("{0} · {1} 张文本表", Path.GetFileName(SourceCpkPath), Tables.Count);
	public String ToolSummary => String.IsNullOrEmpty(ToolPath) ? T("尚未选择 cricodecs") : ToolPath;
	public String SearchText { get => mSearchText; set => SetField(ref mSearchText, value ?? String.Empty, nameof(SearchText)); }
	public TextModLanguage SelectedLanguage
	{
		get => mState.SelectedLanguage;
		set
		{
			if (value == null || mState.SelectedLanguage == value) return;
			mState.SelectedLanguage = value;
			OnPropertyChanged(nameof(SelectedLanguage));
			OnPropertyChanged(nameof(ExpectedCpkText));
		}
	}
	public String ExpectedCpkText => F("应选择 {0}", SelectedLanguage.CpkFileName);
	public ModTarget SelectedTarget
	{
		get => mState.SelectedTarget;
		set
		{
			ModTarget target = value ?? ModTarget.Asia;
			if (mState.SelectedTarget == target) return;
			mState.SelectedTarget = target;
			OnPropertyChanged(nameof(SelectedTarget));
		}
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
	public String ChangeSummary => F("共修改 {0} 项", ChangedCount);

	public TextEditorViewModel(Window owner, Action<String> setStatus, ModProjectState project)
	{
		mOwner = owner;
		mSetStatus = setStatus;
		mProject = project;
		mState = project.Text;
		ChooseToolCommand = new ActionCommand(ChooseTool);
		OpenCpkCommand = new ActionCommand(OpenCpk);
		SearchCommand = new ActionCommand(_ => RefreshResults());
		ExportCommand = new ActionCommand(Export);
		ToolPath = FindToolOnPath() ?? String.Empty;
	}

	public void RefreshLocale()
	{
		foreach (TextTable table in Tables) table.NotifyChanged();
		foreach (TextEntry entry in SearchResults) entry.RefreshLocale();
		OnPropertyChanged(nameof(Languages));
		OnPropertyChanged(nameof(SelectedLanguage));
		OnPropertyChanged(nameof(Targets));
		OnPropertyChanged(nameof(SelectedTarget));
		OnPropertyChanged(nameof(SourceSummary));
		OnPropertyChanged(nameof(ToolSummary));
		OnPropertyChanged(nameof(ExpectedCpkText));
		OnPropertyChanged(nameof(ChangeSummary));
		UpdateValidation();
	}

	private async void ChooseTool(object? parameter)
	{
		var files = await mOwner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = T("选择 cricodecs 可执行文件"),
			AllowMultiple = false,
		});
		String? path = files.FirstOrDefault()?.TryGetLocalPath();
		if (String.IsNullOrEmpty(path)) return;
		try
		{
			TextModPackageBuilder.RunTool(path, ["--version"]);
			ToolPath = path;
			OnPropertyChanged(nameof(ToolSummary));
			mSetStatus(T("CPK 工具校验通过。"));
		}
		catch (Exception exception)
		{
			mSetStatus(F("CPK 工具不可用：{0}", exception.Message));
		}
	}

	private async void OpenCpk(object? parameter)
	{
		if (String.IsNullOrEmpty(ToolPath))
		{
			mSetStatus(T("请先选择 cricodecs 可执行文件。"));
			return;
		}
		var files = await mOwner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = F("选择 {0} 的副本", SelectedLanguage.CpkFileName),
			AllowMultiple = false,
			FileTypeFilter = [new FilePickerFileType(T("CRI CPK 归档")) { Patterns = ["*.CPK", "*.cpk"] }],
		});
		String? path = files.FirstOrDefault()?.TryGetLocalPath();
		if (String.IsNullOrEmpty(path)) return;
		if (!String.Equals(Path.GetFileName(path), SelectedLanguage.CpkFileName, StringComparison.OrdinalIgnoreCase))
		{
			mSetStatus(F("文件名不匹配：当前语言需要 {0}。", SelectedLanguage.CpkFileName));
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
			mSetStatus(F("文本归档载入成功：共 {0:N0} 个索引。", Tables.Sum(table => table.Document.Count)));
		}
		catch (Exception exception)
		{
			mSetStatus(F("载入文本归档失败：{0}", exception.Message));
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
			mSetStatus(T("请先载入 CPK 并修改至少一个文本条目。"));
			return;
		}
		ModTarget target = SelectedTarget;
		var file = await mOwner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
		{
			Title = T("导出文本 MOD 包"),
			DefaultExtension = "zip",
			SuggestedFileName = $"UnicornOverlord-{target.Key}-{SelectedLanguage.CpkFileName[..^4]}-TextMod.zip",
			FileTypeChoices = [new FilePickerFileType(T("ZIP 压缩包")) { Patterns = ["*.zip"] }],
		});
		String? path = file?.TryGetLocalPath();
		if (String.IsNullOrEmpty(path)) return;
		try
		{
			TextModPackageBuilder.Create(path, ToolPath, SourceCpkPath, SelectedLanguage, target, Tables, mProject.ToTextJson(target));
			mSetStatus(F("文本 MOD 导出成功：{0} 项修改，目标 {1}。", ChangedCount, target.DisplayName));
		}
		catch (Exception exception)
		{
			mSetStatus(F("文本 MOD 导出失败：{0}", exception.Message));
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
		mSetStatus(SearchResults.Count == MaximumResults ? T("显示前 500 项，请缩小搜索范围。") : F("找到 {0} 项。", SearchResults.Count));
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
			ValidationMessage = T("请选择一个文本条目。");
			return;
		}
		String originalTokens = String.Join(' ', ExtractMarkupTokens(SelectedEntry.OriginalText));
		String editedTokens = String.Join(' ', ExtractMarkupTokens(SelectedEntry.Text));
		ValidationMessage = originalTokens == editedTokens
			? T("格式标记与运行时占位符保持一致。")
			: T("注意：格式标记或运行时占位符已变化，请确认这是有意修改。");
	}

	private static String T(String source) => LocaleManager.Instance.Translate(source);
	private static String F(String source, params object?[] args) => LocaleManager.Instance.Format(source, args);

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

}
