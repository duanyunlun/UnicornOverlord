using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UnicornOverlord;

public partial class MissionEditorView : UserControl
{
	private const int ImportByteLimit = 16 * 1024 * 1024;
	public MissionEditorView() => AvaloniaXamlLoader.Load(this);

	private async void ImportEdits(object? sender, RoutedEventArgs args)
	{
		if (DataContext is not MissionEditorState state || TopLevel.GetTopLevel(this) is not { } top) return;
		try
		{
			var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = "导入任务编辑 JSON / 上游 ZIP", AllowMultiple = false,
				FileTypeFilter = [new FilePickerFileType("任务编辑 JSON / ZIP") { Patterns = ["*.json", "*.zip"] }],
			});
			if (files.Count == 0) return;
			using var stream = await files[0].OpenReadAsync();
			var document = await ReadEditsAsync(stream, files[0].Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
			if (document["target"] != null && document["target"] is not JsonObject) throw new InvalidDataException("target 必须为对象。");
			if (document["edits"] != null && document["edits"] is not JsonObject) throw new InvalidDataException("edits 必须为对象。");
			if (document["target"] is JsonObject target)
			{
				var current = (top.DataContext as ViewModel)?.SelectedModTarget ?? throw new InvalidDataException("无法确认当前导出目标，已取消导入。");
				String key = MissionModCatalog.Text(target, "Key", MissionModCatalog.Text(target, "key"));
				String title = MissionModCatalog.Text(target, "TitleId", MissionModCatalog.Text(target, "titleId"));
				String build = MissionModCatalog.Text(target, "BuildId", MissionModCatalog.Text(target, "buildId"));
				if (key != current.Key || !String.Equals(title, current.TitleId, StringComparison.OrdinalIgnoreCase) || !String.Equals(build, current.BuildId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("JSON 目标 / TitleID / BuildID 与当前目标不匹配，请先切换正确的导出目标。");
			}
			if (document["schemaVersion"] != null && MissionModCatalog.Number(document, "schemaVersion", -1) != 1) throw new InvalidDataException("不支持的任务编辑文件版本。");
			state.Import(document);
			state.Status = "已通过补丁验证并导入。职业技能与默认 IF 已交由职业模块统一管理。";
		}
		catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
		{
			state.Status = $"导入失败：{error.Message}";
		}
	}

	private async void SaveEdits(object? sender, RoutedEventArgs args)
	{
		if (DataContext is not MissionEditorState state || TopLevel.GetTopLevel(this) is not { DataContext: ViewModel model } top) return;
		try
		{
			var target = model.SelectedModTarget;
			var document = new JsonObject
			{
				["schemaVersion"] = 1,
				["target"] = new JsonObject { ["Key"] = target.Key, ["TitleId"] = target.TitleId, ["BuildId"] = target.BuildId },
				["edits"] = state.ExportEdits(),
			};
			byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, new JsonSerializerOptions { WriteIndented = true });
			if (bytes.Length >= ImportByteLimit) throw new InvalidDataException("编辑 JSON 必须小于 16 MiB。");
			var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = "保存完整任务 / 职业编辑 JSON", SuggestedFileName = "mission_editor_edits.json", DefaultExtension = "json",
				FileTypeChoices = [new FilePickerFileType("任务编辑 JSON") { Patterns = ["*.json"] }],
			});
			if (file == null) return;
			await using var output = await file.OpenWriteAsync();
			await output.WriteAsync(bytes);
			if (output.CanSeek) output.SetLength(output.Position);
			await output.FlushAsync();
			state.Status = "已保存完整编辑 JSON（含职业技能 / 默认 IF / 默认装备）。保存不代表共享覆盖已获授权，补丁导出仍会校验。";
		}
		catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
		{
			state.Status = $"保存失败：{error.Message}";
		}
	}

	internal static async Task<JsonObject> ReadEditsAsync(Stream stream, bool isArchive)
	{
		byte[] bytes = await ReadLimitedAsync(stream);
		if (isArchive)
		{
			using var buffer = new MemoryStream(bytes, false);
			using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
			if (archive.Entries.Count > 2048 || archive.Entries.Any(entry => entry.Length >= ImportByteLimit)) throw new InvalidDataException("ZIP 最多 2048 个条目，每个条目须小于 16 MiB。");
			var matches = archive.Entries.Where(entry => String.Equals(entry.FullName.Replace('\\', '/').Split('/').Last(), "mission_editor_edits.json", StringComparison.OrdinalIgnoreCase)).ToArray();
			if (matches.Length != 1) throw new InvalidDataException("ZIP 必须且只能包含一个 mission_editor_edits.json；不会解压或执行其他条目。");
			using var input = matches[0].Open();
			bytes = await ReadLimitedAsync(input);
		}
		return JsonNode.Parse(bytes) as JsonObject ?? throw new InvalidDataException("导入内容必须为 JSON 对象。");
	}

	private static async Task<byte[]> ReadLimitedAsync(Stream stream)
	{
		using var output = new MemoryStream();
		var buffer = new byte[81920];
		int count;
		while ((count = await stream.ReadAsync(buffer)) != 0)
		{
			if (output.Length + count >= ImportByteLimit) throw new InvalidDataException("ZIP 文件 / JSON 内容必须小于 16 MiB。");
			output.Write(buffer, 0, count);
		}
		return output.ToArray();
	}

	internal static async Task ValidateFileImport()
	{
		using var buffer = new MemoryStream();
		using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
		{
			using var writer = new StreamWriter(archive.CreateEntry("nested/mission_editor_edits.json").Open());
			writer.Write("{\"edits\":{\"unitsets\":[]}}");
		}
		buffer.Position = 0;
		if ((await ReadEditsAsync(buffer, true))["edits"]?["unitsets"] is not JsonArray) throw new InvalidDataException("ZIP 内存导入失败。");
		using (var archive = new ZipArchive(buffer, ZipArchiveMode.Update, true)) archive.CreateEntry("mission_editor_edits.json");
		buffer.Position = 0;
		try { await ReadEditsAsync(buffer, true); throw new InvalidOperationException("重复 JSON 未被拒绝。"); } catch (InvalidDataException) { }
		using var oversized = new MemoryStream(new byte[ImportByteLimit], false);
		try { await ReadEditsAsync(oversized, false); throw new InvalidOperationException("超限 JSON 未被拒绝。"); } catch (InvalidDataException) { }
		using var bomb = new MemoryStream();
		using (var archive = new ZipArchive(bomb, ZipArchiveMode.Create, true))
		{
			using var entry = archive.CreateEntry("mission_editor_edits.json", CompressionLevel.SmallestSize).Open();
			oversized.Position = 0;
			await oversized.CopyToAsync(entry);
		}
		bomb.Position = 0;
		try { await ReadEditsAsync(bomb, true); throw new InvalidOperationException("超限 ZIP 条目未被拒绝。"); } catch (InvalidDataException) { }
	}
}
