import {
  FO_LABEL_TOGGLED_EVENT,
  getLabelColor,
  shouldShowLabelTag,
} from "@fiftyone/looker";
import * as fop from "@fiftyone/plugins";
import * as fos from "@fiftyone/state";
import { fieldSchema } from "@fiftyone/state";
import { useOnShiftClickLabel } from "@fiftyone/state/src/hooks/useOnShiftClickLabel";
import { ThreeEvent } from "@react-three/fiber";
import { folder, useControls } from "leva";
import { get as _get } from "lodash";
import { useCallback, useEffect, useMemo } from "react";
import { useRecoilState, useRecoilValue } from "recoil";
import {
  ANNOTATION_CUBOID,
  ANNOTATION_POLYLINE,
  DRAG_GATE_THRESHOLD_PX,
  PANEL_ORDER_LABELS,
} from "../constants";
import { usePathFilter } from "../hooks";
import { type Looker3dSettings, defaultPluginSettings } from "../settings";
import {
  cuboidLabelLineWidthAtom,
  polylineLabelLineWidthAtom,
} from "../state";
import {
  Archetype3d,
  isDetection3dOverlay,
  isPolyline3dOverlay,
  type OverlayLabel,
} from "../types";
import { toEulerFromDegreesArray } from "../utils";
import { Cuboid, type CuboidProps } from "./cuboid";
import { DragGate3D } from "./DragGate3D";
import { load3dOverlays } from "./loader";
import { type PolyLineProps, Polyline } from "./polyline";

export interface ThreeDLabelsProps {
  sampleMap: { [sliceOrFilename: string]: fos.ModalSample } | fos.Sample[];
  globalOpacity?: number;
  isMainPanel?: boolean;
  /** Additional labels injected from outside FiftyOne's dataset (e.g. Miris stream) */
  extraLabels?: OverlayLabel[];
}

