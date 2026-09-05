namespace UnicornOverlord;

internal sealed class ModSkillSlot : ObservableObject
{
	private ModChoice? mSelectedSkill;
	private int mLevel;
	private SkillConditionState? mConditions;

	public required int Index { get; init; }
	public required bool IsPassive { get; init; }
	public IReadOnlyList<ModChoice> Choices => IsPassive ? ModCatalog.PassiveSkillChoices : ModCatalog.ActiveSkillChoices;
	public String SlotName => LocaleManager.Instance.Format("第 {0} 项", Index + 1);
	public bool IsFirst => Index == 0;
	public bool CanEditLevel => Index > 0;
	public bool CanEditConditions => (SelectedSkill?.Value ?? 0) > 0;
	public IReadOnlyList<ModChoice> ConditionChoices => MissionModCatalog.Conditions;
	public ModChoice? SelectedCondition0
	{
		get => ConditionChoices.FirstOrDefault(choice => choice.Value == (mConditions?.Get(SelectedSkill?.Value ?? 0).First ?? 0));
		set { if (CanEditConditions && value != null && mConditions != null) mConditions.Set(SelectedSkill!.Value, value.Value, mConditions.Get(SelectedSkill.Value).Second); }
	}
	public ModChoice? SelectedCondition1
	{
		get => ConditionChoices.FirstOrDefault(choice => choice.Value == (mConditions?.Get(SelectedSkill?.Value ?? 0).Second ?? 0));
		set { if (CanEditConditions && value != null && mConditions != null) mConditions.Set(SelectedSkill!.Value, mConditions.Get(SelectedSkill.Value).First, value.Value); }
	}
	public void BindConditions(SkillConditionState state)
	{
		mConditions = state;
		state.PropertyChanged += (_, _) => Notify(nameof(SelectedCondition0), nameof(SelectedCondition1));
	}

	public ModChoice? SelectedSkill
	{
		get => mSelectedSkill;
		set
		{
			SetField(ref mSelectedSkill, value, nameof(SelectedSkill));
			Notify(nameof(SelectedCondition0), nameof(SelectedCondition1), nameof(CanEditConditions));
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

	public void RefreshLocale() => Notify(nameof(SlotName), nameof(ConditionChoices), nameof(SelectedCondition0), nameof(SelectedCondition1));
}
