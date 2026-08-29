using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace UnicornOverlord;

public partial class ModSearchPicker : UserControl
{
	public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
		AvaloniaProperty.Register<ModSearchPicker, IEnumerable?>(nameof(ItemsSource));
	public static readonly StyledProperty<double> ControlHeightProperty =
		AvaloniaProperty.Register<ModSearchPicker, double>(nameof(ControlHeight), 32);
	public static readonly StyledProperty<double> ChoiceWidthProperty =
		AvaloniaProperty.Register<ModSearchPicker, double>(nameof(ChoiceWidth), 120);
	public static readonly DirectProperty<ModSearchPicker, object?> SelectedItemProperty =
		AvaloniaProperty.RegisterDirect<ModSearchPicker, object?>(nameof(SelectedItem), picker => picker.SelectedItem,
			(picker, value) => picker.SelectedItem = value, defaultBindingMode: BindingMode.TwoWay);

	private ComboBox mChoices = null!;
	private TextBox mSearchBox = null!;
	private object[] mItems = [];
	private object? mSelectedItem;
	private bool mUpdating;

	public ModSearchPicker()
	{
		AvaloniaXamlLoader.Load(this);
		mChoices = this.FindControl<ComboBox>("Choices")!;
		mSearchBox = this.FindControl<TextBox>("SearchBox")!;
		mChoices.ItemTemplate = new FuncDataTemplate(typeof(object), (item, _) => new TextBlock { Text = GetDisplayText(item) });
		mChoices.SelectionChanged += (_, _) =>
		{
			if (mUpdating) return;
			mUpdating = true;
			SelectedItem = mChoices.SelectedItem;
			mUpdating = false;
		};
	}

	public IEnumerable? ItemsSource
	{
		get => GetValue(ItemsSourceProperty);
		set => SetValue(ItemsSourceProperty, value);
	}

	public double ControlHeight
	{
		get => GetValue(ControlHeightProperty);
		set => SetValue(ControlHeightProperty, value);
	}

	public double ChoiceWidth
	{
		get => GetValue(ChoiceWidthProperty);
		set => SetValue(ChoiceWidthProperty, value);
	}

	public object? SelectedItem
	{
		get => mSelectedItem;
		set
		{
			if (!SetAndRaise(SelectedItemProperty, ref mSelectedItem, value) || mUpdating || mChoices == null) return;
			ApplyFilter();
		}
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == ItemsSourceProperty)
		{
			mItems = ItemsSource?.Cast<object>().ToArray() ?? [];
			if (mSearchBox != null) mSearchBox.Text = String.Empty;
			ApplyFilter();
		}
	}

	private void OnSearchTextChanged(object? sender, TextChangedEventArgs args) => ApplyFilter();

	private void ApplyFilter()
	{
		if (mChoices == null || mSearchBox == null) return;
		String query = mSearchBox.Text?.Trim() ?? String.Empty;
		object[] visibleItems = query.Length == 0 ? mItems : mItems.Where(item => Matches(query, item)).ToArray();
		mUpdating = true;
		mChoices.ItemsSource = visibleItems;
		mChoices.SelectedItem = visibleItems.Contains(SelectedItem) ? SelectedItem : null;
		mUpdating = false;
	}

	private static String GetDisplayText(object? value) => value switch
	{
		ModChoice choice => choice.DisplayName,
		ModLocationChoice location => location.DisplayName,
		FortLocationState location => location.DisplayName,
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
		ModChoice choice => [choice.Name, choice.EnglishName, choice.JapaneseName, choice.ChineseName],
		ModLocationChoice location => [location.DisplayName, location.EnglishName, location.JapaneseName, location.ChineseName],
		FortLocationState location => [location.DisplayName, location.Choice.EnglishName, location.Choice.JapaneseName, location.Choice.ChineseName],
		ModRecordChoice record =>
		[
			record.DetailDisplayName,
			record.DisplayName,
			record.Detail.Name,
		],
		double number => [number.ToString(CultureInfo.CurrentCulture), number.ToString(CultureInfo.InvariantCulture)],
		_ => [item.ToString() ?? String.Empty],
	};
}
