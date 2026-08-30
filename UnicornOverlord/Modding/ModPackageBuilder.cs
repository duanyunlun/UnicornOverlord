using System.IO.Compression;

namespace UnicornOverlord;

internal static class ModPackageBuilder
{
	public static void Create(String filename, IReadOnlyCollection<ModModule> modules, ModTarget target)
	{
		if (modules.Count == 0) throw new InvalidOperationException("请至少选择一个已接入的 MOD。");
		ModProjectState project = modules.First().Project;
		if (modules.Any(module => !ReferenceEquals(module.Project, project)))
			throw new InvalidOperationException("所选 MOD 模块不属于同一个项目状态。");
		var patches = modules.Select(module =>
		{
			if (!module.IsAvailable)
			{
				throw new InvalidOperationException($"MOD 模块尚未接入：{module.Name}");
			}
			return (Module: module, Content: ModPatchGenerator.Generate(module, target));
		}).ToArray();
		ValidateConflicts(patches);

		using FileStream stream = File.Create(filename);
		using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
		foreach (var patch in patches)
		{
			String entryName = $"emulator/contents/{target.TitleId}/{patch.Module.Key}/exefs/main.pchtxt";
			archive.WriteUtf8Text(entryName, patch.Content);
		}

		archive.WriteUtf8Text("README_CN.txt", CreateReadme(modules, target) + "\n");
		archive.WriteUtf8Text("manifest.txt", CreateManifest(modules, target));
		archive.WriteUtf8Text("mod-project.json", project.ToJson(modules, target) + "\n");
	}

	private static void ValidateConflicts(IEnumerable<(ModModule Module, String Content)> patches)
	{
		var ranges = new List<(uint Start, uint End, String Module)>();
		foreach (var patch in patches)
		{
			foreach (String line in patch.Content.Replace("\r\n", "\n").Split('\n'))
			{
				if (line.Length < 10 || !uint.TryParse(line.AsSpan(0, 8), System.Globalization.NumberStyles.HexNumber,
					System.Globalization.CultureInfo.InvariantCulture, out uint address)) continue;
				String hex = line[9..].Trim();
				if (hex.Length == 0 || hex.Length % 2 != 0 || !hex.All(Uri.IsHexDigit))
					throw new InvalidDataException($"{patch.Module.Name} 包含无效补丁行：{line}");
				uint end = checked(address + (uint)(hex.Length / 2));
				var conflict = ranges.FirstOrDefault(range => range.Module != patch.Module.Name && address < range.End && end > range.Start);
				if (conflict != default)
					throw new InvalidOperationException($"{patch.Module.Name} 与 {conflict.Module} 在地址 {Math.Max(address, conflict.Start):X8} 冲突。");
				ranges.Add((address, end, patch.Module.Name));
			}
		}
	}

	private static String CreateReadme(IEnumerable<ModModule> modules, ModTarget target)
	{
		String moduleNames = String.Join("、", modules.Select(module => module.Name));
		return $"""
		《独角兽之王》MOD 包

		目标版本：{target.Name} {target.GameVersion}
		Title ID：{target.TitleId}
		Build ID：{target.BuildId}
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

	private static String CreateManifest(IEnumerable<ModModule> modules, ModTarget target)
	{
		var lines = new List<String>
		{
			$"game_version={target.Name} {target.GameVersion}",
			$"title_id={target.TitleId}",
			$"build_id={target.BuildId}",
		};
		lines.AddRange(modules.Select(module => $"module={module.Key}|{module.Name}"));
		return String.Join('\n', lines) + "\n";
	}
}
