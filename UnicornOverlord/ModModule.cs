using System.ComponentModel;

namespace UnicornOverlord;

internal sealed class ModModule : INotifyPropertyChanged
{
	private bool mIsSelected;
	private int mRecordId;
	private int mValueA;
	private int mValueB;
	private int mValueC;
	private double mValueD;
	private double mValueE;
	private double mValueF;
	private double mValueG;
	private double mValueH;
	private double mValueI;
	private double mValueJ;
	private double mValueK;
	private double mValueL;
	private double mValueM;
	private int mValueN;

	public event PropertyChangedEventHandler? PropertyChanged;

	public required String Key { get; init; }
	public required String Category { get; init; }
	public required String Name { get; init; }
	public required String Description { get; init; }
	public required bool IsAvailable { get; init; }
	public String? TemplateFile { get; init; }
	public String? Warning { get; init; }
	public String? CalibrationState { get; init; }
	public String StateText => CalibrationState ?? (IsAvailable ? "已接入" : "待解析");
	public bool IsAbilityEditor => Key == "ability_editor";
	public bool IsBattlePreview => Key == "battle_preview";
	public bool IsCharacterRandomizer => Key == "character_randomizer";
	public bool IsClassEditor => Key == "class_editor";
	public bool IsFortEditor => Key == "fort_editor";
	public bool IsMineEditor => Key == "mine_editor";
	public bool IsShopEditor => Key == "shop_editor";
	public bool IsSixMemberUnits => Key == "six_member_units";
	public bool IsTypeMatchups => Key == "type_matchups";
	public bool HasNoOptions => Key == "battle_timer_freeze";
	public bool CanEditClassPoints => RecordId is 1 or 21;
	public IReadOnlyList<String> PreviewModes { get; } = ["完全隐藏", "不完美预览"];
	public IReadOnlyList<double> MatchupValues { get; } = [0.5, 0.75, 1, 1.25, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10];
	public IReadOnlyList<String> CostTypes { get; } = ["主动技能（AP）", "被动技能（PP）"];

	public int RecordId
	{
		get => mRecordId;
		set
		{
			SetField(ref mRecordId, value, nameof(RecordId));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanEditClassPoints)));
		}
	}
	public int ValueA { get => mValueA; set => SetField(ref mValueA, value, nameof(ValueA)); }
	public int ValueB { get => mValueB; set => SetField(ref mValueB, value, nameof(ValueB)); }
	public int ValueC { get => mValueC; set => SetField(ref mValueC, value, nameof(ValueC)); }
	public double ValueD { get => mValueD; set => SetField(ref mValueD, value, nameof(ValueD)); }
	public double ValueE { get => mValueE; set => SetField(ref mValueE, value, nameof(ValueE)); }
	public double ValueF { get => mValueF; set => SetField(ref mValueF, value, nameof(ValueF)); }
	public double ValueG { get => mValueG; set => SetField(ref mValueG, value, nameof(ValueG)); }
	public double ValueH { get => mValueH; set => SetField(ref mValueH, value, nameof(ValueH)); }
	public double ValueI { get => mValueI; set => SetField(ref mValueI, value, nameof(ValueI)); }
	public double ValueJ { get => mValueJ; set => SetField(ref mValueJ, value, nameof(ValueJ)); }
	public double ValueK { get => mValueK; set => SetField(ref mValueK, value, nameof(ValueK)); }
	public double ValueL { get => mValueL; set => SetField(ref mValueL, value, nameof(ValueL)); }
	public double ValueM { get => mValueM; set => SetField(ref mValueM, value, nameof(ValueM)); }
	public int ValueN { get => mValueN; set => SetField(ref mValueN, value, nameof(ValueN)); }

	public bool IsSelected
	{
		get => mIsSelected;
		set
		{
			if (!IsAvailable || mIsSelected == value) return;
			mIsSelected = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
		}
	}

	private void SetField<T>(ref T field, T value, String propertyName)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return;
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
