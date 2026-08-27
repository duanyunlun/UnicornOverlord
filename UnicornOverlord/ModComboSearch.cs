using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace UnicornOverlord;

internal static class ModComboSearch
{
	private sealed class SearchState
	{
		public bool UpdatingText { get; set; }
	}

	private static readonly ConditionalWeakTable<ComboBox, SearchState> States = new();

	public static void Attach(ComboBox comboBox)
	{
		if (States.TryGetValue(comboBox, out _)) return;
		var state = new SearchState();
		States.Add(comboBox, state);
		comboBox.IsEditable = true;
		comboBox.IsTextSearchEnabled = false;
		comboBox.PlaceholderText = "输入名称或 ID";
		comboBox.MinHeight = 32;
		comboBox.SelectionChanged += (_, _) => SyncSelectedText(comboBox, state);
		comboBox.LostFocus += (_, _) => CommitSearchText(comboBox, state);
		comboBox.PropertyChanged += (_, args) =>
		{
			if (args.Property == ComboBox.TextProperty && !state.UpdatingText) SelectUniqueMatch(comboBox);
		};
		SyncSelectedText(comboBox, state);
	}

	private static void SelectUniqueMatch(ComboBox comboBox)
	{
		if (String.IsNullOrWhiteSpace(comboBox.Text)) return;
		object[] matches = GetItems(comboBox).Where(item => Matches(comboBox.Text, item)).Take(2).ToArray();
		if (matches.Length == 1) comboBox.SelectedItem = matches[0];
	}

	private static void CommitSearchText(ComboBox comboBox, SearchState state)
	{
		String query = comboBox.Text?.Trim() ?? String.Empty;
		if (query.Length == 0)
		{
			SyncSelectedText(comboBox, state);
			return;
		}

		object? match = GetItems(comboBox).FirstOrDefault(item => GetSearchTerms(item).Any(term => String.Equals(term, query, StringComparison.CurrentCultureIgnoreCase)))
			?? GetItems(comboBox).FirstOrDefault(item => Matches(query, item));
		if (match != null) comboBox.SelectedItem = match;
		SyncSelectedText(comboBox, state);
	}

	private static IEnumerable<object> GetItems(ComboBox comboBox) => comboBox.ItemsSource?.Cast<object>() ?? [];

	private static void SyncSelectedText(ComboBox comboBox, SearchState state)
	{
		if (comboBox.SelectedItem == null) return;
		state.UpdatingText = true;
		comboBox.Text = GetDisplayText(comboBox.SelectedItem);
		state.UpdatingText = false;
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
