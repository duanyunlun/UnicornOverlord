using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace UnicornOverlord;

internal static class ModPatchGenerator
{
	private const uint SkillBase = 0x02787F28;
	private const uint SkillStride = 0x130;
	private const uint FortBase = 0x00D4D67C;
	private const uint FortStride = 0x10;
	private const uint MineBase = 0x00D523F8;
	private const uint MineStride = 0x18;
	private const uint ShopBase = 0x00D46A58;
	private const uint ShopStride = 0x14;
	private const uint ItemPriceBase = 0x02716188;
	private const uint ItemStride = 0xB8;
	private const uint ClassGrowthBase = 0x00D2DFCC;
	private const uint ClassGrowthStride = 0x58;

	public static String Generate(ModModule module, ModTarget target)
	{
		List<PatchWrite> writes = module.Key switch
		{
			"ability_editor" => GenerateAbility(module),
			"class_editor" => GenerateClass(module),
			"fort_editor" => GenerateFort(module),
			"mine_editor" => GenerateMine(module),
			"shop_editor" => GenerateShop(module),
			"type_matchups" => GenerateTypeMatchups(module),
			"six_member_units" => GenerateSixMember(module),
			_ => [],
		};

		if (writes.Count > 0) return WritePchtxt(writes, target);
		String? templateFile = ResolveTemplateFile(module, target);
		if (String.IsNullOrEmpty(templateFile))
			throw new InvalidOperationException($"{module.Name} 尚未配置补丁生成器。");

		String source = Path.Combine(AppContext.BaseDirectory, "mods", templateFile);
		if (!File.Exists(source)) throw new FileNotFoundException($"缺少 MOD 模板：{templateFile}", source);
		String content = File.ReadAllText(source);
		if (module.Key == "character_randomizer") content = ApplyCharacterRandomizer(content, module.ValueA);
		if (module.Key == "six_member_units")
		{
			content = ReplaceWrite(content, 0x00B1ACAC, UInt32Bytes(CheckedUInt(module.ValueA, 0, 999999, "扩编费用")));
		}
		return content;
	}

	private static String? ResolveTemplateFile(ModModule module, ModTarget target)
	{
		if (target != ModTarget.Western)
		{
			return module.Key == "battle_preview"
				? (module.RecordId == 1 ? "battle_preview_imperfect.pchtxt" : "battle_preview_hidden.pchtxt")
				: module.TemplateFile;
		}
		return module.Key switch
		{
			"battle_preview" => module.RecordId == 1 ? "battle_preview_imperfect_western.pchtxt" : "battle_preview_hidden_western.pchtxt",
			"battle_timer_freeze" => "battle_timer_freeze_western.pchtxt",
			"character_randomizer" => "character_randomizer_western_base.pchtxt",
			"six_member_units" => "six_member_units_western.pchtxt",
			_ => module.TemplateFile,
		};
	}

	private static String ApplyCharacterRandomizer(String template, int seed)
	{
		int[] ids = [12, 13, 15, 16, 20, 21, 23, 27, 29, 32, 36, 37, 38, 41, 43, 46, 52, 60, 61, 63, 72,
			73, 75, 76, 77, 78, 79, 82, 83, 84, 86, 100, 108, 109, 115, 116, 121, 129, 130, 131, 133,
			142, 143, 144, 145, 146, 148, 153, 156, 157, 163, 164, 167, 168, 169, 171, 172, 191, 192, 193, 194, 195, 196];
		int[] shuffled = [.. ids];
		var random = new Random(seed);
		for (int index = shuffled.Length - 1; index > 0; index--)
		{
			int other = random.Next(index + 1);
			(shuffled[index], shuffled[other]) = (shuffled[other], shuffled[index]);
		}
		byte[] sigma = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
		byte[] inverse = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
		for (int index = 0; index < ids.Length; index++) sigma[ids[index]] = (byte)shuffled[index];
		for (int index = 0; index < sigma.Length; index++) inverse[sigma[index]] = (byte)index;
		return template
			.Replace("{{CHARACTER_SIGMA_TABLE}}", Convert.ToHexString(sigma), StringComparison.Ordinal)
			.Replace("{{CHARACTER_SIGMA_INVERSE_TABLE}}", Convert.ToHexString(inverse), StringComparison.Ordinal);
	}

