using Avalonia;

namespace UnicornOverlord;

internal static class Program
{
	[STAThread]
	public static void Main(string[] args)
	{
		if (args is ["--validate-mods", String outputPath])
		{
			ModSmokeTest.Run(outputPath);
			return;
		}
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.WithInterFont()
			.LogToTrace();
	}
}
