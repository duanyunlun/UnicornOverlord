using System.IO.Compression;
using System.Text.Json.Nodes;

namespace UnicornOverlord;

internal static class MissionModCatalog
{
	private static readonly Lazy<JsonObject> mRoot = new(Load);
	private static readonly Lazy<IReadOnlyList<ModChoice>> mConditions = new(() => Rows("equipai_if")
		.Select(row => new ModChoice
		{
			Value = Number(row, "id"),
			EnglishName = Number(row, "id") == 0 ? "None" : Text(row, "name", Text(row, "symbol")),
			JapaneseName = Text(row, "comment", Text(row, "name", Text(row, "symbol"))),
			ChineseName = Number(row, "id") == 0 ? "无条件" : Text(row, "name", Text(row, "symbol")),
		}).ToArray());

	public static JsonObject Root => mRoot.Value;
	public static IReadOnlyList<ModChoice> Conditions => mConditions.Value;
	public static IEnumerable<JsonObject> Rows(String name) => Root[name]?.AsArray().OfType<JsonObject>() ?? [];
	public static int Number(JsonNode? node, String name, int fallback = 0) => node?[name] is JsonValue value && value.TryGetValue<int>(out int number) ? number : fallback;
	public static String Text(JsonNode? node, String name, String fallback = "") => node?[name] is JsonValue value && value.TryGetValue<String>(out String? text) && !String.IsNullOrEmpty(text) ? text : fallback;
	public static (int First, int Second) SkillConditions(int skillId)
	{
		JsonNode? row = Root["skill_default_conditions"]?[skillId.ToString(System.Globalization.CultureInfo.InvariantCulture)];
		return (Number(row, "if0"), Number(row, "if1"));
	}

	private static JsonObject Load()
	{
		using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "info", "mission_catalog.json.gz"));
		using var compressed = new GZipStream(stream, CompressionMode.Decompress);
		JsonObject root = JsonNode.Parse(compressed)?.AsObject() ?? throw new InvalidDataException("关卡 MOD 数据为空。");
		if (root["missions"]?.AsArray().Count != 90 || root["equipai_if"]?.AsArray().Count != 203 || root["equiptype_items"]?.AsArray().Count != 56)
			throw new InvalidDataException("关卡 MOD 数据版本不匹配。");
		return root;
	}
}
