using System.IO.Compression;

namespace UnicornOverlord;

internal static class ModSmokeTest
{
	public static void Run(String outputPath)
	{
		String? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
		if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
		ModModule[] modules = [.. ViewModel.CreateModModules()];
		ValidateEditorSemantics(modules);
		foreach (ModTarget target in ModTarget.All)
		{
			String targetPath = target == ModTarget.Asia ? outputPath : AddSuffix(outputPath, target.Key);
			ValidateTarget(targetPath, modules, target);
		}
	}

	private static void ValidateEditorSemantics(ModModule[] modules)
	{
		int originalLanguage = ApplicationSettings.Language;
		ApplicationSettings.Language = 0;
		ModCatalog.RefreshLocalizedNames();
		Require(ModCatalog.FindSkill(372)?.Name == "Abyssal Miasma", "技能名称没有跟随编辑器语言切换为英文。");
		Require(ModCatalog.FindClass(1)?.Name == "Lord", "职业名称没有跟随编辑器语言切换为英文。");
		ApplicationSettings.Language = originalLanguage;
		ModCatalog.RefreshLocalizedNames();

		ModModule ability = modules.Single(module => module.Key == "ability_editor");
		String abilityPatch = ModPatchGenerator.Generate(ability, ModTarget.Asia);
		Require(ability.AbilityTypeText == "被动技能（PP）", "技能 372 应从游戏数据识别为被动技能。");
		Require(abilityPatch.Contains("027A38F4", StringComparison.Ordinal), "被动技能消耗必须写入 PP 字段。");

		ModModule classEditor = modules.Single(module => module.Key == "class_editor");
		String classPatch = ModPatchGenerator.Generate(classEditor, ModTarget.Asia);
		Require(classPatch.Contains("00D36E44", StringComparison.Ordinal), "职业主动技能首槽地址缺失。");
		Require(classPatch.Contains("00D36E74", StringComparison.Ordinal), "职业被动技能首槽地址缺失。");
		classEditor.RecordId = 73;
		classPatch = ModPatchGenerator.Generate(classEditor, ModTarget.Asia);
		Require(classPatch.Contains("00D395A4", StringComparison.Ordinal), "第 73 个职业没有使用统一技能记录步长。");
		classEditor.RecordId = 1;

		ModModule mine = modules.Single(module => module.Key == "mine_editor");
		mine.RecordId = 2;
		Require(mine.ValueA == 5 && mine.ValueB == 3 && mine.ValueC == 150 && mine.ValueD == 1, "切换采矿槽位时没有载入原版记录。");
		mine.RecordId = 0;

		ModModule randomizer = modules.Single(module => module.Key == "character_randomizer");
		String tiered = ModPatchGenerator.Generate(randomizer, ModTarget.Asia);
		randomizer.MixPromotionTiers = true;
		String mixed = ModPatchGenerator.Generate(randomizer, ModTarget.Asia);
		Require(!String.Equals(tiered, mixed, StringComparison.Ordinal), "混合转职阶段必须改变角色置换结果。");
		randomizer.MixPromotionTiers = false;
	}

	private static void Require(bool condition, String message)
	{
		if (!condition) throw new InvalidDataException(message);
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