	private static List<PatchWrite> GenerateAbility(ModModule module)
	{
		uint id = CheckedUInt(module.RecordId, 0, 2047, "技能 ID");
		uint address = SkillBase + id * SkillStride;
		return
		[
			new(address + (module.ValueN == 1 ? 0x0Cu : 0x0Au), UInt16Bytes(CheckedUShort(module.ValueA, 0, 10, "AP/PP 消耗")), module.ValueN == 1 ? "PP 消耗" : "AP 消耗"),
			new(address + 0x18, FloatBytes(module.ValueD), "物理威力"),
			new(address + 0x1C, FloatBytes(module.ValueE), "魔法威力"),
			new(address + 0x22, UInt16Bytes(CheckedUShort(module.ValueB, 0, 999, "命中")), "命中"),
			new(address + 0x28, [CheckedByte(module.ValueC, 0, 255, "目标范围")], "目标范围"),
			new(address + 0x3C, FloatBytes(module.ValueF), "效果强度"),
		];
	}

	private static List<PatchWrite> GenerateClass(ModModule module)
	{
		uint id = CheckedUInt(module.RecordId, 0, 73, "职业 ID");
		uint growth = ClassGrowthBase + id * ClassGrowthStride;
		double[] values = [module.ValueD, module.ValueE, module.ValueF, module.ValueG, module.ValueH,
			module.ValueI, module.ValueJ, module.ValueK, module.ValueL, module.ValueM];
		String[] names = ["生命", "物攻", "物防", "魔攻", "魔防", "命中", "闪避", "暴击", "格挡", "速度"];
		var writes = new List<PatchWrite>();
		for (int index = 0; index < values.Length; index++)
		{
			if (values[index] < 0 || values[index] > 1000) throw new InvalidOperationException($"{names[index]}成长必须在 0 到 1000 之间。");
			writes.Add(new PatchWrite(growth + (uint)(index * 4), FloatBytes(values[index]), $"{names[index]}成长"));
		}
		uint? skills = id switch { 1 => 0x00D36E40, 21 => 0x00D37930, _ => null };
		if (skills.HasValue)
		{
			int ap = CheckedInt(module.ValueA, 1, 4, "AP");
			int pp = CheckedInt(module.ValueB, 1, 4, "PP");
			for (int index = 0; index < 4; index++)
			{
				writes.Add(new PatchWrite(skills.Value + 0x20u + (uint)(index * 4), UInt32Bytes(index < ap ? 1u : 0u), "AP 点数"));
				writes.Add(new PatchWrite(skills.Value + 0x50u + (uint)(index * 4), UInt32Bytes(index < pp ? 1u : 0u), "PP 点数"));
			}
		}
		return writes;
	}

	private static List<PatchWrite> GenerateFort(ModModule module)
	{
		uint slot = CheckedUInt(module.RecordId, 1, 248, "据点槽位");
		return [new(FortBase + slot * FortStride, UInt32Bytes(CheckedUInt(module.ValueA, 0, 73, "职业 ID")), $"据点槽位 {slot} 的职业")];
	}

	private static List<PatchWrite> GenerateMine(ModModule module)
	{
		uint slot = CheckedUInt(module.RecordId, 0, 62, "采矿槽位");
		uint address = MineBase + slot * MineStride;
		return
		[
			new(address, UInt32Bytes(CheckedUInt(module.ValueA, 0, 970, "物品 ID")), "掉落物品"),
			new(address + 4, UInt32Bytes(CheckedUInt(module.ValueB, 0, 1000000, "权重")), "掉落权重"),
			new(address + 8, UInt32Bytes(CheckedUInt(module.ValueC, 0, 1000000, "挖掘目标")), "挖掘目标"),
			new(address + 16, UInt32Bytes(CheckedUInt((int)module.ValueD, 1, 999999, "单局上限")), "单局掉落上限"),
		];
	}

