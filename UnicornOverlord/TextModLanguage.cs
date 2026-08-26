namespace UnicornOverlord;

internal sealed record TextModLanguage(String Name, String CpkFileName)
{
	public static IReadOnlyList<TextModLanguage> All { get; } =
	[
		new("简体中文", "Unicorn_CN.CPK"),
		new("繁体中文", "Unicorn_TW.CPK"),
		new("英语", "Unicorn_US.CPK"),
		new("韩语", "Unicorn_KO.CPK"),
		new("日语（本体）", "Unicorn.CPK"),
	];
}
