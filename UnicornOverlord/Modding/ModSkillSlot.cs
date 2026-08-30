namespace UnicornOverlord;

internal sealed class ModSkillSlot : ObservableObject
{
	private ModChoice? mSelectedSkill;
	private int mLevel;

	public required int Index { get; init; }
	public required bool IsPassive { get; init; }
	public IReadOnlyList<ModChoice> Choices => IsPassive ? ModCatalog.PassiveSkillChoices : ModCatalog.ActiveSkillChoices;
	public String SlotName => LocaleManager.Instance.Format("第 {0} 项", Index + 1);
	public bool IsFirst => Index == 0;
	public bool CanEditLevel => Index > 0;

	public ModChoice? SelectedSkill
	{
		get => mSelectedSkill;
		set
		{
			SetField(ref mSelectedSkill, value, nameof(SelectedSkill));
		}
	}

	public int Level
	{
		get => mLevel;
		set
		{
			int normalized = Math.Clamp(value, 1, 99);
			SetField(ref mLevel, normalized, nameof(Level));
		}
	}

	public void RefreshLocale() => Notify(nameof(SlotName));
}
