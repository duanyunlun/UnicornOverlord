using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace UnicornOverlord;

internal static class MissionModPatch
{
	private const uint UnitBase = 0x28120B8, CharaBase = 0x276DD68, TacticsBase = 0x270AF48;
	private const uint ClassSkillBase = 0xD36D94, SkillBase = 0x2787F28, EquipTypeBase = 0xD13E30;
	private const int CharaCount = 1388, PresetCount = 358;
	private static readonly uint[] AccessoryBranches = [0xDD138, 0xDD150, 0xDD198, 0xDD1B0, 0xDD1F8, 0xDD210];

	public static string Generate(JsonObject edits, ModTarget target, bool includeEngineFix = true) => Generate(edits, target, MissionModCatalog.Root, includeEngineFix);

	private static string Generate(JsonObject edits, ModTarget target, JsonObject catalog, bool includeEngineFix = true)
	{
		ArgumentNullException.ThrowIfNull(edits);
		ArgumentNullException.ThrowIfNull(catalog);
		if (target != ModTarget.Western && target != ModTarget.Asia)
			throw new ArgumentException("任务补丁只支持已校准的亚洲/欧美 v1.0.5 BuildID。", nameof(target));
		if (Rows(edits, "class_equiptypes").Count != 0)
			throw new ArgumentException("不支持 class_equiptypes 编辑。", nameof(edits));

		var charas = Index(catalog, "charasets", "id");
		var presets = Index(catalog, "equipaiset_presets", "id");
		var skills = Index(catalog, "skills", "id");
		var items = Index(catalog, "items", "id");
		var conditions = Index(catalog, "equipai_if", "id");
		var classes = Index(catalog, "class_tactics", "class_id");
		var equipTypes = Index(catalog, "equiptype_items", "id");
		var units = new HashSet<int>();
		var users = new Dictionary<int, HashSet<(int Unit, int Slot)>>();
		var usedPresets = new HashSet<int>();
		foreach (JsonObject mission in Objects(Rows(catalog, "missions")))
		foreach (JsonObject squad in Objects(Rows(mission, "squads")))
		{
			int unit = Int(squad, "unitset_id", 0, 2099);
			units.Add(unit);
			foreach (JsonObject slot in Objects(Rows(squad, "slots")))
			{
				int position = Int(slot, "slot", 0, 5);
				int chara = Int(slot, "charaset_id", 0, CharaCount - 1);
				if (!users.TryGetValue(chara, out var references)) users.Add(chara, references = []);
				references.Add((unit, position));
				usedPresets.Add(Int(slot, "equipaiset_id", 0, PresetCount - 1, 0));
			}
		}
		foreach (var preset in presets)
			if (preset.Value["usage"] == null || Int(preset.Value, "usage", 0, int.MaxValue) > 0)
				usedPresets.Add(preset.Key);

		var writes = new SortedDictionary<uint, byte>();
		void Write(uint address, byte[] bytes)
		{
			for (int offset = 0; offset < bytes.Length; offset++)
			{
				uint at = checked(address + (uint)offset);
				if (at >= CharaBase && at < CharaBase + 2 * 0x48)
					throw new ArgumentException("禁止写入保留 CharaSet 0/1。");
				if (writes.TryGetValue(at, out byte old) && old != bytes[offset])
					throw new ArgumentException($"补丁写入冲突：0x{at:X8}。");
				writes[at] = bytes[offset];
			}
		}
		void Word(uint address, uint value)
		{
			byte[] bytes = new byte[4];
			BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
			Write(address, bytes);
		}
		void Half(uint address, int value)
		{
			byte[] bytes = new byte[2];
			BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)value));
			Write(address, bytes);
		}
		int Item(JsonObject row, string key, int fallback = 0) => Known(Int(row, key, 0, ushort.MaxValue, fallback), items, key, true);
		int Condition(JsonObject line, string key) => Known(Int(line, key, 0, 202, 0), conditions, key, true);
		int Skill(JsonObject line)
		{
			int skill = Int(line, "skill_id", 0, 470, 0);
			return skill is >= 3 and <= 10 ? skill : Known(skill, skills, "skill_id", true);
		}
		byte[] Tactics(JsonArray lines)
		{
			if (lines.Count > 8) throw new ArgumentException("战术行最多 8 行，禁止截断。");
			byte[] bytes = new byte[0x48];
			int position = 0;
			foreach (JsonObject line in Objects(lines))
			{
				Int(line, "slot", 0, 7, position);
				int action = Int(line, "action", 0, 10, 3);
				if (action == 0) action = 3;
				int skill = Skill(line);
				if (skill == 0 && action is >= 3 and <= 10) skill = action;
				int if0 = Condition(line, "if0"), if1 = Condition(line, "if1");
				if (line["learn_level"] != null) Int(line, "learn_level", 1, int.MaxValue);
				if (skill != 0)
				{
					BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(position * 8), (ushort)if0);
					BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(position * 8 + 2), (ushort)if1);
					BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(position * 8 + 4), (uint)skill);
				}
				position++;
			}
			return bytes;
		}

		var editedSlots = new Dictionary<(int Unit, int Slot), JsonObject>();
		foreach (JsonObject unitEdit in Objects(Rows(edits, "unitsets")))
		{
			int unit = Int(unitEdit, "unitset_id", 0, 2099);
			if (!units.Contains(unit)) throw new ArgumentException($"目录中不存在 UnitSet {unit}。");
			JsonArray slots = Rows(unitEdit, "slots");
			if (slots.Count > 6) throw new ArgumentException("UnitSet 最多 6 个槽。");
			foreach (JsonObject slot in Objects(slots))
			{
				int position = Int(slot, "slot", 0, 5);
				if (!editedSlots.TryAdd((unit, position), slot)) throw new ArgumentException("重复的 UnitSet 槽编辑。");
				Known(Int(slot, "charaset_id", 0, CharaCount - 1), charas, "charaset_id", true);
				Int(slot, "equipaiset_id", int.MinValue, PresetCount - 1);
				Number(slot, "flags", 0, uint.MaxValue, 0);
				Bool(slot, "use_duplicate", true);
			}
		}

		JsonObject overrides = edits["equipaiset_lines"] == null ? new() : Object(edits["equipaiset_lines"]);
		var reservedPresets = new HashSet<int>(usedPresets);
		foreach (var entry in overrides)
			reservedPresets.Add(PresetKey(entry.Key, presets));
		foreach (JsonObject slot in editedSlots.Values)
		{
			int preset = Int(slot, "equipaiset_id", int.MinValue, PresetCount - 1);
			if (preset > 0) reservedPresets.Add(Known(preset, presets, "equipaiset_id"));
		}
		var aliases = new Dictionary<string, int>(StringComparer.Ordinal);
		var privateSlots = new Dictionary<(int Unit, int Slot), int>();
		var allocated = new HashSet<int>();
		var requests = Objects(Rows(edits, "equipaiset_creates")).Select(row => (Row: row, Create: true))
			.Concat(Objects(Rows(edits, "equipaiset_allocations")).Select(row => (Row: row, Create: false))).ToArray();
		var explicitIds = new HashSet<int>();
		foreach (var request in requests)
			if (request.Row["new_id"] != null)
			{
				int wanted = Known(Int(request.Row, "new_id", 1, PresetCount - 1), presets, "new_id");
				if (reservedPresets.Contains(wanted) || !explicitIds.Add(wanted))
					throw new ArgumentException($"EquipAiSet {wanted} 不可用于分配：已使用或被编辑引用。");
			}
		void Alias(string key, int id)
		{
			if (!aliases.TryAdd(key, id)) throw new ArgumentException($"重复的预设分配键：{key}。");
		}
		foreach (var request in requests)
		{
			JsonObject row = request.Row;
			int source = Int(row, "source_id", 0, PresetCount - 1, Int(row, "from_id", 0, PresetCount - 1, 0));
			Known(source, presets, "source_id", true);
			string? key = Text(row, "key");
			if (request.Create && string.IsNullOrWhiteSpace(key)) throw new ArgumentException("新预设必须提供 key。");
			int id = row["new_id"] != null ? Int(row, "new_id", 1, PresetCount - 1) :
				Enumerable.Range(1, PresetCount - 1).Reverse().FirstOrDefault(candidate =>
					presets.ContainsKey(candidate) && !reservedPresets.Contains(candidate) && !explicitIds.Contains(candidate));
			if (id == 0) throw new ArgumentException("没有可用的 EquipAiSet 槽。");
			reservedPresets.Add(id);
			allocated.Add(id);
			Write(TacticsBase + (uint)id * 0x48, Tactics(Rows(row, "lines")));
			if (!string.IsNullOrWhiteSpace(key)) Alias(key, id);
			if (request.Create && row["temp_id"] != null)
				Alias(Int(row, "temp_id", int.MinValue, -1).ToString(CultureInfo.InvariantCulture), id);
			if (!request.Create)
			{
				bool hasUnit = row["unitset_id"] != null, hasSlot = row["slot"] != null;
				if (hasUnit != hasSlot) throw new ArgumentException("私有预设须同时指定 unitset_id 和 slot。");
				if (hasUnit)
				{
					var position = (Int(row, "unitset_id", 0, 2099), Int(row, "slot", 0, 5));
					if (!editedSlots.ContainsKey(position) || !privateSlots.TryAdd(position, id))
						throw new ArgumentException("私有预设目标必须是唯一的已编辑 UnitSet 槽。");
				}
				else if (string.IsNullOrWhiteSpace(key))
				{
					if (source == 0) throw new ArgumentException("私有预设必须提供源预设、key 或目标槽。");
					Alias(source.ToString(CultureInfo.InvariantCulture), id);
				}
			}
		}

		var editedCharas = new HashSet<int>();
		var charaEdits = Objects(Rows(edits, "charasets")).ToArray();
		foreach (JsonObject row in charaEdits)
		{
			int id = Known(Int(row, "charaset_id", 2, CharaCount - 1), charas, "charaset_id");
			if (!editedCharas.Add(id)) throw new ArgumentException("重复的 CharaSet 编辑。");
			JsonArray gear = Rows(row, "gear");
			if (gear.Count > 4) throw new ArgumentException("装备最多 4 槽，禁止截断。");
			bool copyShared = Bool(row, "duplicate_if_shared", true);
			bool changed = false;
			foreach (JsonObject item in Objects(gear))
			{
				Item(item, "item_id");
				Item(item, "rom_item_id");
				changed |= Bool(item, "edited", false);
			}
			if (!changed) continue;
			int count = users.TryGetValue(id, out var references) ? references.Count : 0;
			bool shared = charas[id]["usage"] == null || Math.Max(count, Int(charas[id], "usage", 0, int.MaxValue)) > 1;
			if (shared && copyShared)
				throw new ArgumentException($"CharaSet {id} 已共享或缺少完整引用计数；允许修改所有使用者须显式设置 duplicate_if_shared=false。");
			for (int position = 0; position < gear.Count; position++)
			{
				JsonObject item = Object(gear[position]);
				if (!Bool(item, "edited", false)) continue;
				int value = Item(item, "item_id");
				Half(CharaBase + (uint)id * 0x48 + 0x38 + (uint)position * 2, value);
			}
		}

		var boundAllocations = new HashSet<int>();
		foreach (var entry in editedSlots)
		{
			JsonObject slot = entry.Value;
			int chara = Int(slot, "charaset_id", 0, CharaCount - 1);
			int preset = Int(slot, "equipaiset_id", int.MinValue, PresetCount - 1);
			string? key = Text(slot, "equipaiset_alloc_key");
			if (!string.IsNullOrWhiteSpace(key))
			{
				if (!aliases.TryGetValue(key, out preset)) throw new ArgumentException($"未解析的预设分配键：{key}。");
				if (privateSlots.TryGetValue(entry.Key, out int privateId) && privateId != preset)
					throw new ArgumentException("私有预设目标与分配键冲突。");
			}
			else if (privateSlots.TryGetValue(entry.Key, out int privateId)) preset = privateId;
			else if (aliases.TryGetValue(preset.ToString(CultureInfo.InvariantCulture), out int mapped)) preset = mapped;
			if (preset < 0) throw new ArgumentException("未解析的临时 EquipAiSet ID。");
			Known(preset, presets, "equipaiset_id", true);
			if (allocated.Contains(preset)) boundAllocations.Add(preset);
			uint address = UnitBase + (uint)entry.Key.Unit * 0x88 + 0x3C + (uint)entry.Key.Slot * 0xC;
			Word(address, (uint)chara);
			Word(address + 4, (uint)preset);
			Word(address + 8, (uint)Number(slot, "flags", 0, uint.MaxValue, 0));
		}
		foreach (var request in requests.Where(request => !request.Create))
		{
			string? key = Text(request.Row, "key");
			if (string.IsNullOrWhiteSpace(key) && request.Row["unitset_id"] == null)
				key = Int(request.Row, "source_id", 0, PresetCount - 1, Int(request.Row, "from_id", 0, PresetCount - 1, 0)).ToString(CultureInfo.InvariantCulture);
			if (!string.IsNullOrWhiteSpace(key) && !boundAllocations.Contains(aliases[key])) throw new ArgumentException("私有预设未绑定任何已编辑槽。");
		}
		foreach (var entry in overrides)
			Write(TacticsBase + (uint)PresetKey(entry.Key, presets) * 0x48, Tactics(Array(entry.Value)));

		var editedClasses = new HashSet<int>();
		foreach (JsonObject row in Objects(Rows(edits, "class_tactics")))
		{
			int id = Known(Int(row, "class_id", 0, 73), classes, "class_id");
			if (!editedClasses.Add(id)) throw new ArgumentException("重复的职业战术编辑。");
			JsonArray lines = Rows(row, "lines");
			if (lines.Count > 8) throw new ArgumentException("职业战术最多 8 行。");
			var slots = new Dictionary<int, JsonObject>();
			foreach (JsonObject line in Objects(lines))
			{
				int action = Int(line, "action", 3, 10, 3);
				if (!slots.TryAdd(action, line)) throw new ArgumentException("重复的职业技能槽。");
				if (line["slot"] != null) Int(line, "slot", 0, 7);
			}
			for (int action = 3; action <= 10; action++)
			{
				uint offset = action <= 6 ? 0x20u + (uint)(action - 3) * 8 : 0x50u + (uint)(action - 7) * 8;
				uint address = ClassSkillBase + (uint)id * 0x8C + offset;
				uint skill = 0, level = 0;
				if (slots.TryGetValue(action, out JsonObject? line))
				{
					skill = (uint)Known(Int(line, "skill_id", 0, 470, 0), skills, "skill_id", true);
					level = (uint)Int(line, "learn_level", 1, int.MaxValue, 1);
					int if0 = Condition(line, "if0"), if1 = Condition(line, "if1");
					if (skill > 0)
					{
						Word(SkillBase + skill * 0x130 + 0xAC, (uint)if0);
						Word(SkillBase + skill * 0x130 + 0xB0, (uint)if1);
					}
					else if (if0 != 0 || if1 != 0) throw new ArgumentException("空技能不能写入全局 IF。");
				}
				Word(address, level);
				Word(address + 4, skill);
			}
		}
		var editedTypes = new HashSet<int>();
		foreach (JsonObject row in Objects(Rows(edits, "equiptype_items")))
		{
			int id = Known(Int(row, "equiptype_id", 0, 55, Int(row, "id", 0, 55, 0)), equipTypes, "equiptype_id");
			if (!editedTypes.Add(id)) throw new ArgumentException("重复的默认装备表编辑。");
			for (int column = 0; column < 3; column++) Half(EquipTypeBase + (uint)id * 0xC + (uint)column * 2, Item(row, $"item_col{column}_id"));
		}
		if (includeEngineFix)
			foreach (uint address in AccessoryBranches) Word(address, 0xD503201F);
		var result = new StringBuilder($"@nsobid-{target.BuildId}\n@flag offset_shift 0x100\n@enabled\n");
		using var enumerator = writes.GetEnumerator();
		bool more = enumerator.MoveNext();
		while (more)
		{
			uint start = enumerator.Current.Key;
			var bytes = new List<byte>();
			do
			{
				bytes.Add(enumerator.Current.Value);
				more = enumerator.MoveNext();
			} while (more && bytes.Count < 4 && enumerator.Current.Key == start + bytes.Count);
			result.Append(start.ToString("X8", CultureInfo.InvariantCulture)).Append(' ').Append(Convert.ToHexString(bytes.ToArray())).Append('\n');
		}
		return result.Append("@stop\n").ToString();
	}

	private static JsonObject Object(JsonNode? node) => node as JsonObject ?? throw new ArgumentException("预期 JSON 对象。");
	private static JsonArray Array(JsonNode? node) => node as JsonArray ?? throw new ArgumentException("预期 JSON 数组。");
	private static JsonArray Rows(JsonObject row, string key) => row[key] == null ? new() : Array(row[key]);
	private static IEnumerable<JsonObject> Objects(JsonArray rows) => rows.Select(Object);
	private static Dictionary<int, JsonObject> Index(JsonObject catalog, string key, string id)
		=> Objects(Rows(catalog, key)).ToDictionary(row => Int(row, id, 0, int.MaxValue));
	private static long Number(JsonObject row, string key, long min, long max, long? fallback = null)
	{
		if (row[key] == null && fallback.HasValue) return fallback.Value;
		if (row[key] is not JsonValue value || !decimal.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number) ||
			number != decimal.Truncate(number) || number < min || number > max)
			throw new ArgumentException($"{key} 必须是 {min}..{max} 范围内的整数。");
		return (long)number;
	}
	private static int Int(JsonObject row, string key, int min, int max, int? fallback = null) => (int)Number(row, key, min, max, fallback);
	private static bool Bool(JsonObject row, string key, bool fallback)
	{
		if (row[key] == null) return fallback;
		return row[key] is JsonValue value && value.TryGetValue<bool>(out bool flag) ? flag : throw new ArgumentException($"{key} 必须是布尔值。");
	}
	private static string? Text(JsonObject row, string key)
	{
		if (row[key] == null) return null;
		return row[key] is JsonValue value && value.TryGetValue<string>(out string? text) ? text : throw new ArgumentException($"{key} 必须是字符串。");
	}
	private static int Known(int id, Dictionary<int, JsonObject> rows, string field, bool zero = false)
		=> (zero && id == 0) || rows.ContainsKey(id) ? id : throw new ArgumentException($"目录中不存在 {field}={id}。");
	private static int PresetKey(string key, Dictionary<int, JsonObject> presets)
	{
		if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id is <= 0 or >= PresetCount)
			throw new ArgumentException($"无效的 EquipAiSet 键：{key}。");
		return Known(id, presets, "equipaiset_id");
	}
	public static void Validate()
	{
		JsonObject catalog = Object(JsonNode.Parse("""
			{"charasets":[{"id":2,"usage":2},{"id":3,"usage":0}],"skills":[{"id":28},{"id":442}],"items":[{"id":1}],
			"equipai_if":[{"id":0},{"id":13}],"class_tactics":[{"class_id":1}],"equiptype_items":[{"id":1}],
			"equipaiset_presets":[{"id":1,"usage":2},{"id":2,"usage":0},{"id":3,"usage":0}],
			"missions":[{"squads":[{"unitset_id":101,"slots":[{"slot":0,"charaset_id":2,"equipaiset_id":1},{"slot":1,"charaset_id":2,"equipaiset_id":1}]}]}]}
			"""));
		string Export(string json) => Generate(Object(JsonNode.Parse(json)), ModTarget.Western, catalog);
		void Require(bool valid) { if (!valid) throw new InvalidOperationException("MissionModPatch 自检失败。"); }
		void Reject(string json)
		{
			try { Export(json); }
			catch (ArgumentException) { return; }
			throw new InvalidOperationException($"MissionModPatch 未拒绝非法输入：{json}");
		}
		Require(Export("{}").Contains("000DD138 1F2003D5", StringComparison.Ordinal));
		Require(Generate(new(), ModTarget.Asia, catalog).StartsWith("@nsobid-" + ModTarget.Asia.BuildId, StringComparison.Ordinal));
		Require(Export("""{"equipaiset_lines":{"1":[{"skill_id":28,"if0":13}]}}""")
			.Contains("0270AF90 0D000000\n0270AF94 1C000000", StringComparison.Ordinal));
		Require(Export("""{"class_tactics":[{"class_id":1,"lines":[{"action":3,"skill_id":28,"learn_level":10,"if0":13}]}]}""")
			.Contains("00D36E40 0A000000", StringComparison.Ordinal));
		Require(Export("""{"class_tactics":[{"class_id":1,"lines":[{"action":7,"skill_id":442,"learn_level":5,"if0":13}]}]}""")
			.Contains("027A8CB4 0D000000", StringComparison.Ordinal));
		Require(Export("""{"equiptype_items":[{"id":1,"item_col0_id":1}]}""").Contains("00D13E3C 01000000", StringComparison.Ordinal));
		Reject("""{"class_equiptypes":[{}]}""");
		Reject("""{"equipaiset_lines":{"0":[]}}""");
		Reject("""{"equipaiset_lines":{"1":[{},{},{},{},{},{},{},{},{}]}}""");
		Reject("""{"equipaiset_lines":{"1":[{"if0":203}]}}""");
		Reject("""{"equipaiset_lines":{"1":[{"skill_id":1.5}]}}""");
		Reject("""{"equipaiset_lines":{"1":[{"slot":8}]}}""");
		Reject("""{"equipaiset_lines":{"1":[{"skill_id":4294967296}]}}""");
		Reject("""{"equipaiset_creates":[{"key":"x","new_id":1}]}""");
		Reject("""{"equipaiset_creates":[{"key":"x","new_id":2},{"key":"y","new_id":2}]}""");
		Reject("""{"equipaiset_creates":[{"key":"x"},{"key":"y"},{"key":"z"}]}""");
		Reject("""{"charasets":[{"charaset_id":1,"gear":[]}]}""");
		Reject("""{"charasets":[{"charaset_id":2,"gear":[{},{},{},{},{}]}]}""");
		Reject("""{"charasets":[{"charaset_id":2,"gear":[{"edited":true,"item_id":65536}]}]}""");
		Reject("""{"charasets":[{"charaset_id":2,"duplicate_if_shared":"false"}]}""");
		Reject("""{"charasets":[{"charaset_id":2,"gear":[{"edited":true,"item_id":1}]}]}""");
		Reject("""{"unitsets":[{"unitset_id":101,"slots":[{"slot":6,"charaset_id":2,"equipaiset_id":0}]}]}""");
		Reject("""{"unitsets":[{"unitset_id":101,"slots":[{"slot":0,"charaset_id":2,"equipaiset_id":0,"flags":4294967296}]}]}""");
		Reject("""{"unitsets":[{"unitset_id":101,"slots":[{"slot":0,"charaset_id":2,"equipaiset_id":-1}]}]}""");
		Reject("""{"class_tactics":[{"class_id":1,"lines":[{"action":3},{"action":3}]}]}""");
		Reject("""{"class_tactics":[{"class_id":1,"lines":[{"action":3,"skill_id":28,"if0":13},{"action":4,"skill_id":28,"if0":0}]}]}""");
		Require(Export("""{"charasets":[{"charaset_id":2,"duplicate_if_shared":false,"gear":[{"edited":true,"item_id":1}]}]}""")
			.Contains("0276DE30 0100", StringComparison.Ordinal));
		Require(!Generate(new JsonObject { ["class_tactics"] = new JsonArray(new JsonObject { ["class_id"] = 1 }) }, ModTarget.Western, catalog, false)
			.Contains("000DD138", StringComparison.Ordinal));
		string allocated = Export("""{"equipaiset_allocations":[{"source_id":1,"unitset_id":101,"slot":0,"lines":[{"action":3}]}],"unitsets":[{"unitset_id":101,"slots":[{"slot":0,"charaset_id":2,"equipaiset_id":1},{"slot":1,"charaset_id":2,"equipaiset_id":1}]}]}""");
		uint first = UnitBase + 101 * 0x88 + 0x3C;
		Require(allocated.Contains($"{first + 4:X8} 03000000", StringComparison.Ordinal) && allocated.Contains($"{first + 16:X8} 01000000", StringComparison.Ordinal));
		string created = Export("""{"equipaiset_creates":[{"key":"new","temp_id":-1,"lines":[{"skill_id":28}]}],"unitsets":[{"unitset_id":101,"slots":[{"slot":5,"charaset_id":0,"equipaiset_id":-1,"flags":4294967295}]}]}""");
		Require(created.Contains($"{first + 64:X8} 03000000", StringComparison.Ordinal) && created.Contains($"{first + 68:X8} FFFFFFFF", StringComparison.Ordinal));
	}
}
