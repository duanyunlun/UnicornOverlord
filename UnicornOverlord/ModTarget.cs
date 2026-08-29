namespace UnicornOverlord;

internal sealed record ModTarget(String Key, String Name, String GameVersion, String TitleId, String BuildId)
{
	public static ModTarget Asia { get; } = new("asia", "亚洲中文版", "v1.0.5", "010054B01AD92000", "9C3116F0333EA157526612D17354B3755737C4F2");
	public static ModTarget Western { get; } = new("western", "欧美版", "v1.05", "010069401ADB8000", "C841FFE2717FF03A13990480C51DA73F091C04FA");
	public static IReadOnlyList<ModTarget> All { get; } = [Asia, Western];
	public String DisplayName => $"{LocaleManager.Instance.Translate(Name)} {GameVersion}";
}