	private static List<PatchWrite> GenerateShop(ModModule module)
	{
		uint slot = CheckedUInt(module.RecordId, 0, 1, "已标定商店槽位");
		uint address = ShopBase + slot * ShopStride;
		uint itemId = CheckedUInt(module.ValueA, 0, 970, "物品 ID");
		uint priceAddress = ItemPriceBase + itemId * ItemStride;
		ushort price = CheckedUShort(module.ValueC, 0, UInt16.MaxValue, "金币买价");
		return
		[
			new(address + 4, UInt32Bytes(itemId), "商品"),
			new(address + 12, Int32Bytes(CheckedInt(module.ValueB, -1, 9999, "库存")), "库存，-1 为无限"),
			new(priceAddress, UInt16Bytes(price), "全局金币买价"),
			new(priceAddress + 4, UInt16Bytes((ushort)(price / 10)), "全局金币卖价"),
		];
	}

	private static List<PatchWrite> GenerateTypeMatchups(ModModule module)
	{
		return
		[
			new(0x000451CC, FmovImmediate(module.ValueD), "骑兵对步兵倍率"),
			new(0x000451EC, FmovImmediate(module.ValueE), "弓兵对飞行倍率"),
			new(0x00045208, FmovImmediate(module.ValueF), "飞龙/狮鹫对骑兵倍率"),
		];
	}

	private static List<PatchWrite> GenerateSixMember(ModModule module) => [];

	private static String WritePchtxt(IEnumerable<PatchWrite> writes, ModTarget target)
	{
		var builder = new StringBuilder();
		builder.AppendLine($"@nsobid-{target.BuildId}");
		builder.AppendLine("@flag offset_shift 0x100");
		builder.AppendLine("@enabled");
		foreach (PatchWrite write in writes.OrderBy(write => write.Address))
		{
			builder.AppendLine($"// {write.Comment}");
			builder.Append(write.Address.ToString("X8", CultureInfo.InvariantCulture));
			builder.Append(' ');
			builder.AppendLine(Convert.ToHexString(write.Data));
		}
		builder.AppendLine("@stop");
		return builder.ToString();
	}

	private static String ReplaceWrite(String pchtxt, uint address, byte[] data)
	{
		String prefix = address.ToString("X8", CultureInfo.InvariantCulture) + " ";
		String[] lines = pchtxt.Replace("\r\n", "\n").Split('\n');
		for (int index = 0; index < lines.Length; index++)
		{
			if (lines[index].StartsWith(prefix, StringComparison.Ordinal)) lines[index] = prefix + Convert.ToHexString(data);
		}
		return String.Join('\n', lines);
	}

	private static byte[] FmovImmediate(double value)
	{
		Dictionary<double, uint> instructions = new()
		{
			[0.5] = 0x1E2C1000, [0.75] = 0x1E2D1000, [1] = 0x1E2E1000,
			[1.25] = 0x1E2E9000, [1.5] = 0x1E2F1000, [2] = 0x1E201000,
			[2.5] = 0x1E209000, [3] = 0x1E211000, [4] = 0x1E221000,
			[5] = 0x1E229000, [6] = 0x1E231000, [8] = 0x1E241000,
			[10] = 0x1E249000,
		};
		if (!instructions.TryGetValue(value, out uint instruction))
			throw new InvalidOperationException("克制倍率只能选择 0.5、0.75、1、1.25、1.5、2、2.5、3、4、5、6、8 或 10。");
		return UInt32Bytes(instruction);
	}

	private static byte[] UInt16Bytes(ushort value) { byte[] data = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(data, value); return data; }
	private static byte[] UInt32Bytes(uint value) { byte[] data = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(data, value); return data; }
	private static byte[] Int32Bytes(int value) { byte[] data = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(data, value); return data; }
	private static byte[] FloatBytes(double value) => UInt32Bytes(BitConverter.SingleToUInt32Bits(checked((float)value)));
	private static uint CheckedUInt(int value, int min, int max, String name) => checked((uint)CheckedInt(value, min, max, name));
	private static ushort CheckedUShort(int value, int min, int max, String name) => checked((ushort)CheckedInt(value, min, max, name));
	private static byte CheckedByte(int value, int min, int max, String name) => checked((byte)CheckedInt(value, min, max, name));
	private static int CheckedInt(int value, int min, int max, String name)
	{
		if (value < min || value > max) throw new InvalidOperationException($"{name} 必须在 {min} 到 {max} 之间。");
		return value;
	}
}
