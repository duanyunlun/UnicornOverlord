import { buildMissionMod, type ExportEdits, type ExportCatalog } from "./exportMissionMod.ts";
import { validateEdits } from "./exportValidation.ts";
import { isTarget, type Target } from "./frameApi.ts";

export function prepareMissionPatch(edits: ExportEdits, catalog: ExportCatalog, target: Target, confirmShared: () => boolean, modName = "mission_project") {
  if (!isTarget(target)) throw new Error("无效的游戏目标");
  validateEdits(edits, catalog, true);
  const payload = structuredClone(edits);
  payload.charasets = (payload.charasets ?? []).filter(row => row.gear?.some(gear => gear.edited));
  const changed = [payload.unitsets, payload.charasets, payload.class_tactics, payload.equiptype_items, payload.equipaiset_creates, payload.equipaiset_allocations].some(rows => rows?.length) || Object.keys(payload.equipaiset_lines ?? {}).length > 0;
  if (!changed) return { content: null, edits: payload, files: [], patchCount: 0 };
  if (payload.charasets.length) {
    if (!confirmShared()) throw new Error("已取消导出：未确认共享装备的全局影响");
    for (const row of payload.charasets) row.duplicate_if_shared = false;
  }
  const built = buildMissionMod(payload, catalog, modName, target);
  return { ...built, content: built.files[0].text, edits: payload };
}
