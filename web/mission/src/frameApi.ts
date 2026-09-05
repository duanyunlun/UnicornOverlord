export const TARGETS = {
  asia: { name: "亚洲中文版 v1.0.5", titleId: "010054B01AD92000", buildId: "9C3116F0333EA157526612D17354B3755737C4F2" },
  western: { name: "欧美版 v1.0.5", titleId: "010069401ADB8000", buildId: "C841FFE2717FF03A13990480C51DA73F091C04FA" },
} as const;

export type Target = keyof typeof TARGETS;
export type FrameView = "missions" | "classes" | "presets" | "gear";
export function isTarget(value: unknown): value is Target {
  return value === "asia" || value === "western";
}
export function isView(value: unknown): value is FrameView {
  return value === "missions" || value === "classes" || value === "presets" || value === "gear";
}
export function acceptsParentMessage(event: Pick<MessageEvent, "origin" | "source">, origin: string, parent: Window): boolean {
  return event.origin === origin && event.source === parent;
}
