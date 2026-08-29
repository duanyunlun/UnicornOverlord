using System.ComponentModel;

namespace UnicornOverlord;

internal sealed class ModSkillSlot : INotifyPropertyChanged
{
	private ModChoice? mSelectedSkill;
	private int mLevel;

	public event PropertyChangedEventHandler? PropertyChanged;
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
			if (mSelectedSkill == value) return;
			mSelectedSkill = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSkill)));
		}
	}

	public int Level
	{
		get => mLevel;
		set
		{
			int normalized = Math.Clamp(value, 1, 99);
			if (mLevel == normalized) return;
			mLevel = normalized;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level)));
		}
	}

	public void RefreshLocale() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SlotName)));
}
