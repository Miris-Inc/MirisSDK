import { Miris, MirisStream as MirisStreamSDK } from "@miris-inc/three";
import { useThree } from "@react-three/fiber";
import { useEffect, useState } from "react";
import type { Quaternion, Vector3 } from "three";
import { useMirisLiveLabels } from "../MirisLiveLabelsContext";
import type { MirisStreamAsset } from "../render-types";
import type { OverlayLabel } from "../../types";

interface MirisStreamProps {
  name: string;
  asset: MirisStreamAsset;
  position: Vector3;
  quaternion: Quaternion;
  scale: Vector3;
  children?: React.ReactNode;
}

export const MirisStream = ({
  name,
  asset,
  position,
  quaternion,
  scale,
}: MirisStreamProps) => {
  const { scene } = useThree();
  const [stream, setStream] = useState<MirisStreamSDK | null>(null);
  const { setLiveDetections } = useMirisLiveLabels();

  useEffect(() => {
    let cancelled = false;
    const rawBounds = new Map<string, number[]>();
    let mirisRef: InstanceType<typeof Miris> | null = null;
    let streamRef: MirisStreamSDK | null = null;

    const emitOverlayLabels = () => {
      const overlays: OverlayLabel[] = [];
      for (const [id, b] of rawBounds.entries()) {
        overlays.push({
          _id: id,
          _cls: "Detection",
          path: "bounding_box",
          selected: false,
          location: [b[0], b[1], b[2]] as [number, number, number],
          dimensions: [b[3], b[4], b[5]] as [number, number, number],
          rotation: [0, 0, 0] as [number, number, number],
        });
      }
      setLiveDetections(overlays);
    };

    (async () => {
      try {
        const s = new MirisStreamSDK({
          uuid: asset.streamUuid,
          viewerKey: asset.viewerKey,
        });
        s.name = name;
        streamRef = s;
        s.addEventListener("streamloaded", () => {
          // eslint-disable-next-line no-console
          console.log("[miris]", name, "streamloaded");

          const bounds = s.getBounds();
          rawBounds.set("0", [...bounds.center, ...bounds.size]);
          emitOverlayLabels();
        });
        s.addEventListener("rootloaded", () => {
          // eslint-disable-next-line no-console
          console.log("[miris]", name, "rootloaded");
        });
        (scene as any).add(s);

        const miris = await (Miris as any).instance();
        mirisRef = miris;

        if (cancelled) return;

        setStream(s);
      } catch (err) {
        console.error("[MirisStream] async init threw:", err);
      }
    })();

    return () => {
      cancelled = true;
      rawBounds.clear();
      setLiveDetections([]);
      if (streamRef) {
        (scene as any).remove(streamRef);
      }
      setStream(null);
    };
  }, [scene, asset.streamUuid, asset.viewerKey, name, setLiveDetections]);

  useEffect(() => {
    if (!stream) return;
    stream.position.copy(position);
    stream.quaternion.copy(quaternion);
    stream.scale.copy(scale);
  }, [stream, position, quaternion, scale]);

  return null;
};
