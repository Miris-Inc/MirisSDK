// Stub for @fiftyone/state/src/jotai
// These are jotai atoms and utilities from @fiftyone/state — only used in
// annotation/label hover code paths that are unreachable in the first pass.
import { atom, createStore } from "jotai";

export const modalMode = atom<string>("default");
export const selectedLabels = atom<string[]>([]);
export const hoveredLabel = atom<any>(null);

// Store utilities used by use-similar-labels-3d
export const jotaiStore = createStore();
export const removeAllHoveredInstances = (_store?: any) => {};
export const updateHoveredInstances = (
  _store: any,
  _instanceIds: string[],
  _hovered: boolean
) => {};

// Event bus types (re-exported from @fiftyone/looker stubs via jotai path)
export const FO_LABEL_HOVERED_EVENT = "fo-label-hovered";
export const FO_LABEL_UNHOVERED_EVENT = "fo-label-unhovered";
