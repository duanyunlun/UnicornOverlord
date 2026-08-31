using System.Collections.ObjectModel;

namespace UnicornOverlord;

internal static class ModModuleCatalog
{
	public static ObservableCollection<ModModule> CreateModules() => CreateModules(new ModProjectState());

	public static ObservableCollection<ModModule> CreateModules(ModProjectState project) =>
	[
		new() { Project = project, Key = "ability_editor", Category = "技能", Name = "技能编辑器", Description = "从 441 个已校准技能中选择主动或被动技能，修改其 AP/PP 消耗、威力、命中、目标范围和首个效果参数。技能类型由游戏数据决定，不能手动互换。", IsAvailable = true, CalibrationState = "441 个技能已校准" },
		new() { Project = project, Key = "battle_preview", Category = "战斗", Name = "战斗预览调整", Description = "“不完美预览”用 5 次模拟的平均值展示大致趋势；“完全隐藏”则移除整个预览条。", IsAvailable = true, CalibrationState = "亚洲版已重定位", TemplateFile = "battle_preview_hidden.pchtxt" },
		new() { Project = project, Key = "battle_timer_freeze", Category = "战斗", Name = "冻结战斗计时器", Description = "冻结关卡实时计时器，战斗不再受时间限制。", IsAvailable = true, TemplateFile = "battle_timer_freeze.pchtxt" },
		new() { Project = project, Key = "unlimited_battle_start", Category = "战斗", Name = "解除开战被动限制", Description = "战斗开始时触发的被动技能不再限制为每支队伍只能由一名角色发动；多个开场技能会按顺序完整结算。", IsAvailable = true, CalibrationState = "两版写入点已校准", TemplateFile = "unlimited_battle_start.pchtxt" },
		new() { Project = project, Key = "type_matchups", Category = "战斗", Name = "类型克制", Description = "设置游戏内三种固有兵种克制倍率。它会作用于对应单位的所有攻击，并与技能自身的“对某类型威力加成”叠加；不写入存档，可随时启停。", IsAvailable = true, CalibrationState = "三项已校准" },
		new() { Project = project, Key = "character_randomizer", Category = "角色", Name = "角色加入随机化", Description = "随机改变教程五人以外的 63 名剧情角色加入顺序；过场、地图事件和能力触发时点不变。", IsAvailable = true, CalibrationState = "亚洲版已重定位", TemplateFile = "character_randomizer_base.pchtxt", Warning = "实验性功能：只用于新游戏，全流程保持启用并备份存档；中途移除可能使剧情读取不同步。" },
		new() { Project = project, Key = "class_editor", Category = "职业", Name = "职业编辑器", Description = "按职业名称修改 73 个职业的十项成长率、AP/PP，以及 4 个主动和 4 个被动技能及习得等级。", IsAvailable = true, CalibrationState = "73 个职业字段已校准" },
		new() { Project = project, Key = "fort_editor", Category = "据点", Name = "据点雇佣编辑器", Description = "按 63 个具体据点选择全部 248 个招募位置并修改可招募职业；选择后会载入原版职业，手动选择不受转职阶段限制。", IsAvailable = true, CalibrationState = "63 个据点 / 248 项已校准", Warning = "仅写职业字段，亚洲版记录中的性别与附加类型保持不变。" },
		new() { Project = project, Key = "mine_editor", Category = "采矿", Name = "采矿掉落编辑器", Description = "按五个地区采掘场选择 63 条具体原版掉落，修改物品、相对权重、挖掘目标和单局上限。", IsAvailable = true, CalibrationState = "5 个采掘场 / 63 项已校准", Warning = "藏宝图等一次性物品由游戏另行限制；提高权重时也要检查单局上限。" },
		new() { Project = project, Key = "shop_editor", Category = "商店", Name = "商店库存编辑器", Description = "按科尔尼亚的具体地图地点选择武具店和原版商品，修改商品、库存与金币价格；共享库存会明确标识。", IsAvailable = true, CalibrationState = "25 个武具店 / 211 个地点条目已校准", Warning = "当前接入科尔尼亚普通武具店；兑换所价格结构不同，不会错误套用金币价格。" },
		new() { Project = project, Key = "six_member_units", Category = "编队", Name = "六人编队", Description = "允许 S 级声望下将部队扩充至六人，并可设置荣誉费用。", IsAvailable = true, CalibrationState = "亚洲版已重定位", TemplateFile = "six_member_units.pchtxt", Warning = "卸载前必须先撤下所有部队的第六名成员。" },
		new() { Project = project, Key = "text_editor", Category = "文本", Name = "文本编辑器", Description = "基于所选语言 CPK 的原始 FMS 按索引修改文本，不改动源归档。", IsAvailable = true, CalibrationState = "CPK 文本表编辑" },
	];

	public static IReadOnlyList<ModCategory> CreateCategories(IReadOnlyList<ModModule> modules)
	{
		IReadOnlyDictionary<String, ModModule> modulesByKey = modules.ToDictionary(module => module.Key, StringComparer.Ordinal);
		ModModule Find(String key) => modulesByKey[key];
		return
		[
			new("技能", [Find("ability_editor")]),
			new("战斗", [Find("battle_preview"), Find("battle_timer_freeze"), Find("unlimited_battle_start"), Find("type_matchups")]),
			new("角色", [Find("character_randomizer")]),
			new("职业", [Find("class_editor")]),
			new("据点", [Find("fort_editor")]),
			new("采矿", [Find("mine_editor")]),
			new("商店", [Find("shop_editor")]),
			new("编队", [Find("six_member_units")]),
			new("文本", [Find("text_editor")]),
		];
	}
}
