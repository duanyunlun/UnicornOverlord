using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace UnicornOverlord;

public partial class ChoiceWindow : Window
{
	public enum eType
	{
		eItem,
		eEquipment,
		eClass,
	}

	public uint ID { get; set; }
	public eType Type { get; set; } = eType.eItem;
	public bool AllowMultiple { get; set; }
	public IReadOnlyList<uint> SelectedIDs { get; private set; } = [];

	public ChoiceWindow()
	{
		InitializeComponent();
	}

	private ListBox ItemList => this.FindControl<ListBox>("ListBoxItem")!;
	private TextBox FilterBox => this.FindControl<TextBox>("TextBoxFilter")!;
	private Button DecisionButton => this.FindControl<Button>("ButtonDecision")!;

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private void Window_Loaded(object? sender, RoutedEventArgs e)
	{
		ItemList.SelectionMode = AllowMultiple ? SelectionMode.Multiple : SelectionMode.Single;
		if (AllowMultiple)
		{
			Title = "选择一个或多个条目";
			DecisionButton.Content = "添加选中项";
		}
		CreateItemList(String.Empty);
		foreach (var item in ItemList.Items)
		{
			if (item is not NameValueInfo info || info.Value != ID) continue;
			ItemList.SelectedItem = item;
			ItemList.ScrollIntoView(item);
			break;
		}
		FilterBox.Focus();
	}

	private void TextBoxFilter_TextChanged(object? sender, TextChangedEventArgs e)
	{
		CreateItemList(FilterBox.Text ?? String.Empty);
	}

	private void ListBoxItem_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		DecisionButton.IsEnabled = ItemList.SelectedItems?.Count > 0;
	}

	private void ButtonDecision_Click(object? sender, RoutedEventArgs e)
	{
		SelectedIDs = ItemList.SelectedItems?.OfType<NameValueInfo>().Select(info => info.Value).ToArray() ?? [];
		if (SelectedIDs.Count == 0) return;
		ID = SelectedIDs[0];
		Close(true);
	}

	private void ButtonCancel_Click(object? sender, RoutedEventArgs e)
	{
		Close(false);
	}

	private void CreateItemList(String filter)
	{
		List<NameValueInfo> items = Type == eType.eClass ? Info.Instance().Class : Info.Instance().Item;
		IEnumerable<NameValueInfo> filtered = items.Where(item => MatchesType(item) && MatchesFilter(item, filter));
		ItemList.ItemsSource = filtered.ToList();
	}

	private bool MatchesType(NameValueInfo item)
	{
		if (Type == eType.eClass) return true;
		bool isEquipment = Info.Instance().Search(Info.Instance().Kind, item.Value) != null;
		return Type == eType.eEquipment ? isEquipment : !isEquipment;
	}

	private static bool MatchesFilter(NameValueInfo item, String filter)
	{
		if (String.IsNullOrWhiteSpace(filter)) return true;
		return item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
			item.Value.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase);
	}
}
