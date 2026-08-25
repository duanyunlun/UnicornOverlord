using System.IO.Compression;
using System.Text;

namespace UnicornOverlord;

internal static class ModPackageBuilder
{
	public const String TitleId = "010054b01ad92000";
	public const String BuildId = "9C3116F0333EA157526612D17354B3755737C4F2";
	public const String GameVersion = "亚洲中文版 v1.0.5";

	public static void Create(String filename, IReadOnlyCollection<ModModule> modules)
	{
		if (modules.Count == 0) throw new InvalidOperationException("请至少选择一个已接入的 MOD。");
		var templates = modules.Select(module =>
		{
			if (!module.IsAvailable || String.IsNullOrEmpty(module.TemplateFile))
			{
				throw new InvalidOperationException($"MOD 模块尚未接入：{module.Name}");
			}
			String source = Path.Combine(AppContext.BaseDirectory, "mods", module.TemplateFile);
			if (!File.Exists(source)) throw new FileNotFoundException($"缺少 MOD 模板：{module.TemplateFile}", source);
			return (Module: module, Source: source);
		}).ToArray();

		using FileStream stream = File.Create(filename);
		using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
		foreach (var template in templates)
		{
			String entryName = $"emulator/contents/{TitleId}/{template.Module.Key}/exefs/main.pchtxt";
			archive.CreateEntryFromFile(template.Source, entryName, CompressionLevel.Optimal);
		}

		WriteText(archive, "README_CN.txt", CreateReadme(modules) + "\n");
		WriteText(archive, "manifest.txt", CreateManifest(modules));
	}

	private static String CreateReadme(IEnumerable<ModModule> modules)
	{
		String moduleNames = String.Join("、", modules.Select(module => module.Name));
		return $"""
		《独角兽之王》MOD 包

		目标版本：{GameVersion}
		Title ID：{TitleId}
		Build ID：{BuildId}
		包含模块：{moduleNames}

		Astris / 模拟器安装：
		1. 完全退出游戏。
		2. 解压本文件。
		3. 将 emulator/contents/ 合并到模拟器的 mods/contents/ 目录。
		4. 在模拟器的模组管理中启用对应模块，再启动游戏。

		卸载时删除对应模块目录即可。本包只包含模拟器使用的 pchtxt，不能直接用于 Atmosphere 实机。
		请保留存档备份；补丁版本或 Build ID 不一致时不要启用。
		""";
	}

	private static String CreateManifest(IEnumerable<ModModule> modules)
	{
		var lines = new List<String>
		{
			$"game_version={GameVersion}",
			$"title_id={TitleId}",
			$"build_id={BuildId}",
		};
		lines.AddRange(modules.Select(module => $"module={module.Key}|{module.Name}"));
		return String.Join('\n', lines) + "\n";
	}

	private static void WriteText(ZipArchive archive, String path, String content)
	{
		ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
		using Stream stream = entry.Open();
		using var writer = new StreamWriter(stream, new UTF8Encoding(false));
		writer.Write(content);
	}
}
