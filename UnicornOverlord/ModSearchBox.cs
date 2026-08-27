using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;

namespace UnicornOverlord;

public sealed class ModSearchBox : ComboBox
{
	private bool mUpdatingText;

	public ModSearchBox()
	{
		IsEditable = true;
		IsTextSearchEnabled = false;
		PlaceholderText = "输入名称或 ID";
		MinHeight = 32;
		SelectionChanged += (_, _) => SyncSelectedText();
		Loaded += (_, _) => SyncSelectedText();
		LostFocus += (_, _) => CommitSearchText();
		KeyDown += OnSearchKeyDown;
		PropertyChanged += (_, args) =>
		{
			if (args.Property == TextProperty && !mUpdatingText) SelectUniqueMatch();
		};
	}

	private void OnSearchKeyDown(object? sender, KeyEventArgs args)
	{
		if (args.Key != Key.Enter) return;
		CommitSearchText();
		args.Handled = true;
	}

	private void SelectUniqueMatch()
	{
		if (String.IsNullOrWhiteSpace(Text)) return;
		object[] matches = GetItems().Where(item => Matches(Text, item)).Take(2).ToArray();
		if (matches.Length == 1) SelectedItem = matches[0];
	}

	private void CommitSearchText()
	{
		String query = Text?.Trim() ?? String.Empty;
		if (query.Length == 0)
		{
			SyncSelectedText();
			return;
		}

		object? match = GetItems().FirstOrDefault(item => GetSearchTerms(item).Any(term => String.Equals(term, query, StringComparison.CurrentCultureIgnoreCase)))
			?? GetItems().FirstOrDefault(item => Matches(query, item));
		if (match != null) SelectedItem = match;
		SyncSelectedText();
	}

	private IEnumerable<object> GetItems() => ItemsSource?.Cast<object>() ?? [];

	private void SyncSelectedText()
	{
		if (SelectedItem == null) return;
		mUpdatingText = true;
		Text = GetDisplayText(SelectedItem);
		mUpdatingText = false;
	}

	private static String GetDisplayText(object? value) => value switch
	{
		ModChoice choice => choice.DisplayName,
		ModLocationChoice location => location.DisplayName,
		ModRecordChoice record => record.DetailDisplayName,
		double number => number.ToString(CultureInfo.CurrentCulture),
		_ => value?.ToString() ?? String.Empty,
	};

	internal static bool Matches(String? search, object? item)
	{
		if (item == null) return false;
		if (String.IsNullOrWhiteSpace(search)) return true;
		String query = search.Trim();
		return GetSearchTerms(item).Any(term => term.Contains(query, StringComparison.CurrentCultureIgnoreCase));
	}

	private static IEnumerable<String> GetSearchTerms(object item) => item switch
	{
		ModChoice choice => [choice.Value.ToString(CultureInfo.InvariantCulture), choice.Name, choice.DisplayName, choice.EnglishName, choice.ChineseName],
		ModLocationChoice location => [location.DisplayName, location.EnglishName, location.JapaneseName, location.ChineseName],
		ModRecordChoice record =>
		[
			record.Value.ToString(CultureInfo.InvariantCulture),
			record.Detail.Value.ToString(CultureInfo.InvariantCulture),
			record.DetailDisplayName,
			record.DisplayName,
			record.Detail.Name,
		],
		double number => [number.ToString(CultureInfo.CurrentCulture), number.ToString(CultureInfo.InvariantCulture)],
		_ => [item.ToString() ?? String.Empty],
	};
}
