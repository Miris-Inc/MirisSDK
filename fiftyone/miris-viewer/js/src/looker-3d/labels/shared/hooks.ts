import * as fos from "@fiftyone/state";
import { useCursor } from "@react-three/drei";
import type { ThreeEvent } from "@react-three/fiber";
import { useCallback, useState } from "react";
import { useRecoilCallback, useRecoilValue } from "recoil";
import { use3dLabelColor } from "../../hooks/use-3d-label-color";
import { useSimilarLabels3d } from "../../hooks/use-similar-labels-3d";
import {
  editSegmentsModeAtom,
  isActivelySegmentingSelector,
} from "../../state";
import type { BaseOverlayProps, EventHandlers, HoverState } from "../../types";

export const useHoverState = (): HoverState => {
  const isSegmenting = useRecoilValue(isActivelySegmentingSelector);
  const [isHovered, setIsHovered] = useState(false);
  const isEditSegmentsMode = useRecoilValue(editSegmentsModeAtom);

  useCursor(
    isHovered,
    isSegmenting || isEditSegmentsMode ? "crosshair" : "pointer",
    isSegmenting ? "crosshair" : "auto"
  );

  return { isHovered, setIsHovered };
};

const useMeshTooltipProps = (label: any) => {
  const onPointerOver = useRecoilCallback(
    ({ set }) =>
      () => {
        set(fos.tooltipDetail, {
          field: Array.isArray(label.path)
            ? label.path.at(-1)
            : label.path,
          label,
          type: label.type,
          color: label.color,
          sampleId: label.sampleId,
        });
      },
    [label]
  );

  const onPointerOut = useRecoilCallback(
    ({ snapshot, set }) =>
      () => {
        const isTooltipLocked = snapshot
          .getLoadable(fos.isTooltipLocked)
          .getValue();
        if (!isTooltipLocked) {
          set(fos.tooltipDetail, null);
        }
      },
    []
  );

  const onPointerMissed = useRecoilCallback(
    ({ snapshot, set }) =>
      () => {
        const isTooltipLocked = snapshot
          .getLoadable(fos.isTooltipLocked)
          .getValue();
        if (!isTooltipLocked) {
          set(fos.tooltipDetail, null);
        }
      },
    []
  );

  const onPointerMove = useRecoilCallback(
    ({ snapshot, set }) =>
      (e: ThreeEvent<PointerEvent>) => {
        const isTooltipLocked = snapshot
          .getLoadable(fos.isTooltipLocked)
          .getValue();
        if (isTooltipLocked) return;

        if (e.ctrlKey) {
          set(fos.isTooltipLocked, true);
        } else {
          set(
            fos.tooltipCoordinates,
            fos.computeCoordinates([e.clientX, e.clientY])
          );
        }
      },
    []
  );

  return { onPointerOver, onPointerOut, onPointerMissed, onPointerMove };
};

export const useEventHandlers = (label: any): EventHandlers => {
  const { onPointerOver, onPointerOut, ...rest } = useMeshTooltipProps(label);

  return {
    onPointerOver: useCallback(() => {
      onPointerOver();
    }, [onPointerOver]),
    onPointerOut: useCallback(() => {
      onPointerOut();
    }, [onPointerOut]),
    ...rest,
  };
};

export const useLabelColor = (
  props: Pick<BaseOverlayProps, "selected" | "color">,
  isHovered: boolean,
  label: any,
  isSelectedForAnnotation?: boolean
) => {
  const isSimilarLabelHovered = useSimilarLabels3d(label);

  const strokeAndFillColor = use3dLabelColor({
    isSelected: props.selected,
    isHovered,
    isSimilarLabelHovered,
    defaultColor: props.color,
    isSelectedForAnnotation,
  });

  return { strokeAndFillColor, isSimilarLabelHovered };
};
