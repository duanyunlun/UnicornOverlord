/** Client-side tactics resolution matching Scripts/build_mission_squads_json.py */

export type ResolveLine = {
  action: number;
  slot?: number;
  if0: number;
  if1: number;
  if0_symbol?: string;
  if1_symbol?: string;
  skill_id?: number;
  skill_symbol?: string;
  skill_name?: string;
  learn_level?: number;
  locked?: boolean;
  from_class_default?: boolean;
  from_item?: boolean;
  from_equipaiset_preset?: boolean;
  ref_kind?: string;
};

export type ClassLine = ResolveLine & {
  action: number;
  skill_id?: number;
  skill_name?: string;
  skill_symbol?: string;
  learn_level?: number;
};

export type ItemSkill = {
  skill_id: number;
  skill_symbol?: string;
  skill_name?: string;
  if0: number;
  if1: number;
  if0_symbol?: string;
  if1_symbol?: string;
};

export type SkillMeta = {
  id: number;
  symbol?: string;
  name?: string;
};

export type IfMeta = { id: number; symbol?: string };

const MARKER_MIN = 3;
const MARKER_MAX = 10;

export function isClassMarker(skillRef: number): boolean {
  return skillRef >= MARKER_MIN && skillRef <= MARKER_MAX;
}

function ifSym(id: number, ifs: Map<number, string>): string | undefined {
  if (!id) return undefined;
  return ifs.get(id);
}

export function tacticsForClass(
  classLines: ClassLine[],
  unitLevel: number,
  gearItemIds: number[],
  itemSkills: Map<number, ItemSkill>,
  ifs: Map<number, string>
): ResolveLine[] {
  const lvl = unitLevel > 0 ? unitLevel : 1;
  const classRows = classLines.map((line) => ({
    ...line,
    locked: (line.learn_level || 1) > lvl,
    from_class_default: true,
  }));
  const actives = classRows.filter((r) => (r.action || 0) < 7);
  const passives = classRows.filter((r) => (r.action || 0) >= 7);
  const have = new Set(
    classRows.map((r) => r.skill_id || 0).filter((id) => id > 0)
  );

  const itemActive: ResolveLine[] = [];
  const itemPassive: ResolveLine[] = [];
  for (const iid of gearItemIds) {
    const meta = itemSkills.get(iid);
    if (!meta?.skill_id || have.has(meta.skill_id)) continue;
    have.add(meta.skill_id);
    const ssym = meta.skill_symbol || "";
    const kindPassive = ssym.startsWith("PAS_");
    const entry: ResolveLine = {
      action: kindPassive ? 7 : 3,
      if0: meta.if0 || 0,
      if1: meta.if1 || 0,
      from_item: true,
      learn_level: 1,
      skill_id: meta.skill_id,
      skill_symbol: ssym,
      skill_name: meta.skill_name || ssym,
    };
    const s0 = meta.if0_symbol || ifSym(entry.if0, ifs);
    const s1 = meta.if1_symbol || ifSym(entry.if1, ifs);
    if (s0) entry.if0_symbol = s0;
    if (s1) entry.if1_symbol = s1;
    (kindPassive ? itemPassive : itemActive).push(entry);
  }
  return [...actives, ...itemActive, ...itemPassive, ...passives];
}

export function tacticsForPreset(
  classLines: ClassLine[],
  unitLevel: number,
  presetLines: ResolveLine[],
  skills: Map<number, SkillMeta>,
  ifs: Map<number, string>
): ResolveLine[] {
  const lvl = unitLevel > 0 ? unitLevel : 1;
  const byAction = new Map<number, ClassLine>();
  for (const line of classLines) {
    byAction.set(line.action || 0, line);
  }

  const out: ResolveLine[] = [];
  for (const ln of presetLines) {
    const sid = ln.skill_id || 0;
    const refKind =
      ln.ref_kind || (isClassMarker(sid) ? "class_slot" : sid ? "skill" : "");
    const if0 = ln.if0 || 0;
    const if1 = ln.if1 || 0;
    const entry: ResolveLine = {
      action: 0,
      slot: ln.slot ?? 0,
      if0,
      if1,
      from_equipaiset_preset: true,
      ref_kind: refKind,
    };
    const s0 = ln.if0_symbol || ifSym(if0, ifs);
    const s1 = ln.if1_symbol || ifSym(if1, ifs);
    if (s0) entry.if0_symbol = s0;
    if (s1) entry.if1_symbol = s1;

    if (refKind === "class_slot" || isClassMarker(sid)) {
      const base = byAction.get(sid);
      if (!base) continue;
      entry.action = base.action || sid;
      entry.skill_id = base.skill_id || 0;
      entry.skill_symbol = base.skill_symbol || "";
      entry.skill_name = base.skill_name || "";
      entry.learn_level = base.learn_level || 1;
      entry.locked = (base.learn_level || 1) > lvl;
      entry.from_class_default = true;
    } else if (sid) {
      const meta = skills.get(sid);
      entry.action = ln.action || 3;
      entry.skill_id = sid;
      entry.skill_symbol = ln.skill_symbol || meta?.symbol || "";
      entry.skill_name = ln.skill_name || meta?.name || entry.skill_symbol;
      entry.learn_level = 1;
      entry.locked = false;
      entry.from_item = true;
    } else {
      continue;
    }
    out.push(entry);
  }
  return out;
}

export function resolveMarkerHint(
  skillRef: number,
  classLines: ClassLine[]
): { skill_id: number; skill_name: string; skill_symbol: string } | null {
  if (!isClassMarker(skillRef)) return null;
  const base = classLines.find((l) => (l.action || 0) === skillRef);
  if (!base?.skill_id) return null;
  return {
    skill_id: base.skill_id,
    skill_name: base.skill_name || "",
    skill_symbol: base.skill_symbol || "",
  };
}
