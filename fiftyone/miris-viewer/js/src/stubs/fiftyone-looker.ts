// Stubs for @fiftyone/looker (and ALL sub-paths) used in looker-3d

// ── Types ────────────────────────────────────────────────────────────────────

export type DetectionLabel = {
  _id: string;
  id?: string;
  label?: string;
  bounding_box?: number[];
  [key: string]: any;
};

export type PolylineLabel = {
  _id: string;
  id?: string;
  label?: string;
  points?: number[][][];
  [key: string]: any;
};

export type ColorscaleInput = {
  list: Array<{ value: number; color: string }>;
};

export type Sample = Record<string, any>;
export type Coordinates = number[];

export type LabelHoveredEvent = { labelId: string; instanceId?: string };
export type LabelToggledEvent = { labelId: string; visible: boolean };

// ── Event constants ───────────────────────────────────────────────────────────

export const FO_LABEL_HOVERED_EVENT = "fo-label-hovered";
export const FO_LABEL_UNHOVERED_EVENT = "fo-label-unhovered";
export const FO_LABEL_TOGGLED_EVENT = "fo-label-toggled";

// ── Event bus stub ────────────────────────────────────────────────────────────

export const selectiveRenderingEventBus = {
  emit: (_event: any) => {},
  on: (_event: string, _handler: any) => () => {},
  off: (_event: string, _handler: any) => {},
};

// ── Overlay utils (from @fiftyone/looker/src/overlays/util) ──────────────────

// Assigns a consistent color from coloring.pool based on field path.
export const getLabelColor = ({
  coloring,
  path,
}: {
  coloring?: any;
  path?: string;
  isTagged?: boolean;
  labelTagColors?: any;
  customizeColorSetting?: any;
  label?: any;
  embeddedDocType?: string;
}): string => {
  if (!coloring?.pool?.length) return "#FF6D04";
  const pool: string[] = coloring.pool;
  let hash = 0;
  const key = path ?? "";
  for (let i = 0; i < key.length; i++) {
    hash = (hash * 31 + key.charCodeAt(i)) & 0xffffffff;
  }
  return pool[Math.abs(hash) % pool.length];
};

export const shouldShowLabelTag = (
  _selectedLabelTags: any,
  _tags: any
): boolean => false;

// ── Hooks (never called in first pass) ───────────────────────────────────────

export const useEventHandler = () => {};
export const useOverlayLabel = () => null;
export const useLabelHandler = () => {};
export const SELECTION_SCOPE = "selection";
