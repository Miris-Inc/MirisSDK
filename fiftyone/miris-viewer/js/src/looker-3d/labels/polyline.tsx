import { Line as LineDrei } from "@react-three/drei";
import { useEffect, useMemo, useRef } from "react";
import { useRecoilValue, useSetRecoilState } from "recoil";
import * as THREE from "three";
import { FO_USER_DATA } from "../constants";
import { hoveredLabelAtom } from "../state";
import { isValidPoint3d, validatePoints3d } from "../utils";
import { createFilledPolygonMeshes } from "./polygon-fill-utils";
import type { OverlayProps } from "./shared";
import { useEventHandlers, useHoverState, useLabelColor } from "./shared/hooks";

export interface PolyLineProps extends OverlayProps {
  points3d: THREE.Vector3Tuple[][];
  filled: boolean;
  lineWidth?: number;
  closed?: boolean;
}

export const Polyline = ({
  opacity,
  filled,
  rotation,
  points3d,
  color,
  selected,
  lineWidth,
  closed,
  onClick,
  label,
}: PolyLineProps) => {
  const meshesRef = useRef<THREE.Mesh[]>([]);

  useHoverState();
  const hoveredLabel = useRecoilValue(hoveredLabelAtom);
  const setHoveredLabel = useSetRecoilState(hoveredLabelAtom);
  const isHovered = hoveredLabel?.id === label._id;

  const { onPointerOver, onPointerOut, ...restEventHandlers } =
    useEventHandlers(label);

  const { strokeAndFillColor } = useLabelColor({ selected, color }, isHovered, label);

  const lines = useMemo(() => {
    const lineElements = (points3d ?? [])
      .map((pts, i) => {
        if (!pts || !Array.isArray(pts) || pts.length === 0) return null;
        const validPts = validatePoints3d(pts);
        if (validPts.length === 0) return null;

        return (
          <LineDrei
            key={`polyline-${label._id}-${i}`}
            lineWidth={lineWidth}
            points={validPts}
            color={strokeAndFillColor}
            rotation={rotation}
            transparent={opacity < 0.2}
            opacity={opacity}
          />
        );
      })
      .filter(Boolean);

    if (closed) {
      const closingLines = (points3d ?? [])
        .map((pts, i) => {
          if (!pts || pts.length < 2) return null;
          const first = pts[0];
          const last = pts[pts.length - 1];
          if (!isValidPoint3d(first) || !isValidPoint3d(last)) return null;
          return (
            <LineDrei
              key={`polyline-closing-${label._id}-${i}`}
              lineWidth={lineWidth}
              points={[last, first]}
              color={strokeAndFillColor}
              rotation={rotation}
              transparent={opacity < 0.2}
              opacity={opacity}
            />
          );
        })
        .filter(Boolean);

      return [...lineElements, ...closingLines];
    }

    return lineElements;
  }, [points3d, closed, strokeAndFillColor, lineWidth, rotation, opacity, label._id]);

  const material = useMemo(() => {
    if (!filled) return null;
    return new THREE.MeshBasicMaterial({
      color: strokeAndFillColor,
      opacity,
      transparent: true,
      side: THREE.DoubleSide,
      depthWrite: false,
    });
  }, [filled, strokeAndFillColor, opacity]);

  const filledMeshes = useMemo(() => {
    if (!filled || !material || !points3d) return null;
    const meshes = createFilledPolygonMeshes(points3d, material);
    if (!meshes) return null;
    return meshes.map((mesh, idx) => (
      <primitive
        key={`filled-${label._id}-${idx}`}
        object={mesh}
        rotation={rotation as unknown as THREE.Euler}
      />
    ));
  }, [filled, points3d, rotation, material, label._id]);

  useEffect(() => {
    return () => {
      meshesRef.current.forEach((m) => m.geometry?.dispose());
    };
  }, []);

  useEffect(() => {
    return () => {
      material?.dispose();
    };
  }, [material]);

  return (
    <group
      userData={{ [FO_USER_DATA.LABEL_ID]: label._id }}
      {...restEventHandlers}
      onPointerOver={() => {
        setHoveredLabel({ id: label._id });
        onPointerOver();
      }}
      onPointerOut={() => {
        setHoveredLabel(null);
        onPointerOut();
      }}
      onClick={onClick}
    >
      {filled && filledMeshes}
      {lines}
    </group>
  );
};
