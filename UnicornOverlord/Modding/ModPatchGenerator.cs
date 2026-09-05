using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace UnicornOverlord;

internal static class ModPatchGenerator
{
	private const uint SkillBase = 0x02787F28;
	private const uint SkillStride = 0x130;
	private const uint FortBase = 0x00D4D67C;
	private const uint FortStride = 0x10;
	private const uint MineBase = 0x00D523F8;
	private const uint MineStride = 0x18;
	private const uint ItemPriceBase = 0x02716188;
	private const uint ItemStride = 0xB8;
	private const uint ClassGrowthBase = 0x00D2DFCC;
	private const uint ClassGrowthStride = 0x58;
	private const uint ClassSkillBase = 0x00D36E40;
	private const uint ClassSkillStride = 0x8C;

	public static String Generate(ModModule module, ModTarget target)
	{
		if (module.Key == "experience_scale") return ExperienceScalePatch.Generate(module.Project.ExperienceMultiplier, target);
		if (module.Key == "enemy_level_scale") return EnemyLevelScalePatch.Generate(target);
		if (module.Key == "mission_editor")
		{
			var edits = (JsonObject)module.Project.MissionEdits.DeepClone();
			edits.Remove("class_tactics");
			edits.Remove("equiptype_items");
			return MissionModPatch.Generate(edits, target);
		}
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
		if (module.IsClassEditor && module.Project.MissionEdits["equiptype_items"] is JsonArray gear && gear.Count > 0)
		{
			String patch = MissionModPatch.Generate(new JsonObject { ["equiptype_items"] = gear.DeepClone() }, target, includeEngineFix: false);
			foreach (String line in patch.Split('\n'))
			{
				String[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 2 && UInt32.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint address))
					writes.Add(new PatchWrite(address, Convert.FromHexString(parts[1]), "默认装备表"));
			}
		}
		if (module.IsClassEditor && writes.Count == 0) throw new InvalidOperationException("职业编辑器尚未修改职业、默认条件或默认装备。");

		if (writes.Count > 0) return WritePchtxt(writes, target);
		String? templateFile = ResolveTemplateFile(module, target);
		if (String.IsNullOrEmpty(templateFile))
			throw new InvalidOperationException($"{module.Name} 尚未配置补丁生成器。");

