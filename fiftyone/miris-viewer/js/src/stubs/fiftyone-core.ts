// Stubs for @fiftyone/core and @fiftyone/components deep sub-paths.
// All sub-path imports from these packages map to this file.
// None of these components/hooks are executed in the first-pass (no fo3d sample).
import React from "react";
import { atom } from "recoil";

// ── Generic null component (serves as default export for any sub-path) ───────
const NullComponent = (_props: any): React.ReactElement | null => null;
export default NullComponent;

// ── Named UI component stubs ──────────────────────────────────────────────────
export const Checkbox = (_props: any) => null;
export const Selector = NullComponent;
export const NumberInput = (props: any) =>
  React.createElement("input", { type: "number", ...props });
export const Input = (props: any) => React.createElement("input", props);
export const RangeSlider = (_props: any) => null;
export const getRGBColorFromPool = (_index: number) => "#ffffff";

// ── Hook stubs ────────────────────────────────────────────────────────────────
export const useCanAnnotate = () => ({
  showAnnotationTab: false,
  canAnnotate: false,
});
export const useExit = () => () => {};
export const useLabels = () => ({ labels: [] });
export const coerceStringBooleans = (v: any) => v;

// ── Recoil atom stubs (annotation-only state) ─────────────────────────────────
export const editing = atom<any>({
  key: "__miris_stub_fo3d_editing__",
  default: null,
});

export const savedLabel = atom<any>({
  key: "__miris_stub_fo3d_savedLabel__",
  default: null,
});

export const labelSchemaData = atom<any>({
  key: "__miris_stub_fo3d_labelSchemaData__",
  default: {},
});

export const activeLabelSchemas = atom<any>({
  key: "__miris_stub_fo3d_activeLabelSchemas__",
  default: {},
});
