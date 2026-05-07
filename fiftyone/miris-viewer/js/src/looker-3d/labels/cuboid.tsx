import * as fos from "@fiftyone/state";
import { extend } from "@react-three/fiber";
import chroma from "chroma-js";
import { useEffect, useMemo } from "react";
import { useRecoilValue, useSetRecoilState } from "recoil";
import * as THREE from "three";
import { LineMaterial } from "three/examples/jsm/lines/LineMaterial";
import { LineSegments2 } from "three/examples/jsm/lines/LineSegments2";
import { LineSegmentsGeometry } from "three/examples/jsm/lines/LineSegmentsGeometry";
import { FO_USER_DATA } from "../constants";
import { hoveredLabelAtom } from "../state";
import type { OverlayProps } from "./shared";
import { useEventHandlers, useHoverState, useLabelColor } from "./shared/hooks";

extend({ LineSegments2, LineMaterial, LineSegmentsGeometry });

export interface CuboidProps extends OverlayProps {
  location: THREE.Vector3Tuple;
  dimensions: THREE.Vector3Tuple;
  itemRotation: THREE.Vector3Tuple;
  lineWidth?: number;
}

export const Cuboid = ({
  itemRotation,
  dimensions,
  opacity,
  rotation,
  location,
  lineWidth,
  selected,
  onClick,
  label,
  color,
  useLegacyCoordinates,
}: CuboidProps) => {
  useHoverState();
  const hoveredLabel = useRecoilValue(hoveredLabelAtom);
  const setHoveredLabel = useSetRecoilState(hoveredLabelAtom);
  const isHovered = hoveredLabel?.id === label._id;

  const { onPointerOver, onPointerOut, ...restEventHandlers } =
    useEventHandlers(label);

  const { strokeAndFillColor, isSimilarLabelHovered } = useLabelColor(
    { selected, color },
    isHovered,
    label
  );

  // In legacy coordinate system, location is top-center; adjust to geometric center.
  const displayPosition = useMemo<THREE.Vector3Tuple>(() => {
    const [x, y, z] = location;
    return useLegacyCoordinates
      ? [x, y - 0.5 * (dimensions?.[1] ?? 0), z]
      : [x, y, z];
  }, [location, dimensions, useLegacyCoordinates]);

  const renderBoxGeometry = useMemo(
    () => dimensions && new THREE.BoxGeometry(...dimensions),
    [dimensions]
  );

  const renderEdgesGeometry = useMemo(
    () => new THREE.EdgesGeometry(renderBoxGeometry),
    [renderBoxGeometry]
  );

  const lineSegmentsGeometry = useMemo(
    () =>
      new LineSegmentsGeometry().fromLineSegments(
        new THREE.LineSegments(renderEdgesGeometry)
      ),
    [renderEdgesGeometry]
  );

  const complementaryColor = useMemo(
    () => chroma(strokeAndFillColor).set("hsl.h", "+180").hex(),
    [strokeAndFillColor]
  );

  const material = useMemo(
    () =>
      new LineMaterial({
        opacity,
        transparent: opacity < 0.2,
        color: strokeAndFillColor,
        linewidth: lineWidth,
      }),
    [selected, lineWidth, opacity, isHovered, isSimilarLabelHovered, strokeAndFillColor]
  );

  useEffect(() => {
    return () => {
      renderBoxGeometry?.dispose();
      renderEdgesGeometry?.dispose();
      lineSegmentsGeometry?.dispose();
      material?.dispose();
    };
  }, [renderBoxGeometry, renderEdgesGeometry, lineSegmentsGeometry, material]);

  if (!location || !dimensions) return null;

  return (
    <group
      rotation={itemRotation}
      position={displayPosition}
      userData={{ [FO_USER_DATA.LABEL_ID]: label._id }}
    >
      {/* Outline */}
      {/* @ts-ignore */}
      <lineSegments2 geometry={lineSegmentsGeometry} material={material} />

      {/* Clickable volume */}
      <group
        onClick={onClick}
        onPointerOver={() => {
          setHoveredLabel({ id: label._id });
          onPointerOver();
        }}
        onPointerOut={() => {
          setHoveredLabel(null);
          onPointerOut();
        }}
        {...restEventHandlers}
      >
        <mesh>
          <boxGeometry args={dimensions} />
          <meshBasicMaterial
            transparent={true}
            opacity={0}
            depthWrite={false}
            color={strokeAndFillColor}
          />
        </mesh>
      </group>
    </group>
  );
};