export const ThreeDLabels = ({
  sampleMap,
  globalOpacity,
  isMainPanel = true,
  extraLabels,
}: ThreeDLabelsProps) => {
  const schema = useRecoilValue(fieldSchema({ space: fos.State.SPACE.SAMPLE }));
  const { coloring, selectedLabelTags, customizeColorSetting, labelTagColors } =
    useRecoilValue(fos.lookerOptions({ withFilter: true, modal: true }));

  const settings = fop.usePluginSettings<Looker3dSettings>(
    "3d",
    defaultPluginSettings
  );
  const onSelectLabel = fos.useOnSelectLabel();
  const pathFilter = usePathFilter();
  const colorScheme = useRecoilValue(fos.colorScheme);
  const [cuboidLineWidth, setCuboidLineWidth] = useRecoilState(
    cuboidLabelLineWidthAtom
  );
  const [polylineWidth, setPolylineWidth] = useRecoilState(
    polylineLabelLineWidthAtom
  );
  const selectedLabels = useRecoilValue(fos.selectedLabelMap);
  const labelAlpha = globalOpacity ?? colorScheme.opacity;

  useControls(
    () => ({
      Labels: folder(
        {
          cuboidLineWidget: {
            value: cuboidLineWidth,
            min: 0,
            max: 20,
            step: 1,
            label: "Cuboid Line Width",
            onChange: (value: number) => setCuboidLineWidth(value),
          },
          polylineLineWidget: {
            value: polylineWidth,
            min: 0,
            max: 20,
            step: 1,
            label: "Polyline Line Width",
            onChange: (value: number) => setPolylineWidth(value),
          },
        },
        { order: PANEL_ORDER_LABELS, collapsed: true }
      ),
    }),
    [setCuboidLineWidth, setPolylineWidth]
  );

  const handleSelect = useCallback(
    (label: OverlayLabel, _archetype: Archetype3d, e: ThreeEvent<MouseEvent>) => {
      onSelectLabel({
        detail: {
          id: label._id,
          field: label.path,
          sampleId: label.sampleId,
          instanceId: label.instance?._id,
          isShiftPressed: e.shiftKey,
        },
      });
    },
    [onSelectLabel]
  );

  const [overlayRotation, itemRotation] = useMemo(
    () => [
      toEulerFromDegreesArray(_get(settings, "overlay.rotation", [0, 0, 0])),
      toEulerFromDegreesArray(_get(settings, "overlay.itemRotation", [0, 0, 0])),
    ],
    [settings]
  );

  // Load overlays from FiftyOne sample data
  const foOverlays = useMemo(
    () =>
      (load3dOverlays(sampleMap, selectedLabels, [], schema) ?? [])
        .map((l) => {
          const isTagged = shouldShowLabelTag(selectedLabelTags, l.tags);
          const color = getLabelColor({
            coloring,
            path: l.path,
            isTagged,
            labelTagColors,
            customizeColorSetting,
            label: l,
            embeddedDocType: l._cls,
          });
          return { ...l, color, id: l._id };
        })
        .filter((l) => pathFilter(l.path, l)),
    [coloring, pathFilter, sampleMap, selectedLabels, schema, selectedLabelTags, labelTagColors, customizeColorSetting]
  );

  // Merge FiftyOne overlays with any extra labels supplied from outside
  const allOverlays = useMemo(() => {
    if (!extraLabels?.length) return foOverlays;
    const colored = extraLabels
      .map((l) => ({
        ...l,
        id: l._id,
        color: l.color ?? getLabelColor({ coloring, path: l.path }),
      }))
      .filter((l) => pathFilter(l.path, l));
    return [...foOverlays, ...colored];
  }, [foOverlays, extraLabels, coloring, pathFilter]);

  const detections = useMemo(
    () => allOverlays.filter(isDetection3dOverlay),
    [allOverlays]
  );

  const polylines = useMemo(
    () => allOverlays.filter(isPolyline3dOverlay),
    [allOverlays]
  );

  const cuboidOverlays = useMemo(
    () =>
      detections.map((overlay) => (
        <DragGate3D
          key={`cuboid-${overlay._id}-${overlay.sampleId}`}
          dragThresholdPx={DRAG_GATE_THRESHOLD_PX}
          onClick={(e) => handleSelect(overlay, ANNOTATION_CUBOID, e)}
        >
          <Cuboid
            lineWidth={cuboidLineWidth}
            rotation={overlayRotation}
            itemRotation={overlay.rotation ?? itemRotation}
            opacity={labelAlpha}
            {...(overlay as unknown as CuboidProps)}
            label={overlay}
            useLegacyCoordinates={settings.useLegacyCoordinates}
            color={overlay.color}
          />
        </DragGate3D>
      )),
    [detections, cuboidLineWidth, overlayRotation, itemRotation, labelAlpha, handleSelect, settings]
  );

  const polylineOverlays = useMemo(
    () =>
      polylines.map((overlay) => (
        <DragGate3D
          key={`polyline-${overlay._id}-${overlay.sampleId}`}
          dragThresholdPx={DRAG_GATE_THRESHOLD_PX}
          onClick={(e) => handleSelect(overlay, ANNOTATION_POLYLINE, e)}
        >
          <Polyline
            rotation={overlayRotation}
            opacity={labelAlpha}
            lineWidth={polylineWidth}
            {...(overlay as unknown as PolyLineProps)}
            label={overlay}
            color={overlay.color}
          />
        </DragGate3D>
      )),
    [polylines, overlayRotation, labelAlpha, polylineWidth, handleSelect]
  );

  const getOnShiftClickLabelCallback = useOnShiftClickLabel();

  useEffect(() => {
    const unsub = (fos as any).selectiveRenderingEventBus?.on?.(
      FO_LABEL_TOGGLED_EVENT,
      (e: any) => { getOnShiftClickLabelCallback(e); }
    );
    return () => { unsub?.(); };
  }, [getOnShiftClickLabelCallback]);

  return (
    <group>
      <mesh rotation={overlayRotation}>{cuboidOverlays}</mesh>
      {polylineOverlays}
    </group>
  );
};
