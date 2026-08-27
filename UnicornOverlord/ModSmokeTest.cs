using System.IO.Compression;

namespace UnicornOverlord;

internal static class ModSmokeTest
{
	public static void Run(String outputPath)
	{
		String? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
		if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
		ModModule[] modules = [.. ViewModel.CreateModModules().Where(module => !module.IsTextEditor)];
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
		Require(ModCatalog.FortRecordChoices.Count == 248 && ModCatalog.FindFortRecord(1)?.DisplayName.StartsWith("Fort Soligie", StringComparison.Ordinal) == true, "据点名称映射不完整。");
		Require(ModCatalog.MineRecordChoices.Count == 63 && ModCatalog.FindMineRecord(0)?.DisplayName.StartsWith("Cornia Quarry", StringComparison.Ordinal) == true, "采掘场名称映射不完整。");
		Require(ModCatalog.ShopLocations.Count == 25 && ModCatalog.ShopRecordChoices.Count == 211, "科尔尼亚武具店地点或商品映射不完整。");
		Require(ModCatalog.FindShopRecord(0)?.DisplayName.StartsWith("Palevia Town · Armorer", StringComparison.Ordinal) == true, "商店名称映射不正确。");
		Require(ModCatalog.FortRecordChoices.Select(choice => choice.Value).SequenceEqual(Enumerable.Range(1, 248)), "据点名称映射存在重复或缺失记录。");
		Require(ModCatalog.MineRecordChoices.Select(choice => choice.Value).SequenceEqual(Enumerable.Range(0, 63)), "采矿名称映射存在重复或缺失记录。");
		Require(ModCatalog.ShopRecordChoices.Select(choice => choice.Value).SequenceEqual(Enumerable.Range(0, 211)), "商店名称映射存在重复或缺失记录。");
		ApplicationSettings.Language = originalLanguage;
		ModCatalog.RefreshLocalizedNames();
		Require(ModCatalog.FindFortRecord(1)?.DisplayName.StartsWith("索力吉堡垒", StringComparison.Ordinal) == true, "据点名称没有跟随编辑器语言切换为中文。");
		Require(ModCatalog.FindShopRecord(0)?.DisplayName.StartsWith("帕雷比亚镇 · 武具店", StringComparison.Ordinal) == true, "商店名称没有跟随编辑器语言切换为中文。");

		ModModule ability = modules.Single(module => module.Key == "ability_editor");
		String abilityPatch = ModPatchGenerator.Generate(ability, ModTarget.Asia);
		Require(ability.AbilityTypeText == "被动技能（PP）", "技能 372 应从游戏数据识别为被动技能。");
		Require(abilityPatch.Contains("027A38F4", StringComparison.Ordinal), "被动技能消耗必须写入 PP 字段。");
		ability.AbilityFilterIndex = 1;
		Require(ability.FilteredSkillChoices.Count == ModCatalog.ActiveSkillChoicesWithoutEmpty.Count &&
			ability.FilteredSkillChoices.All(choice => ModCatalog.ActiveSkillChoicesWithoutEmpty.Contains(choice)), "主动技能筛选结果不完整。");
		Require(!ModCatalog.Skills.First(skill => skill.Choice.Value == ability.RecordId).IsPassive, "切换到主动技能筛选后没有选择有效的主动技能。");
		ability.AbilityFilterIndex = 2;
		Require(ability.FilteredSkillChoices.Count == ModCatalog.PassiveSkillChoicesWithoutEmpty.Count &&
			ability.FilteredSkillChoices.All(choice => ModCatalog.PassiveSkillChoicesWithoutEmpty.Contains(choice)), "被动技能筛选结果不完整。");
		Require(ModCatalog.Skills.First(skill => skill.Choice.Value == ability.RecordId).IsPassive, "切换到被动技能筛选后没有选择有效的被动技能。");
		ability.AbilityFilterIndex = 0;
		ability.RecordId = 372;

		ModModule classEditor = modules.Single(module => module.Key == "class_editor");
		String classPatch = ModPatchGenerator.Generate(classEditor, ModTarget.Asia);
		Require(classPatch.Contains("00D36E44", StringComparison.Ordinal), "职业主动技能首槽地址缺失。");
		Require(classPatch.Contains("00D36E74", StringComparison.Ordinal), "职业被动技能首槽地址缺失。");
		classEditor.RecordId = 73;
		classPatch = ModPatchGenerator.Generate(classEditor, ModTarget.Asia);
		Require(classPatch.Contains("00D395A4", StringComparison.Ordinal), "第 73 个职业没有使用统一技能记录步长。");
		classEditor.RecordId = 1;

		ModModule mine = modules.Single(module => module.Key == "mine_editor");
		Require(mine.MineLocations.Count == 5 && mine.MineRecordsAtLocation.Count == 11, "采矿地点级联没有载入科尔尼亚采掘场。");
		mine.SelectedMineLocation = mine.MineLocations[1];
		Require(mine.RecordId == 11 && mine.MineRecordsAtLocation.Count == 11, "切换采矿地点时没有筛选对应掉落记录。");
		mine.RecordId = 2;
		Require(mine.ValueA == 5 && mine.ValueB == 3 && mine.ValueC == 150 && mine.ValueD == 1, "切换采矿槽位时没有载入原版记录。");
		mine.RecordId = 0;

		ModModule fort = modules.Single(module => module.Key == "fort_editor");
		Require(fort.FortLocations.Count == 63 && fort.FortRecordsAtLocation.Count == 3, "据点地点级联没有载入索力吉堡垒。");
		fort.SelectedFortLocation = fort.FortLocations[1];
		Require(fort.RecordId == 4 && fort.FortRecordsAtLocation.Count == 3, "切换据点时没有筛选对应招募位置。");

		ModModule shop = modules.Single(module => module.Key == "shop_editor");
		Require(shop.ShopRecordsAtLocation.Count == 7, "帕雷比亚镇武具店应显示 2 个专属商品和 5 个共享商品。");
		shop.SelectedShopLocation = ModCatalog.ShopLocations.Single(location => location.EnglishName.StartsWith("Ouvrir Harbor", StringComparison.Ordinal));
		Require(shop.ShopRecordsAtLocation.Count == 7 && shop.ValueA == 386, "切换商店地点时没有载入该地点首个原版商品。");
		String shopPatch = ModPatchGenerator.Generate(shop, ModTarget.Asia);
		Require(shopPatch.Contains("00D46B10", StringComparison.Ordinal), "乌夫里尔武具店商品地址不正确。");

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
