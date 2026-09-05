using System.IO.Compression;
using System.Text.Json.Nodes;

namespace UnicornOverlord;

internal static class ModIntegrationSmokeTest
{
	public static void Run(String directory)
	{
		MissionModPatch.Validate();
		MissionEditorState.Validate();
		ExperienceScalePatch.Validate();
		EnemyLevelScalePatch.Validate();
		Reject(() => ExperienceScalePatch.Generate(2, ModTarget.Asia));
		Reject(() => EnemyLevelScalePatch.Generate(ModTarget.Asia));
		Reject(() => ExperienceScalePatch.Generate(Double.NaN, ModTarget.Western));
		Require(MissionModCatalog.Rows("missions").Count() == 90, "任务目录不完整。");
		Require(MissionModCatalog.Rows("equipaiset_presets").Count() == 357, "战术预设目录不完整。");
		var project = new ModProjectState();
		ModModule[] modules = [.. ModModuleCatalog.CreateModules(project)];
		var categories = ModModuleCatalog.CreateCategories(modules);
		Require(categories.Select(category => category.SourceName).SequenceEqual(new[] { "技能", "战斗", "角色", "职业", "据点", "采矿", "商店", "编队", "文本" }), "已有 MOD 分类被改动。");
		ModModule classes = modules.Single(module => module.IsClassEditor);
		ModModule mission = modules.Single(module => module.IsMissionEditor);
		ModModule experience = modules.Single(module => module.IsExperienceScale);
		ModModule levels = modules.Single(module => module.Key == "enemy_level_scale");
		ModSkillSlot active = project.Classes.SelectedRecord.ActiveSkills[0];
		int skillId = active.SelectedSkill!.Value;
		var defaults = project.Classes.Conditions.Get(skillId);
		int changedCondition = defaults.First == 0 ? 1 : 0;
		project.Classes.Conditions.Set(skillId, changedCondition, defaults.Second);
		Require(active.SelectedCondition0?.Value == changedCondition, "职业技能没有读取共享条件状态。");
		String conditionsPatch = ModPatchGenerator.Generate(classes, ModTarget.Asia);
		Require(conditionsPatch.Contains($"{0x2787F28 + skillId * 0x130 + 0xAC:X8}", StringComparison.Ordinal), "条件-only 修改没有进入职业模块补丁。");
		project.Missions.Edits["unitsets"] = JsonNode.Parse("""[{"unitset_id":101,"slots":[{"slot":1,"charaset_id":565,"equipaiset_id":0,"flags":256}]}]""");
		project.Missions.Edits["equiptype_items"] = JsonNode.Parse("""[{"equiptype_id":23,"item_col0_id":282,"item_col1_id":283,"item_col2_id":284}]""");
		String gearPatch = ModPatchGenerator.Generate(classes, ModTarget.Asia);
		Require(gearPatch.Contains($"{0xD13E30 + 23 * 12:X8}", StringComparison.Ordinal), "默认装备没有随职业模块导出。");
		Require(!gearPatch.Contains("000DD138", StringComparison.Ordinal), "职业-only 导出不应夹带饰品引擎补丁。");
		String missionPatch = ModPatchGenerator.Generate(mission, ModTarget.Asia);
		Require(!missionPatch.Contains($"{0xD13E30 + 23 * 12:X8}", StringComparison.Ordinal), "编队模块重复导出了职业模块的默认装备。");
		String path = Path.Combine(directory, "mission-integration-asia.zip");
		ModPackageBuilder.Create(path, [mission, classes], ModTarget.Asia);
		using (var archive = ZipFile.OpenRead(path))
		{
			ZipArchiveEntry entry = archive.GetEntry("mission_editor_edits.json") ?? throw new InvalidDataException("缺少上游兼容编辑状态。");
			using var reader = new StreamReader(entry.Open());
			JsonObject snapshot = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
			Require(snapshot["edits"]?["class_tactics"]?.AsArray().Count > 0, "默认条件没有进入工程快照。");
			Require(snapshot["edits"]?["equiptype_items"]?.AsArray().Count == 1, "默认装备没有进入工程快照。");
			Require(archive.GetEntry("THIRD_PARTY_MODS.md") != null, "发行包缺少上游 MIT 通知。");
		}
		project.ExperienceMultiplier = 2;
		ModPackageBuilder.Create(Path.Combine(directory, "mission-integration-western.zip"), [mission, classes, experience, levels], ModTarget.Western);
		project.Classes.Conditions.Set(skillId, defaults.First, defaults.Second);
		Require(project.Classes.Conditions.ModifiedRecords.Count == 0, "恢复默认条件仍残留修改。");
		Console.WriteLine("新增 MOD 集成自检通过：分类保留、双版本编队、职业默认条件/装备、工程快照、欧美经验/等级、亚洲运行时补丁拒绝。");
	}

	private static void Require(bool condition, String message)
	{
		if (!condition) throw new InvalidDataException(message);
	}
	private static void Reject(Action action)
	{
		try { action(); }
		catch (ArgumentException) { return; }
		catch (NotSupportedException) { return; }
		throw new InvalidDataException("无效或未校准的输入没有被拒绝。");
	}
}
