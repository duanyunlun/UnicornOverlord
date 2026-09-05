namespace UnicornOverlord;

internal sealed class SkillConditionState : ObservableObject
{
	private readonly Dictionary<int, (int First, int Second)> mEdits = [];
	public IReadOnlyDictionary<int, (int First, int Second)> ModifiedRecords => mEdits;
	public (int First, int Second) Get(int skillId) => mEdits.GetValueOrDefault(skillId, MissionModCatalog.SkillConditions(skillId));
	public void Set(int skillId, int first, int second)
	{
		if (skillId is < 1 or > 468 || first is < 0 or > 202 || second is < 0 or > 202)
			throw new InvalidDataException("技能默认条件超出有效范围。");
		if ((first, second) == MissionModCatalog.SkillConditions(skillId)) mEdits.Remove(skillId);
		else mEdits[skillId] = (first, second);
		Notify(nameof(ModifiedRecords));
	}
}
