using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using System.ComponentModel;
using System.Text.Json;

namespace UnicornOverlord;

internal sealed record EditorLanguage(String Code, String DisplayName)
{
	public override String ToString() => DisplayName;
}

internal sealed class LocaleManager : INotifyPropertyChanged
{
	private readonly Dictionary<String, IReadOnlyDictionary<String, String>> mLocales = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<String, String> mKnownTranslations = new(StringComparer.Ordinal);
	private int mLanguageIndex;

	private LocaleManager()
	{
		Languages = [new("en-US", "English"), new("ja-JP", "日本語"), new("zh-CN", "简体中文")];
		foreach (EditorLanguage language in Languages)
		{
			String path = Path.Combine(AppContext.BaseDirectory, "locales", $"{language.Code}.json");
			Dictionary<String, String> locale = File.Exists(path)
				? JsonSerializer.Deserialize<Dictionary<String, String>>(File.ReadAllText(path)) ?? []
				: [];
			mLocales[language.Code] = locale;
			foreach ((String source, String translation) in locale)
				if (!String.IsNullOrWhiteSpace(translation)) mKnownTranslations.TryAdd(translation, source);
		}
		mLanguageIndex = Math.Clamp(ApplicationSettings.Language, 0, Languages.Count - 1);
	}

	public static LocaleManager Instance { get; } = new();
	public event PropertyChangedEventHandler? PropertyChanged;
	public event EventHandler? LanguageChanged;
	public IReadOnlyList<EditorLanguage> Languages { get; }
	public int LanguageIndex => mLanguageIndex;

	public void SetLanguage(int index)
	{
		int normalized = Math.Clamp(index, 0, Languages.Count - 1);
		ApplicationSettings.Language = normalized;
		if (mLanguageIndex == normalized) return;
		mLanguageIndex = normalized;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageIndex)));
		ModTarget.RefreshLocale();
		TextModLanguage.RefreshLocale();
		LanguageChanged?.Invoke(this, EventArgs.Empty);
	}

	public String Translate(String source)
	{
		if (String.IsNullOrEmpty(source)) return source;
		if (mKnownTranslations.TryGetValue(source, out String? original)) source = original;
		IReadOnlyDictionary<String, String> locale = mLocales[Languages[mLanguageIndex].Code];
		if (locale.TryGetValue(source, out String? translated) && !String.IsNullOrWhiteSpace(translated)) return translated;
		foreach (String template in mLocales.Values.SelectMany(values => values.Keys).Distinct().Where(value => value.Count(c => c == '{') == 1 && value.Contains("{0}", StringComparison.Ordinal)))
		{
			IEnumerable<String> candidates = [template, .. mLocales.Values.Select(values => values.GetValueOrDefault(template)).Where(value => !String.IsNullOrWhiteSpace(value))!];
			foreach (String candidate in candidates)
			{
				int marker = candidate.IndexOf("{0}", StringComparison.Ordinal);
				String prefix = candidate[..marker];
				String suffix = candidate[(marker + 3)..];
				if (!source.StartsWith(prefix, StringComparison.Ordinal) || !source.EndsWith(suffix, StringComparison.Ordinal) || source.Length < prefix.Length + suffix.Length) continue;
				String value = source[prefix.Length..(source.Length - suffix.Length)];
				String target = locale.GetValueOrDefault(template) ?? template;
				return String.Format(target, value);
			}
		}
		return source;
	}

	public String Format(String source, params object?[] args) => String.Format(Translate(source), args);
}

internal static class VisualLocalizer
{
	public static void Apply(Window window)
	{
		Translate(window, Window.TitleProperty);
		foreach (Visual visual in window.GetVisualDescendants())
		{
			if (visual is TextBlock text) Translate(text, TextBlock.TextProperty);
			if (visual is ContentControl content) TranslateObject(content, ContentControl.ContentProperty);
			if (visual is HeaderedContentControl header) TranslateObject(header, HeaderedContentControl.HeaderProperty);
			if (visual is TextBox box) Translate(box, TextBox.PlaceholderTextProperty);
			if (visual is ToggleSwitch toggle)
			{
				TranslateObject(toggle, ToggleSwitch.OnContentProperty);
				TranslateObject(toggle, ToggleSwitch.OffContentProperty);
			}
			if (visual is Control control && ToolTip.GetTip(control) is String tip)
				ToolTip.SetTip(control, LocaleManager.Instance.Translate(tip));
		}
	}

	private static void Translate(AvaloniaObject target, AvaloniaProperty<String?> property)
	{
		String? value = (String?)target.GetValue(property);
		if (value == null) return;
		String translated = LocaleManager.Instance.Translate(value);
		if (!String.Equals(value, translated, StringComparison.Ordinal)) target.SetCurrentValue(property, translated);
	}

	private static void TranslateObject(AvaloniaObject target, AvaloniaProperty<object?> property)
	{
		if (target.GetValue(property) is String value)
		{
			String translated = LocaleManager.Instance.Translate(value);
			if (!String.Equals(value, translated, StringComparison.Ordinal)) target.SetCurrentValue(property, translated);
		}
	}
}
