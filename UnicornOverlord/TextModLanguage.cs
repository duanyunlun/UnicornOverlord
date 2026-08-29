namespace UnicornOverlord;

internal sealed class TextModLanguage
{
	private readonly String mSourceName;

	public String Name => LocaleManager.Instance.Translate(mSourceName);
	public String CpkFileName { get; }

	private TextModLanguage(String name, String cpkFileName)
	{
		mSourceName = name;
		CpkFileName = cpkFileName;
	}

	public override String ToString() => Name;

	public static IReadOnlyList<TextModLanguage> All { get; } =
	[
		new("简体中文", "Unicorn_CN.CPK"),
		new("繁体中文", "Unicorn_TW.CPK"),
		new("英语", "Unicorn_US.CPK"),
		new("韩语", "Unicorn_KO.CPK"),
		new("日语（本体）", "Unicorn.CPK"),
	];
}
