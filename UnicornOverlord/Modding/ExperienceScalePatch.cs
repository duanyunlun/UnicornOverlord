using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace UnicornOverlord;

internal static class ExperienceScalePatch
{
	private const String BuildId = "C841FFE2717FF03A13990480C51DA73F091C04FA";
	private static readonly Dictionary<double, String> TemplateHashes = new()
	{
		[0.1] = "C6A87F26A521C29A762AC75352D30113B3E0140A34B94130800C994AB2605BBF",
		[0.25] = "BFEA5BFAFFCBA8F0044E9B1EDF64F56976BD1B8EF947BA581842F6AF10604C5C",
		[0.5] = "3ECEA65A61DAC79B441B3812D1ECA883E2857EFBBCB4F2490006BE036120FE9F",
		[0.75] = "425BE47A0BBBB37E68D75D814430B5F8F210DAFB6AF998B6AE7682E47203AE8C",
		[1] = "E10D16B7629C7EFEAC95290259E2F67439671D4B1F6AB622DCEE2225D2103D9C",
		[1.25] = "8C4F993295B912C7CB190B5F74F4AD10DCD05EA8589E8F8BDC8F2F29BAC08072",
		[1.5] = "6EB199C9FA72B6ACCAD15EB42A0823E5E4823B30D0206B830F91D89B674A3CA1",
		[2] = "D0DAF54A19CDFB63AC58AC52EB26C06413008DA4643AB52C80D8A85417418926",
		[10] = "5B0C396D81183C4E6B070F684AF6320214D856A153601B91F55206B22D8907BD",
	};

	public static IReadOnlyList<double> Multipliers { get; } = Array.AsReadOnly(TemplateHashes.Keys.ToArray());

	public static String Generate(double multiplier, ModTarget target)
	{
		ArgumentNullException.ThrowIfNull(target);
		if (!TemplateHashes.TryGetValue(multiplier, out String? expectedHash))
			throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier,
				"战斗经验倍率仅接受 0.1、0.25、0.5、0.75、1、1.25、1.5、2、10，不进行近似或截断。");
		if (target.Key != "western" || target.GameVersion != "v1.05" ||
			target.TitleId != "010069401ADB8000" || target.BuildId != BuildId)
			throw new NotSupportedException("战斗经验倍率仅支持欧美版 v1.0.5（指定 Build ID，上游来源校验，未本地 NSO 验证）；亚洲版五个钩子原指令虽匹配，但上游代码洞位于其只读数据段，尚无可靠可执行写入点，禁止导出。");
		String filename = $"experience_scale_western_{multiplier.ToString(CultureInfo.InvariantCulture)}.pchtxt";
		String content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "mods", filename));
		String canonical = String.Join("\n", content.Split('\n').Select(line => line.Trim())
			.Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal)));
		if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))) != expectedHash)
			throw new InvalidDataException("战斗经验倍率模板与上游 395732f release 不一致；禁止修改 Build ID、offset_shift、地址、分支或代码洞字节。");
		return content;
	}

	public static void Validate()
	{
		foreach (double multiplier in Multipliers) Generate(multiplier, ModTarget.Western);
		foreach (double multiplier in new double[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity,
			-1, 0, 0.099999, 0.3, 1.0000000000000002, 10.000001, double.MaxValue })
		{
			try { Generate(multiplier, ModTarget.Western); }
			catch (ArgumentOutOfRangeException) { continue; }
			throw new InvalidDataException("战斗经验倍率接受了不支持的倍率。");
		}
		foreach (ModTarget target in new[] { ModTarget.Asia, ModTarget.Western with { GameVersion = "v1.0.4" },
			ModTarget.Western with { BuildId = "UNKNOWN" }, ModTarget.Western with { TitleId = ModTarget.Asia.TitleId },
			ModTarget.Western with { Key = "unknown" } })
		{
			try { Generate(1, target); }
			catch (NotSupportedException) { continue; }
			throw new InvalidDataException("战斗经验倍率接受了不支持的版本。");
		}
		try { Generate(1, null!); }
		catch (ArgumentNullException) { return; }
		throw new InvalidDataException("战斗经验倍率接受了空目标。");
	}
}
