using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace UnicornOverlord;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		DataContext = new ViewModel(this);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private void MineLocation_SelectionChanged(object? sender, SelectionChangedEventArgs args)
	{
		if (sender is not ComboBox choices || choices.DataContext is not ModModule module || choices.SelectedIndex < 0) return;
		int selectedIndex = choices.SelectedIndex;
		Dispatcher.UIThread.Post(() =>
		{
			if (ReferenceEquals(choices.DataContext, module) && choices.SelectedIndex == selectedIndex)
				module.SelectedMineLocationIndex = selectedIndex;
		}, DispatcherPriority.Background);
	}
}
