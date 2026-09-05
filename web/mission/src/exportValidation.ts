import type { ExportCatalog, ExportEdits, ExportLine } from "./exportMissionMod.ts";

function integer(value: unknown, min: number, max: number, label: string): number {
  if (typeof value !== "number" || !Number.isInteger(value) || value < min || value > max) throw new Error(`${label} 必须是 ${min}–${max} 范围内的整数`);
  return value;
}

function unique(set: Set<string | number>, key: string | number, label: string) {
  if (set.has(key)) throw new Error(`重复的${label}：${key}`);
  set.add(key);
}

export function validateEdits(input: ExportEdits, catalog: ExportCatalog, allowUnconfirmedShared = false): ExportEdits {
  if (!input || typeof input !== "object" || Array.isArray(input)) throw new Error("无效的编辑数据");
  const edits = structuredClone(input);
  for (const key of ["unitsets", "charasets", "equipaiset_creates", "equipaiset_allocations", "class_tactics", "equiptype_items", "class_equiptypes"] as const) {
    const rows = edits[key];
    if (rows != null && (!Array.isArray(rows) || rows.some(row => !row || typeof row !== "object" || Array.isArray(row)))) throw new Error(`无效的 ${key}`);
  }
  if (edits.class_equiptypes?.length) throw new Error("class_equiptypes 只读，禁止导出修改");
  const presets = new Map((catalog.equipaiset_presets ?? []).map(row => [row.id, row]));
  const charas = new Map((catalog.charasets ?? []).map(row => [row.id, row]));
  const skills = new Set((catalog.skills ?? []).map(row => row.id));
  const conditions = new Set((catalog.equipai_if ?? []).map(row => row.id));
  const items = new Set((catalog.items ?? []).map(row => row.id));
  const classes = new Set((catalog.class_tactics ?? []).map(row => row.class_id));
  const equiptypes = new Set((catalog.equiptype_items ?? []).map(row => row.id));
  const known = (value: unknown, ids: Set<number> | Map<number, unknown>, max: number, name: string, zero = false) => {
    const id = integer(value, 0, max, name);
    if (!(zero && id === 0) && !ids.has(id)) throw new Error(`目录中不存在 ${name}=${id}`);
    return id;
  };
  const presetId = (value: unknown, zero = true) => known(value, presets, 357, "EquipAiSet", zero);
  const validateLines = (rows: ExportLine[] | undefined, classMode = false) => {
    if (rows == null) return;
    if (!Array.isArray(rows) || rows.length > 8) throw new Error("战术最多 8 行，禁止截断");
    const actions = new Set<string | number>();
    rows.forEach((row, index) => {
      if (!row || typeof row !== "object" || Array.isArray(row)) throw new Error("无效战术行");
      integer(row.slot ?? index, 0, 7, "战术槽");
      const action = integer(row.action ?? 3, classMode ? 3 : 0, 10, "action");
      if (classMode) unique(actions, action, "职业技能槽");
      const skill = integer(row.skill_id ?? 0, 0, 470, "skill_id");
      if (!(skill === 0 || (!classMode && skill >= 3 && skill <= 10))) known(skill, skills, 470, "skill_id");
      known(row.if0 ?? 0, conditions, 202, "IF0", true);
      known(row.if1 ?? 0, conditions, 202, "IF1", true);
      if (classMode && !skill && (row.if0 || row.if1)) throw new Error("空技能不能写入全局 IF");
      for (const text of [row.skill_name, row.skill_symbol, row.if0_symbol, row.if1_symbol, row.ref_kind]) if (text != null && typeof text !== "string") throw new Error("战术名称必须为文本");
      if (row.learn_level != null) integer(row.learn_level, 1, 0x7fffffff, "learn_level");
    });
  };
  const units = new Set<number>();
  const users = new Map<number, Set<string>>();
  const reserved = new Set<number>();
  for (const preset of presets.values()) if (preset.usage == null || preset.usage > 0) reserved.add(preset.id);
  for (const mission of catalog.missions ?? []) for (const squad of mission.squads ?? []) {
    units.add(squad.unitset_id!);
    for (const slot of squad.slots ?? []) {
      const id = slot.charaset_id ?? 0;
      if (!users.has(id)) users.set(id, new Set());
      users.get(id)!.add(`${squad.unitset_id}:${slot.slot}`);
      if (slot.equipaiset_id) reserved.add(slot.equipaiset_id);
    }
  }
  const slots = new Map<string, NonNullable<NonNullable<ExportEdits["unitsets"]>[number]["slots"]>[number]>();
  for (const unit of edits.unitsets ?? []) {
    const id = known(unit.unitset_id, units, 2099, "UnitSet");
    if (unit.slots != null && (!Array.isArray(unit.slots) || unit.slots.length > 6)) throw new Error("编队最多六个槽");
    for (const slot of unit.slots ?? []) {
      integer(slot.slot, 0, 5, "slot");
      const key = `${id}:${slot.slot}`;
      if (slots.has(key)) throw new Error(`重复编队槽 ${key}`);
      slots.set(key, slot);
      known(slot.charaset_id, charas, 1387, "CharaSet", true);
      integer(slot.equipaiset_id, -0x80000000, 357, "equipaiset_id");
      integer(slot.flags ?? 0, 0, 0xffffffff, "flags");
      if (slot.use_duplicate != null && typeof slot.use_duplicate !== "boolean") throw new Error("use_duplicate 必须为布尔值");
      if (slot.equipaiset_id > 0) reserved.add(presetId(slot.equipaiset_id));
    }
  }
  if (edits.equipaiset_lines != null && (typeof edits.equipaiset_lines !== "object" || Array.isArray(edits.equipaiset_lines))) throw new Error("无效的 equipaiset_lines");
  for (const [key, rows] of Object.entries(edits.equipaiset_lines ?? {})) {
    if (!/^[1-9]\d*$/.test(key)) throw new Error(`无效预设键 ${key}`);
    reserved.add(presetId(Number(key), false));
    validateLines(rows);
  }
  const requests = [...(edits.equipaiset_creates ?? []).map(row => ({ row, create: true })), ...(edits.equipaiset_allocations ?? []).map(row => ({ row, create: false }))];
  const explicit = new Set<string | number>();
  for (const { row } of requests) if (row.new_id != null) {
    const id = presetId(row.new_id, false);
    if (reserved.has(id)) throw new Error(`预设 ${id} 已使用或被编辑引用，不能分配`);
    unique(explicit, id, "预设分配 ID");
  }
  const aliases = new Map<string, number>();
  const privateSlots = new Map<string, number>();
  const alias = (key: string, id: number) => {
    if (aliases.has(key)) throw new Error(`重复预设分配键 ${key}`);
    aliases.set(key, id);
  };
  for (const { row, create } of requests) {
    const source = presetId(row.source_id ?? ("from_id" in row ? row.from_id : undefined) ?? 0);
    if (row.key != null && (typeof row.key !== "string" || !row.key.trim())) throw new Error("无效预设分配键");
    if (create && !row.key) throw new Error("新预设必须有 key");
    if ("symbol" in row && row.symbol != null && typeof row.symbol !== "string") throw new Error("预设名称必须为文本");
    validateLines(row.lines);
    const id = row.new_id ?? [...presets.keys()].sort((left, right) => right - left).find(candidate => candidate > 0 && !reserved.has(candidate) && !explicit.has(candidate));
    if (id == null) throw new Error("没有空闲的 EquipAiSet，已阻止导出");
    row.new_id = id;
    reserved.add(id);
    if (row.key) alias(row.key, id);
    if (create && "temp_id" in row && row.temp_id != null) alias(String(integer(row.temp_id, -0x80000000, -1, "temp_id")), id);
    if (!create) {
      const allocation = row as NonNullable<ExportEdits["equipaiset_allocations"]>[number];
      if (allocation.unitset_id != null || allocation.slot != null) {
        integer(allocation.unitset_id, 0, 2099, "unitset_id");
        integer(allocation.slot, 0, 5, "slot");
        const key = `${allocation.unitset_id}:${allocation.slot}`;
        if (!slots.has(key) || privateSlots.has(key)) throw new Error("私有预设必须绑定唯一的已编辑编队槽");
        privateSlots.set(key, id);
      } else if (!row.key) {
        if (!source) throw new Error("私有预设缺少绑定目标");
        alias(String(source), id);
      }
    }
  }
  const bound = new Set<number>();
  for (const [position, slot] of slots) {
    const key = slot.equipaiset_alloc_key;
    if (key != null && (typeof key !== "string" || !aliases.has(key))) throw new Error(`未解析的预设分配键 ${key}`);
    const id = key != null ? aliases.get(key)! : privateSlots.get(position) ?? aliases.get(String(slot.equipaiset_id)) ?? slot.equipaiset_id;
    if (privateSlots.has(position) && privateSlots.get(position) !== id) throw new Error("私有预设绑定冲突");
    if (id < 0) throw new Error(`未解析的临时预设 ${id}`);
    presetId(id);
    bound.add(id);
  }
  for (const { row, create } of requests) if (!create && !bound.has(row.new_id!)) throw new Error("私有预设未绑定任何编队槽");
  const seenCharas = new Set<string | number>();
  for (const row of edits.charasets ?? []) {
    const id = known(row.charaset_id, charas, 1387, "CharaSet");
    if (id < 2) throw new Error("禁止修改保留 CharaSet 0/1");
    unique(seenCharas, id, "CharaSet 编辑");
    if (row.gear != null && (!Array.isArray(row.gear) || row.gear.length > 4)) throw new Error("装备最多四槽");
    if (row.duplicate_if_shared != null && typeof row.duplicate_if_shared !== "boolean") throw new Error("duplicate_if_shared 必须为布尔值");
    for (const gear of row.gear ?? []) {
      if (!gear || typeof gear !== "object") throw new Error("无效装备");
      known(gear.item_id ?? 0, items, 65535, "item_id", true);
      known(gear.rom_item_id ?? 0, items, 65535, "rom_item_id", true);
      if (gear.edited != null && typeof gear.edited !== "boolean") throw new Error("edited 必须为布尔值");
    }
    const usage = catalog.charaset_usage?.[String(id)] ?? charas.get(id)?.usage;
    const shared = usage == null || Math.max(usage, users.get(id)?.size ?? 0) > 1;
    if (!allowUnconfirmedShared && shared && row.gear?.some(gear => gear.edited) && row.duplicate_if_shared !== false) throw new Error(`CharaSet ${id} 被共享或引用未知。无法自动克隆；必须显式确认全局装备影响`);
  }
  const seenClasses = new Set<string | number>();
  for (const row of edits.class_tactics ?? []) {
    unique(seenClasses, known(row.class_id, classes, 73, "class_id"), "职业编辑");
    validateLines(row.lines, true);
  }
  const seenEquip = new Set<string | number>();
  for (const row of edits.equiptype_items ?? []) {
    unique(seenEquip, known(row.equiptype_id ?? row.id, equiptypes, 55, "equiptype_id"), "默认装备编辑");
    for (const value of [row.item_col0_id, row.item_col1_id, row.item_col2_id]) known(value ?? 0, items, 65535, "默认装备 item_id", true);
  }
  return edits;
}
