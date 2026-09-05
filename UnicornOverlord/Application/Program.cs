using Avalonia;

namespace UnicornOverlord;

internal static class Program
{
	[STAThread]
	public static void Main(string[] args)
	{
		if (args is ["--validate-mod-layout"])
		{
			BuildAvaloniaApp().SetupWithoutStarting();
			ModLayoutSmokeTest.Run();
			return;
		}
		if (args is ["--validate-mods", String outputPath])
		{
			ModSmokeTest.Run(outputPath);
			return;
		}
		if (args is ["--validate-fms", String inputPath, String fmsOutputPath])
		{
			FmsDocument.Load(inputPath).Write(fmsOutputPath);
			Console.WriteLine($"FMS 往返完成：{fmsOutputPath}");
			return;
		}
		if (args is ["--validate-text-mod", String toolPath, String sourceCpk, String sourceFms, String textModOutputPath])
		{
			FmsDocument document = FmsDocument.Load(sourceFms);
			document.SetText(95, document.GetText(95) + " ");
			TextModPackageBuilder.Create(textModOutputPath, toolPath, sourceCpk, TextModLanguage.All[0], ModTarget.Asia,
				[new TextTable("UcFactorList", "MsgSheet/UcFactorList.fms", 28, document)]);
			Console.WriteLine($"文本 MOD 自检包生成完成：{textModOutputPath}");
			return;
		}
		if (args is ["--inspect-fms", String inspectPath, String indexText] && Int32.TryParse(indexText, out int index))
		{
			FmsDocument document = FmsDocument.Load(inspectPath);
			Console.WriteLine($"索引 {index}：{document.GetText(index).Replace(" ", "[空格]")}");
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
