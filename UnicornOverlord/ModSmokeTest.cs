using System.IO.Compression;
using System.Text.Json;

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
			Require(ability.AbilityTypeText == "被动技能（PP）", "技能 372 应从游戏数据识别为被动技能。");
			Require(!String.IsNullOrWhiteSpace(ability.AbilityDescription), "技能说明没有从亚洲版游戏数据载入。");
			Require(ModSearchPicker.Matches("幽世瘴气", ability.SelectedSkill), "长下拉框必须能按当前语言的完整名称匹配技能。");
		Require(ModSearchPicker.Matches("瘴气", ability.SelectedSkill), "长下拉框必须能按技能名称片段匹配。");
		Require(!ModSearchPicker.Matches("372", ability.SelectedSkill), "长下拉框不应再按内部 ID 匹配技能。");
		ability.AbilityFilterIndex = 1;
		Require(ability.FilteredSkillChoices.Count == ModCatalog.ActiveSkillChoicesWithoutEmpty.Count &&
			ability.FilteredSkillChoices.All(choice => ModCatalog.ActiveSkillChoicesWithoutEmpty.Contains(choice)), "主动技能筛选结果不完整。");
		Require(!ModCatalog.Skills.First(skill => skill.Choice.Value == ability.RecordId).IsPassive, "切换到主动技能筛选后没有选择有效的主动技能。");
		Require(ability.AbilityTypeText == "主动技能（AP）", "主动技能筛选后固有类型没有同步。");
		ability.AbilityFilterIndex = 2;
		Require(ability.FilteredSkillChoices.Count == ModCatalog.PassiveSkillChoicesWithoutEmpty.Count &&
			ability.FilteredSkillChoices.All(choice => ModCatalog.PassiveSkillChoicesWithoutEmpty.Contains(choice)), "被动技能筛选结果不完整。");
		Require(ModCatalog.Skills.First(skill => skill.Choice.Value == ability.RecordId).IsPassive, "切换到被动技能筛选后没有选择有效的被动技能。");
		Require(ability.AbilityTypeText == "被动技能（PP）", "被动技能筛选后固有类型没有同步。");
			ability.AbilityFilterIndex = 0;
			ability.RecordId = 372;
			int ability372Cost = ability.ValueA == 10 ? 9 : ability.ValueA + 1;
			ability.ValueA = ability372Cost;
			ability.RecordId = 28;
			ability.ValueB++;
			ability.RecordId = 372;
			Require(ability.ValueA == ability372Cost, "切换技能后先前的修改没有保留。");
			String abilityPatch = ModPatchGenerator.Generate(ability, ModTarget.Asia);
			Require(abilityPatch.Contains("027A38F4", StringComparison.Ordinal), "被动技能消耗必须写入 PP 字段。");
			Require(abilityPatch.Contains($"{0x02787F28u + 28u * 0x130u + 0x22u:X8}", StringComparison.Ordinal), "技能补丁没有累计第二个技能的修改。");

			ModModule classEditor = modules.Single(module => module.Key == "class_editor");
			classEditor.ValueD++;
			double class1Growth = classEditor.ValueD;
			classEditor.RecordId = 73;
			classEditor.ValueE++;
			classEditor.RecordId = 1;
			Require(classEditor.ValueD == class1Growth, "切换职业后先前的修改没有保留。");
			String classPatch = ModPatchGenerator.Generate(classEditor, ModTarget.Asia);
			Require(classPatch.Contains("00D36E44", StringComparison.Ordinal), "职业主动技能首槽地址缺失。");
			Require(classPatch.Contains("00D36E74", StringComparison.Ordinal), "职业被动技能首槽地址缺失。");
			Require(classPatch.Contains("00D395A4", StringComparison.Ordinal), "第 73 个职业没有使用统一技能记录步长。");

		ModModule mine = modules.Single(module => module.Key == "mine_editor");
		MineEditorState mining = mine.Mine ?? throw new InvalidDataException("采矿编辑状态没有初始化。");
		Require(mining.Locations.Count == 5 && mining.SelectedLocation?.Records.Count == 11, "采矿地点级联没有载入科尔尼亚采掘场。");
		for (int locationIndex = 0; locationIndex < mining.Locations.Count; locationIndex++)
		{
			MineLocationState location = mining.Locations[locationIndex];
			mining.SelectedLocation = location;
			Require(ReferenceEquals(mining.SelectedLocation, location) && mining.SelectedRecord != null && location.Records.Count > 0,
				$"切换到 {location.DisplayName} 时地点或原版掉落为空。");
			MineRecordEdit last = location.Records[^1];
			mining.SelectedRecord = last;
			Require(ReferenceEquals(mining.SelectedLocation, location) && ReferenceEquals(mining.SelectedRecord, last),
				$"切换 {location.DisplayName} 的原版掉落后地点选择丢失。");
		}
		mining.SelectedLocation = mining.Locations[0];
		mining.PropertyChanged += (_, args) =>
		{
			if (args.PropertyName == nameof(MineEditorState.SelectedLocation)) mining.SelectedRecord = null;
		};
		mining.SelectedLocation = mining.Locations[1];
		Require(mining.SelectedRecord?.RecordId == 11 && mining.SelectedLocation.Records.Count == 11, "控件回写空选择后没有恢复地点首条掉落记录。");
		mining.SelectedLocation = mining.Locations[0];
		mining.SelectedRecord = mining.SelectedLocation.Records.Single(record => record.RecordId == 2);
		Require(mining.SelectedItem?.Value == 5 && mining.Weight == 3 && mining.DigTarget == 150 && mining.RoundLimit == 1, "切换采矿记录时没有载入原版配置。");
		mining.Weight = 4;
		mining.SelectedLocation = mining.Locations[1];
		mining.Weight = 51;
		Require(mining.ModifiedCount == 2, "采矿编辑状态没有累积多个地点的修改。");
		String minePatch = ModPatchGenerator.Generate(mine, ModTarget.Asia);
		Require(minePatch.Contains("00D5242C", StringComparison.Ordinal) && minePatch.Contains("00D52500", StringComparison.Ordinal), "采矿补丁没有同时写入多个已修改槽位。");

			ModModule fort = modules.Single(module => module.Key == "fort_editor");
			Require(fort.FortLocations.Count == 63 && fort.FortRecordsAtLocation.Count == 3, "据点地点级联没有载入索力吉堡垒。");
			fort.ValueA = fort.ValueA == 1 ? 2 : 1;
			int fort1Class = fort.ValueA;
			fort.SelectedFortLocation = fort.FortLocations[1];
			Require(fort.RecordId == 4 && fort.FortRecordsAtLocation.Count == 3, "切换据点时没有筛选对应招募位置。");
			fort.ValueA = fort.ValueA == 1 ? 2 : 1;
			fort.SelectedFortLocation = fort.FortLocations[0];
			Require(fort.ValueA == fort1Class, "切换据点后先前的招募修改没有保留。");
			String fortPatch = ModPatchGenerator.Generate(fort, ModTarget.Asia);
			Require(fortPatch.Contains("00D4D68C", StringComparison.Ordinal) && fortPatch.Contains("00D4D6BC", StringComparison.Ordinal), "据点补丁没有累计两个招募位置的修改。");

			ModModule shop = modules.Single(module => module.Key == "shop_editor");
			Require(shop.ShopRecordsAtLocation.Count == 7, "帕雷比亚镇武具店应显示 2 个专属商品和 5 个共享商品。");
			Require(ReferenceEquals(shop.ShopRecordsAtLocation, shop.ShopRecordsAtLocation), "同一商店地点的商品列表必须保持稳定实例，避免界面重复清空选择。");
			Require(shop.SelectedShopRecordIndex == 0, "商店初始商品索引应指向第一条记录。");
			shop.SelectedShopRecordIndex = -1;
			Require(shop.SelectedShopRecordIndex == 0, "界面刷新产生的空索引不应清除商店商品选择。");
			shop.ValueB++;
			int shop0Stock = shop.ValueB;
			shop.SelectedShopLocation = ModCatalog.ShopLocations.Single(location => location.EnglishName.StartsWith("Ouvrir Harbor", StringComparison.Ordinal));
			Require(shop.ShopRecordsAtLocation.Count == 7 && shop.SelectedShopRecordIndex == 0 && shop.ValueA == 386, "切换商店地点时没有载入该地点首个原版商品。");
			int ouvrirRecordId = shop.RecordId;
			shop.SelectedShopRecordIndex = 1;
			Require(shop.RecordId != ouvrirRecordId, "商店商品索引没有切换到第二条记录。");
			shop.SelectedShopRecordIndex = 0;
			shop.ValueB++;
			shop.SelectedShopLocation = ModCatalog.ShopLocations[0];
			Require(shop.ValueB == shop0Stock, "切换商店后先前的库存修改没有保留。");
			String shopPatch = ModPatchGenerator.Generate(shop, ModTarget.Asia);
			Require(shopPatch.Contains($"{ModCatalog.ShopRecords[0].Address + 12:X8}", StringComparison.Ordinal) &&
				shopPatch.Contains($"{ModCatalog.ShopRecords[ouvrirRecordId].Address + 12:X8}", StringComparison.Ordinal), "商店补丁没有累计两个地点的库存修改。");

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
			ZipArchiveEntry projectEntry = archive.GetEntry("mod-project.json") ?? throw new InvalidDataException("MOD 包缺少项目中间状态 mod-project.json。");
			using (var projectReader = new StreamReader(projectEntry.Open()))
			{
				using JsonDocument project = JsonDocument.Parse(projectReader.ReadToEnd());
				String[] keys = project.RootElement.GetProperty("modules").EnumerateArray()
					.Select(element => element.GetProperty("Key").GetString() ?? String.Empty).ToArray();
				if (!keys.Contains("ability_editor", StringComparer.Ordinal) || !keys.Contains("shop_editor", StringComparer.Ordinal))
					throw new InvalidDataException("项目中间状态没有记录所有已选模块。");
			}
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