		String source = Path.Combine(AppContext.BaseDirectory, "mods", templateFile);
		if (!File.Exists(source)) throw new FileNotFoundException($"缺少 MOD 模板：{templateFile}", source);
		String content = File.ReadAllText(source);
		if (module.Key == "character_randomizer") content = ApplyCharacterRandomizer(content, module.ValueA, module.MixPromotionTiers);
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
			"unlimited_battle_start" => "unlimited_battle_start_western.pchtxt",
			"character_randomizer" => "character_randomizer_western_base.pchtxt",
			"six_member_units" => "six_member_units_western.pchtxt",
			_ => module.TemplateFile,
		};
	}

	private static String ApplyCharacterRandomizer(String template, int seed, bool mixPromotionTiers)
	{
		int[] ids = [12, 13, 15, 16, 20, 21, 23, 27, 29, 32, 36, 37, 38, 41, 43, 46, 52, 60, 61, 63, 72,
			73, 75, 76, 77, 78, 79, 82, 83, 84, 86, 100, 108, 109, 115, 116, 121, 129, 130, 131, 133,
			142, 143, 144, 145, 146, 148, 153, 156, 157, 163, 164, 167, 168, 169, 171, 172, 191, 192, 193, 194, 195, 196];
		int[] shuffled = [.. ids];
		var random = new Random(seed);
		if (mixPromotionTiers)
		{
			Shuffle(shuffled, random);
		}
		else
		{
			// 两组成员由官网两次“保持相同转职阶段”补丁的置换闭包交叉校验得到。
			int[] baseTier = [12, 13, 15, 16, 20, 23, 27, 29, 32, 36, 37, 41, 43, 52, 60, 61, 63, 72, 73, 75, 76, 77, 78, 79, 82, 83, 108, 109, 196];
			int[] promotedTier = ids.Except(baseTier).ToArray();
			int[] originalBaseTier = [.. baseTier];
			int[] originalPromotedTier = [.. promotedTier];
			Shuffle(baseTier, random);
			Shuffle(promotedTier, random);
			for (int index = 0; index < ids.Length; index++)
			{
				int baseIndex = Array.IndexOf(originalBaseTier, ids[index]);
				shuffled[index] = baseIndex >= 0 ? baseTier[baseIndex] : promotedTier[Array.IndexOf(originalPromotedTier, ids[index])];
			}
		}
		byte[] sigma = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
		byte[] inverse = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
		for (int index = 0; index < ids.Length; index++) sigma[ids[index]] = (byte)shuffled[index];
		for (int index = 0; index < sigma.Length; index++) inverse[sigma[index]] = (byte)index;
		return template
			.Replace("{{CHARACTER_SIGMA_TABLE}}", Convert.ToHexString(sigma), StringComparison.Ordinal)
			.Replace("{{CHARACTER_SIGMA_INVERSE_TABLE}}", Convert.ToHexString(inverse), StringComparison.Ordinal);
	}

	private static void Shuffle<T>(T[] values, Random random)
	{
		for (int index = values.Length - 1; index > 0; index--)
		{
			int other = random.Next(index + 1);
			(values[index], values[other]) = (values[other], values[index]);
		}
	}

	private static List<PatchWrite> GenerateAbility(ModModule module)
	{
		AbilityRecordEdit[] edits = module.Project.Ability.ModifiedRecords.OrderBy(record => record.RecordId).ToArray();
		if (edits.Length == 0) throw new InvalidOperationException("技能编辑器尚未修改任何技能。");
		var writes = new List<PatchWrite>(edits.Length * 6);
		foreach (AbilityRecordEdit edit in edits)
		{
			uint id = CheckedUInt(edit.RecordId, 28, 468, "技能 ID");
			uint address = SkillBase + id * SkillStride;
			writes.Add(new(address + (edit.Original.IsPassive ? 0x0Cu : 0x0Au), UInt16Bytes(CheckedUShort(edit.Cost, 0, 10, "AP/PP 消耗")), edit.Original.IsPassive ? $"技能 {id} 的 PP 消耗" : $"技能 {id} 的 AP 消耗"));
			writes.Add(new(address + 0x18, FloatBytes(edit.PhysicalPotency), $"技能 {id} 的物理威力"));
			writes.Add(new(address + 0x1C, FloatBytes(edit.MagicalPotency), $"技能 {id} 的魔法威力"));
			writes.Add(new(address + 0x22, UInt16Bytes(CheckedUShort(edit.Accuracy, 0, 999, "命中")), $"技能 {id} 的命中"));
			writes.Add(new(address + 0x28, [CheckedByte(edit.TargetShape, 0, 255, "目标范围")], $"技能 {id} 的目标范围"));
			writes.Add(new(address + 0x3C, FloatBytes(edit.EffectValue), $"技能 {id} 的效果强度"));
		}
		return writes;
	}

	private static List<PatchWrite> GenerateClass(ModModule module)
	{
		ClassRecordEdit[] edits = module.Project.Classes.ModifiedRecords.OrderBy(record => record.RecordId).ToArray();
		var writes = new List<PatchWrite>();
		String[] names = ["生命", "物攻", "物防", "魔攻", "魔防", "命中", "闪避", "暴击", "格挡", "速度"];
		foreach (ClassRecordEdit edit in edits)
		{
			uint id = CheckedUInt(edit.RecordId, 1, 73, "职业 ID");
			uint growth = ClassGrowthBase + id * ClassGrowthStride;
			for (int index = 0; index < edit.Growths.Length; index++)
			{
				if (edit.Growths[index] < 0 || edit.Growths[index] > 1000) throw new InvalidOperationException($"职业 {id} 的{names[index]}成长必须在 0 到 1000 之间。");
				writes.Add(new PatchWrite(growth + (uint)(index * 4), FloatBytes(edit.Growths[index]), $"职业 {id} 的{names[index]}成长"));
			}
			uint skills = ClassSkillBase + (id - 1) * ClassSkillStride;
			int ap = CheckedInt(edit.Ap, 1, 4, "AP");
			int pp = CheckedInt(edit.Pp, 1, 4, "PP");
			for (int index = 0; index < 4; index++)
			{
				writes.Add(new PatchWrite(skills + 0x20u + (uint)(index * 4), UInt32Bytes(index < ap ? 1u : 0u), $"职业 {id} 的 AP 点数"));
				writes.Add(new PatchWrite(skills + 0x50u + (uint)(index * 4), UInt32Bytes(index < pp ? 1u : 0u), $"职业 {id} 的 PP 点数"));
			}
			AppendClassSkillWrites(writes, skills, edit.ActiveSkills, 0x04, $"职业 {id} 的主动");
			AppendClassSkillWrites(writes, skills, edit.PassiveSkills, 0x34, $"职业 {id} 的被动");
		}
		foreach (var edit in module.Project.Classes.Conditions.ModifiedRecords.OrderBy(entry => entry.Key))
		{
			uint address = SkillBase + (uint)edit.Key * SkillStride;
			writes.Add(new PatchWrite(address + 0xAC, UInt32Bytes((uint)edit.Value.First), $"技能 {edit.Key} 的全局默认条件 1"));
			writes.Add(new PatchWrite(address + 0xB0, UInt32Bytes((uint)edit.Value.Second), $"技能 {edit.Key} 的全局默认条件 2"));
		}
		return writes;
	}

	private static void AppendClassSkillWrites(List<PatchWrite> writes, uint address, IReadOnlyList<ModSkillSlot> slots, uint offset, String type)
	{
		for (int index = 0; index < slots.Count; index++)
		{
			uint skillId = CheckedUInt(slots[index].SelectedSkill?.Value ?? 0, 0, 468, $"{type}技能 ID");
			uint skillOffset = index == 0 ? offset : offset + (uint)(index * 8);
			writes.Add(new PatchWrite(address + skillOffset, UInt32Bytes(skillId), $"{type}技能 {index + 1}"));
			if (index > 0)
			{
				uint level = skillId == 0 ? 0u : CheckedUInt(slots[index].Level, 1, 99, $"{type}技能 {index + 1} 习得等级");
				writes.Add(new PatchWrite(address + skillOffset - 4, UInt32Bytes(level), $"{type}技能 {index + 1} 习得等级"));
			}
		}
	}

	private static List<PatchWrite> GenerateFort(ModModule module)
	{
		FortRecordEdit[] edits = module.Project.Fort.ModifiedRecords.OrderBy(record => record.RecordId).ToArray();
		if (edits.Length == 0) throw new InvalidOperationException("据点编辑器尚未修改任何招募位置。");
		return edits.Select(edit =>
		{
			uint slot = CheckedUInt(edit.RecordId, 1, 248, "据点槽位");
			return new PatchWrite(FortBase + slot * FortStride, UInt32Bytes(CheckedUInt(edit.ClassId, 0, 73, "职业 ID")), $"据点槽位 {slot} 的职业");
		}).ToList();
	}

	private static List<PatchWrite> GenerateMine(ModModule module)
	{
		MineEditorState state = module.Project.Mine;
		MineRecordEdit[] edits = state.ModifiedRecords.OrderBy(record => record.RecordId).ToArray();
		if (edits.Length == 0) throw new InvalidOperationException("采矿编辑器尚未修改任何掉落记录。");
		var writes = new List<PatchWrite>(edits.Length * 4);
		foreach (MineRecordEdit edit in edits)
		{
			uint slot = CheckedUInt(edit.RecordId, 0, 62, "采矿槽位");
			uint address = MineBase + slot * MineStride;
			writes.Add(new(address, UInt32Bytes(CheckedUInt(edit.ItemId, 0, 970, "物品 ID")), $"采矿槽位 {slot} 的掉落物品"));
			writes.Add(new(address + 4, UInt32Bytes(CheckedUInt(edit.Weight, 0, 1000000, "权重")), $"采矿槽位 {slot} 的掉落权重"));
			writes.Add(new(address + 8, UInt32Bytes(CheckedUInt(edit.DigTarget, 0, 1000000, "挖掘目标")), $"采矿槽位 {slot} 的挖掘目标"));
			writes.Add(new(address + 16, UInt32Bytes(CheckedUInt(edit.RoundLimit, 1, 999999, "单局上限")), $"采矿槽位 {slot} 的单局掉落上限"));
		}
		return writes;
	}

	private static List<PatchWrite> GenerateShop(ModModule module)
	{
		ShopRecordEdit[] edits = module.Project.Shop.ModifiedRecords.OrderBy(record => record.RecordId).ToArray();
		if (edits.Length == 0) throw new InvalidOperationException("商店编辑器尚未修改任何商品。");
		var writes = new List<PatchWrite>(edits.Length * 4);
		foreach (ShopRecordEdit edit in edits)
		{
			uint itemId = CheckedUInt(edit.ItemId, 0, 970, "物品 ID");
			uint priceAddress = ItemPriceBase + itemId * ItemStride;
			ushort price = CheckedUShort(edit.Price, 0, UInt16.MaxValue, "金币买价");
			writes.Add(new(edit.Original.Address + 4, UInt32Bytes(itemId), $"商店记录 {edit.RecordId} 的商品"));
			writes.Add(new(edit.Original.Address + 12, Int32Bytes(CheckedInt(edit.Stock, -1, 9999, "库存")), $"商店记录 {edit.RecordId} 的库存"));
			writes.Add(new(priceAddress, UInt16Bytes(price), $"物品 {itemId} 的全局金币买价"));
			writes.Add(new(priceAddress + 4, UInt16Bytes((ushort)(price / 10)), $"物品 {itemId} 的全局金币卖价"));
		}
		return writes;
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
