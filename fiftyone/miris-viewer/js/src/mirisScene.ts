import { MirisScene, Miris } from "@miris-inc/three";

export type { MirisScene };

let _scenePromise: Promise<MirisScene> | null = null;

export function initMirisScene(
  mirisPromise: Promise<Miris>,
  viewerKey?: string | null,
): void {
  _scenePromise = mirisPromise.then(
    () => new MirisScene({ viewerKey }),
  );
}

export async function getMirisScene(): Promise<MirisScene> {
  if (!_scenePromise)
    throw new Error("MirisScene not initialized. Call initMirisScene first.");
  return _scenePromise;
}
