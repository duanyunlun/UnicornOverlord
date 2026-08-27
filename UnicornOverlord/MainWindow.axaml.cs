using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

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

	private void OnModSearchLoaded(object? sender, RoutedEventArgs args)
	{
		if (sender is ComboBox comboBox) ModComboSearch.Attach(comboBox);
	}
}
