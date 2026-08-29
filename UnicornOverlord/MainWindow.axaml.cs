using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace UnicornOverlord;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		DataContext = new ViewModel(this);
		Opened += (_, _) => VisualLocalizer.Apply(this);
		LocaleManager.Instance.LanguageChanged += LocaleManager_LanguageChanged;
		Closed += (_, _) => LocaleManager.Instance.LanguageChanged -= LocaleManager_LanguageChanged;
	}

	private void LocaleManager_LanguageChanged(object? sender, EventArgs e) => VisualLocalizer.Apply(this);

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

}
