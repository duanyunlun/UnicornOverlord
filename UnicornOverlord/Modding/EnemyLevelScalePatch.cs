using System.Security.Cryptography;
using System.Text;

namespace UnicornOverlord;

internal static class EnemyLevelScalePatch
{
	private const String BuildId = "C841FFE2717FF03A13990480C51DA73F091C04FA";
	private const String TemplateFile = "enemy_level_scale_western.pchtxt";
	private const String TemplateHash = "1A1BBA2254633F3B705DB528C7616AB42961DDEDD952FD4D1E88B1F1719F4AFC";

	public static String Generate(ModTarget target)
	{
		ArgumentNullException.ThrowIfNull(target);
		if (target.Key != "western" || target.GameVersion != "v1.05" ||
			target.TitleId != "010069401ADB8000" || target.BuildId != BuildId)
			throw new NotSupportedException("敌军等级动态缩放仅支持欧美版 v1.0.5（指定 Build ID）；亚洲版的代码引用和代码洞尚未可靠校准，禁止导出。");
		return ReadValidatedTemplate();
	}

	public static void Validate() => ReadValidatedTemplate();

	private static String ReadValidatedTemplate()
	{
		String path = Path.Combine(AppContext.BaseDirectory, "mods", TemplateFile);
		String content = File.ReadAllText(path);
		String canonical = String.Join("\n", content.Split('\n').Select(line => line.Trim())
			.Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal)));
		if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))) != TemplateHash)
			throw new InvalidDataException("敌军等级缩放模板与上游 395732f 的完整三文件写入不一致；禁止修改 Build ID、offset_shift、地址、相对分支或代码洞字节。");
		return content;
	}
}
