using Avalonia.Controls;
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
}
