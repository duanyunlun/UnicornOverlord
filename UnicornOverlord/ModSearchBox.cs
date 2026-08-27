using System.Globalization;
using Avalonia.Controls;

namespace UnicornOverlord;

public sealed class ModSearchBox : AutoCompleteBox
{
	public ModSearchBox()
	{
		MinimumPrefixLength = 0;
		MinimumPopulateDelay = TimeSpan.Zero;
		IsTextCompletionEnabled = false;
		ClearSelectionOnLostFocus = false;
		ItemFilter = Matches;
		MinHeight = 32;
	}

	protected override String FormatValue(object? value) => value switch
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
