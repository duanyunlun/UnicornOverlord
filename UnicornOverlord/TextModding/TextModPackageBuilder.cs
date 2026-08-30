using System.Diagnostics;
using System.IO.Compression;

namespace UnicornOverlord;

internal static class TextModPackageBuilder
{
	public static void Create(String filename, String toolPath, String sourceCpk, TextModLanguage language,
		ModTarget target, IReadOnlyCollection<TextTable> tables, String? projectJson = null)
	{
		TextTable[] changedTables = tables.Where(table => table.Document.ChangedCount > 0).ToArray();
		if (changedTables.Length == 0) throw new InvalidOperationException("请先修改至少一个文本条目。");
		if (!String.Equals(Path.GetFileName(sourceCpk), language.CpkFileName, StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException($"当前语言应使用 {language.CpkFileName}，所选文件名不匹配。");

		String outputDirectory = Path.GetDirectoryName(Path.GetFullPath(filename)) ?? Environment.CurrentDirectory;
		String workDirectory = Path.Combine(outputDirectory, $".uo-text-{Guid.NewGuid():N}");
		Directory.CreateDirectory(workDirectory);
		try
		{
			var arguments = new List<String> { sourceCpk };
			foreach (TextTable table in changedTables)
			{
				String tablePath = Path.Combine(workDirectory, Path.GetFileName(table.ArchivePath));
				table.Document.Write(tablePath);
				arguments.Add("--replace");
				arguments.Add($"{table.ArchiveIndex}={tablePath}");
			}

			String rebuiltCpk = Path.Combine(workDirectory, language.CpkFileName);
			arguments.Add("-o");
			arguments.Add(rebuiltCpk);
			RunTool(toolPath, arguments);

			using FileStream zipStream = File.Create(filename);
			using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);
			String cpkPath = $"emulator/contents/{target.TitleId}/text_editor/romfs/{language.CpkFileName}";
			archive.CreateEntryFromFile(rebuiltCpk, cpkPath, CompressionLevel.NoCompression);
				archive.WriteUtf8Text("README_CN.txt", $"""
				《独角兽之王》文本 MOD

				目标版本：{target.DisplayName}
				目标语言：{language.Name}
				替换文件：{language.CpkFileName}
				修改条目：{changedTables.Sum(table => table.Document.ChangedCount)} 项

				压缩包内 emulator 目录可复制到模拟器配置目录。启用前请确认 Title ID 与游戏版本一致。
				本工具只重建所选 CPK 的副本，不修改源 CPK、游戏文件或存档。
				""" + "\n");
				if (!String.IsNullOrEmpty(projectJson)) archive.WriteUtf8Text("mod-project.json", projectJson + "\n");
		}
		finally
		{
			Directory.Delete(workDirectory, true);
		}
	}

	internal static String RunTool(String toolPath, IReadOnlyCollection<String> arguments)
	{
		var startInfo = new ProcessStartInfo(toolPath)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};
		foreach (String argument in arguments) startInfo.ArgumentList.Add(argument);

		using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 CPK 工具。");
		Task<String> outputTask = process.StandardOutput.ReadToEndAsync();
		Task<String> errorTask = process.StandardError.ReadToEndAsync();
		process.WaitForExit();
		String output = outputTask.GetAwaiter().GetResult();
		String error = errorTask.GetAwaiter().GetResult();
		if (process.ExitCode != 0)
			throw new InvalidOperationException($"CPK 工具执行失败：{(String.IsNullOrWhiteSpace(error) ? output : error).Trim()}");
		return output;
	}
}
