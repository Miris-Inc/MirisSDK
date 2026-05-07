// Browser-compatible stub for @fiftyone/utilities/src/paths
// Used by looker-3d/fo3d/utils.ts to join asset paths

export enum PathType {
  URL = 0,
  LINUX = 1,
  WINDOWS = 2,
}

export function determinePathType(path: string): PathType {
  if (/^\w+:\/\//.test(path)) return PathType.URL;
  if (path?.includes("\\")) return PathType.WINDOWS;
  return PathType.LINUX;
}

export function joinPaths(...parts: string[]): string {
  if (!parts.length) return "";
  const type = determinePathType(parts[0]);
  if (type === PathType.URL) {
    const base = parts[0].replace(/\/?$/, "");
    const rest = parts
      .slice(1)
      .map((p) => p.replace(/^\//, ""))
      .join("/");
    return rest ? `${base}/${rest}` : base;
  }
  if (type === PathType.WINDOWS) {
    return parts.join("\\").replace(/\\+/g, "\\");
  }
  return parts.join("/").replace(/\/+/g, "/");
}

export function getSeparator(pathType: PathType): string {
  return pathType === PathType.WINDOWS ? "\\" : "/";
}

export function getBasename(path: string): string | null {
  const sep = path.includes("\\") ? "\\" : "/";
  const parts = path.split(sep);
  return parts[parts.length - 1] || null;
}
