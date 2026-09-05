/** Overlay .pchtxt patches onto catalog class/item tables (no dumped main). */

import type { ClassLine, ItemSkill } from "./tacticsResolve.ts";
import { TARGETS, type Target } from "./frameApi.ts";

const CLASS_SKILL_BASE = 0xd36d94;
const CLASS_SKILL_STRIDE = 0x8c;
const CLASS_ACTIVE_LEVELS = [0x20, 0x28, 0x30, 0x38];
const CLASS_ACTIVES = [0x24, 0x2c, 0x34, 0x3c];
const CLASS_PASSIVE_LEVELS = [0x50, 0x58, 0x60, 0x68];
const CLASS_PASSIVES = [0x54, 0x5c, 0x64, 0x6c];
const EQUIPAISET_BASE = 0x2787f28;
const EQUIPAISET_STRIDE = 0x130;
const SKILL_DEFAULT_IF0_OFF = 0xac;
const SKILL_DEFAULT_IF1_OFF = 0xb0;
const ITEM_BASE = 0x2716168;
const ITEM_STRIDE = 0xb8;
const ITEM_SKILL_OFF = 0x28;
const N_IFS = 203;

export type CatalogClass = {
  class_id: number;
  class_symbol: string;
  lines: ClassLine[];
};

function parsePchtxtBytes(text: string): { bytes: Map<number, number>; count: number } {
  const bytes = new Map<number, number>();
  let count = 0;
  let enabled = false;
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.split("//", 1)[0].trim();
    if (line === "@enabled") { enabled = true; continue; }
    if (line === "@disabled" || line === "@stop") { enabled = false; continue; }
    if (!line || line.startsWith("@")) continue;
    if (!enabled) continue;
    const parts = line.split(/\s+/);
    if (parts.length !== 2 || !/^[0-9a-f]{1,8}$/i.test(parts[0]) || !/^(?:[0-9a-f]{2})+$/i.test(parts[1])) throw new Error("无效的 pchtxt 补丁行");
    const addr = Number.parseInt(parts[0], 16);
    const hex = parts[1].replace(/\s/g, "");
    if (!Number.isFinite(addr) || hex.length % 2 !== 0) continue;
    const data: number[] = [];
    let bad = false;
    for (let i = 0; i < hex.length; i += 2) {
      const b = Number.parseInt(hex.slice(i, i + 2), 16);
      if (Number.isNaN(b)) {
        bad = true;
        break;
      }
      data.push(b);
    }
    if (bad || !data.length) continue;
    data.forEach((b, i) => bytes.set(addr + i, b));
    count += 1;
  }
  return { bytes, count };
}

function u32(bytes: Map<number, number>, addr: number): number | undefined {
  const b0 = bytes.get(addr);
  const b1 = bytes.get(addr + 1);
  const b2 = bytes.get(addr + 2);
  const b3 = bytes.get(addr + 3);
  if (b0 == null || b1 == null || b2 == null || b3 == null) return undefined;
  return (b0 | (b1 << 8) | (b2 << 16) | (b3 << 24)) >>> 0;
}

