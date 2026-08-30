using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace UnicornOverlord;

public partial class MainWindow : Window
{
	private bool mLocalizationQueued;

	public MainWindow()
	{
		InitializeComponent();
		DataContext = new ViewModel(this);
		Opened += (_, _) => QueueLocalization(true);
		LayoutUpdated += (_, _) => QueueLocalization(false);
		LocaleManager.Instance.LanguageChanged += LocaleManager_LanguageChanged;
		Closed += (_, _) => LocaleManager.Instance.LanguageChanged -= LocaleManager_LanguageChanged;
	}

	private void LocaleManager_LanguageChanged(object? sender, EventArgs e) => QueueLocalization(true);

	private void QueueLocalization(bool applyNow)
	{
		if (applyNow) VisualLocalizer.Apply(this);
		if (mLocalizationQueued) return;
		mLocalizationQueued = true;
		Dispatcher.UIThread.Post(() =>
		{
			mLocalizationQueued = false;
			VisualLocalizer.Apply(this);
		}, DispatcherPriority.Background);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

}
