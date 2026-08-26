using System.IO.Compression;

namespace UnicornOverlord;

internal static class ModSmokeTest
{
	public static void Run(String outputPath)
	{
		String? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
		if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
		ModModule[] modules = [.. ViewModel.CreateModModules()];
		foreach (ModTarget target in ModTarget.All)
		{
			String targetPath = target == ModTarget.Asia ? outputPath : AddSuffix(outputPath, target.Key);
			ValidateTarget(targetPath, modules, target);
		}
	}

	private static void ValidateTarget(String outputPath, ModModule[] modules, ModTarget target)
	{
		ModPackageBuilder.Create(outputPath, modules, target);
		using ZipArchive archive = ZipFile.OpenRead(outputPath);
		ZipArchiveEntry[] patches = archive.Entries.Where(entry => entry.FullName.EndsWith("main.pchtxt", StringComparison.Ordinal)).ToArray();
		if (patches.Length != modules.Length) throw new InvalidDataException($"应生成 {modules.Length} 个补丁，实际为 {patches.Length} 个。");
		foreach (ZipArchiveEntry patch in patches)
		{
			using var reader = new StreamReader(patch.Open());
			String content = reader.ReadToEnd();
			if (!content.Contains($"@nsobid-{target.BuildId}", StringComparison.Ordinal))
				throw new InvalidDataException($"{patch.FullName} 的 Build ID 不正确。");
			if (content.Contains("{{", StringComparison.Ordinal))
				throw new InvalidDataException($"{patch.FullName} 仍包含未替换占位符。");
		}
		if (patches.Any(entry => !entry.FullName.Contains($"/contents/{target.TitleId}/", StringComparison.Ordinal)))
			throw new InvalidDataException($"{target.DisplayName} 的 ZIP 路径包含错误 Title ID。");
		Console.WriteLine($"MOD 自检通过：{target.DisplayName}，{patches.Length} 个模块，输出 {outputPath}");
	}

	private static String AddSuffix(String path, String suffix)
	{
		String extension = Path.GetExtension(path);
		return Path.Combine(Path.GetDirectoryName(path) ?? String.Empty, Path.GetFileNameWithoutExtension(path) + $"-{suffix}" + extension);
	}
}
