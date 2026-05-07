// Stubs for @fiftyone/utilities used in looker-3d

export const is3d = (mediaType?: string | null) =>
  mediaType === "point_cloud" || mediaType === "three_d";

export const isDirect3dSamplePath = (path?: string | null) => {
  if (!path) return false;
  return [".pcd", ".ply", ".stl", ".obj", ".glb", ".gltf", ".fbx"].some(
    (ext) => path.toLowerCase().endsWith(ext)
  );
};

export const setContains3d = (set?: Set<string> | null) => {
  if (!set) return false;
  return [...set].some((t) => is3d(t));
};

export const isFo3dSamplePath = (path?: string | null) => {
  if (!path) return false;
  return path.endsWith(".fo3d");
};

export const DETECTION = "Detection";
export const POLYLINE = "Polyline";
export const objectId = () => Math.random().toString(36).slice(2);

export enum PathType {
  URL = 0,
  LINUX = 1,
  WINDOWS = 2,
}

export const determinePathType = (path: string): PathType => {
  if (/^\w+:\/\//.test(path)) return PathType.URL;
  if (path?.includes("\\")) return PathType.WINDOWS;
  return PathType.LINUX;
};

export const getFetchFunction =
  (_opts?: any) => async (path: string, method?: string, body?: any) => {
    const response = await fetch(path, {
      method: method ?? "GET",
      body: body ? JSON.stringify(body) : undefined,
    });
    return response.json();
  };

export const interpolateColorsHex = (_colors: any[], _value: number) =>
  "#ffffff";
export const rgbStringToHex = (_rgb: string) => "#ffffff";
export const coerceStringBooleans = (value: any) => value;

export const FLOAT_FIELD = "FloatField";
export const INT_FIELD = "IntField";

// Label type constants
export const DETECTIONS = "Detections";
export const POLYLINES = "Polylines";

// Sample path utilities
export const isWrappableDirect3dSamplePath = (path?: string | null) => {
  if (!path) return false;
  return isDirect3dSamplePath(path);
};
export const getSamplePathExtension = (path?: string | null) => {
  if (!path) return "";
  const dot = path.lastIndexOf(".");
  return dot >= 0 ? path.slice(dot) : "";
};

// Label list mapping: list-type class → key that holds the items array on the object
export const LABEL_LIST: Record<string, string> = {
  Classifications: "classifications",
  Detections: "detections",
  Keypoints: "keypoints",
  Polylines: "polylines",
  TemporalDetections: "detections",
};

// TypeScript type matching FiftyOne's field schema shape
export type Schema = Record<
  string,
  {
    ftype?: string;
    embeddedDocType?: string;
    name?: string;
    path?: string;
    [key: string]: any;
  }
>;

// Returns the short class name for a field path given the sample schema.
// e.g. "bowls" → "Detections", "bowls.detections" → "Detection"
export const getCls = (path: string, schema: Schema): string | null => {
  if (!schema || !path) return null;
  const field = schema[path];
  if (!field) return null;
  // FiftyOne stores types as dotted paths like "fiftyone.core.labels.Detections"
  if (field.embeddedDocType) {
    return field.embeddedDocType.split(".").at(-1) ?? null;
  }
  if (field.ftype) {
    return field.ftype.split(".").at(-1) ?? null;
  }
  return null;
};