export function overlayPchtxtOnCatalog(
  files: { name: string; text: string }[],
  classTactics: CatalogClass[],
  skills: Map<number, { name?: string; symbol?: string }>,
  ifs: Map<number, string>,
  baselineItems: Map<number, ItemSkill> = new Map(),
  defaultConditions: Record<string, { if0: number; if1: number }> = {},
  target: Target = "asia"
): {
  class_tactics: CatalogClass[];
  item_skills: Map<number, ItemSkill>;
  patches_applied: number;
} {
  const bytes = new Map<number, number>();
  let patchesApplied = 0;
  for (const file of files) {
    const build = file.text.match(/@nsobid-([0-9a-f]+)/i)?.[1].toUpperCase();
    if (build !== TARGETS[target].buildId) throw new Error(`${file.name} 的 BuildID 缺失或与当前目标不符`);
    if (!/@flag\s+offset_shift\s+0x100\b/i.test(file.text)) throw new Error(`${file.name} 只支持 offset_shift 0x100`);
    const parsed = parsePchtxtBytes(file.text);
    parsed.bytes.forEach((b, addr) => bytes.set(addr, b));
    patchesApplied += parsed.count;
  }

  const skillIf = (sid: number): { if0?: number; if1?: number } => {
    const row = EQUIPAISET_BASE + sid * EQUIPAISET_STRIDE;
    return {
      if0: u32(bytes, row + SKILL_DEFAULT_IF0_OFF) ?? defaultConditions[String(sid)]?.if0,
      if1: u32(bytes, row + SKILL_DEFAULT_IF1_OFF) ?? defaultConditions[String(sid)]?.if1,
    };
  };

  const decorate = (line: ClassLine): ClassLine => {
    const sid = Number(line.skill_id || 0);
    const patched = sid > 0 ? skillIf(sid) : { if0: 0, if1: 0 };
    const if0 = patched.if0 ?? line.if0 ?? 0;
    const if1 = patched.if1 ?? line.if1 ?? 0;
    const meta = skills.get(sid);
    return {
      ...line,
      if0: if0 < N_IFS ? if0 : 0,
      if1: if1 < N_IFS ? if1 : 0,
      if0_symbol: if0 ? ifs.get(if0) || line.if0_symbol : undefined,
      if1_symbol: if1 ? ifs.get(if1) || line.if1_symbol : undefined,
      skill_name: meta?.name || line.skill_name,
      skill_symbol: meta?.symbol || line.skill_symbol,
    };
  };

  const classOut: CatalogClass[] = classTactics.map((entry) => {
    const base = CLASS_SKILL_BASE + entry.class_id * CLASS_SKILL_STRIDE;
    const byAction = new Map<number, ClassLine>();
    for (const line of entry.lines) byAction.set(Number(line.action || 0), { ...line });

    const applySlot = (
      action: number,
      levelOff: number,
      skillOff: number
    ) => {
      const skillId = u32(bytes, base + skillOff);
      const learnLevel = u32(bytes, base + levelOff);
      if (skillId == null && learnLevel == null) return;
      const prev = byAction.get(action) || {
        action,
        skill_id: 0,
        if0: 0,
        if1: 0,
        learn_level: 1,
      };
      byAction.set(action, {
        ...prev,
        action,
        skill_id: skillId ?? prev.skill_id,
        learn_level: learnLevel ?? prev.learn_level ?? 1,
      });
    };

    for (let i = 0; i < 4; i++) {
      applySlot(3 + i, CLASS_ACTIVE_LEVELS[i], CLASS_ACTIVES[i]);
      applySlot(7 + i, CLASS_PASSIVE_LEVELS[i], CLASS_PASSIVES[i]);
    }

    const lines = [...byAction.values()]
      .sort((a, b) => Number(a.action || 0) - Number(b.action || 0))
      .filter(line => line.skill_id)
      .map(decorate);
    return { ...entry, lines };
  });

  const itemSkills = new Map<number, ItemSkill>([...baselineItems].map(([id, item]) => {
    const conditions = skillIf(item.skill_id);
    return [id, { ...item, if0: conditions.if0 ?? item.if0, if1: conditions.if1 ?? item.if1 }];
  }));
  const itemSkillAddr = ITEM_BASE + ITEM_SKILL_OFF;
  for (const addr of bytes.keys()) {
    const delta = addr - itemSkillAddr;
    if (delta < 0 || delta % ITEM_STRIDE !== 0) continue;
    const iid = delta / ITEM_STRIDE;
    const sid = u32(bytes, addr);
    if (sid == null) continue;
    if (sid === 0) { itemSkills.delete(iid); continue; }
    const meta = skills.get(sid);
    const sifs = skillIf(sid);
    itemSkills.set(iid, {
      skill_id: sid,
      skill_symbol: meta?.symbol,
      skill_name: meta?.name,
      if0: (sifs.if0 ?? 0) < N_IFS ? sifs.if0 ?? 0 : 0,
      if1: (sifs.if1 ?? 0) < N_IFS ? sifs.if1 ?? 0 : 0,
      if0_symbol: sifs.if0 ? ifs.get(sifs.if0) : undefined,
      if1_symbol: sifs.if1 ? ifs.get(sifs.if1) : undefined,
    });
  }

  return {
    class_tactics: classOut,
    item_skills: itemSkills,
    patches_applied: patchesApplied,
  };
}
