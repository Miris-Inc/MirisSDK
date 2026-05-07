// TODO: restore when re-enabling annotation
// import useCanAnnotate from "@fiftyone/core/src/components/Modal/Sidebar/Annotate/useCanAnnotate";
import * as fos from "@fiftyone/state";
import type { CameraControls } from "@react-three/drei";
// TODO: restore when re-enabling annotation
// import { useAtomValue } from "jotai";
import React, { useCallback, useEffect } from "react";
import { useRecoilValue, useSetRecoilState } from "recoil";
import { type PerspectiveCamera, Quaternion, Vector3 } from "three";
// TODO: restore when re-enabling annotation
// import { useWorkingLabel } from "../annotation/store";
// import type { ReconciledDetection3D, ReconciledPolyline3D } from "../annotation/types";
import {
  SET_EGO_VIEW_EVENT,
  SET_TOP_VIEW_EVENT,
  SET_ZOOM_TO_SELECTED_EVENT,
} from "../constants";
import { useFo3dContext } from "../fo3d/context";
import {
  annotationPlaneAtom,
  cameraViewStatusAtom,
  isFo3dBackgroundOnAtom,
  selectedLabelForAnnotationAtom,
} from "../state";
import { isDetection3dOverlay, isPolyline3dOverlay } from "../types";

interface UseCameraViewsProps {
  cameraRef: React.RefObject<PerspectiveCamera>;
  cameraControlsRef: React.RefObject<CameraControls>;
}

export const useCameraViews = ({
  cameraRef,
  cameraControlsRef,
}: UseCameraViewsProps) => {
  const { sceneBoundingBox, upVector } = useFo3dContext();
  const setCameraViewStatus = useSetRecoilState(cameraViewStatusAtom);
  const annotationPlane = useRecoilValue(annotationPlaneAtom);
  // TODO: restore when re-enabling annotation
  // const canAnnotate = useCanAnnotate();
  // const mode = useAtomValue(fos.modalMode);
  // const enableAnnotationPlaneCameraView = canAnnotate && mode === "annotate";
  const enableAnnotationPlaneCameraView = false;
  // TODO: restore when re-enabling annotation
  // const selectedLabelForAnnotation = useRecoilValue(selectedLabelForAnnotationAtom);
  const setIsFo3dBackgroundOn = useSetRecoilState(isFo3dBackgroundOnAtom);

  // TODO: restore when re-enabling annotation
  // const workingLabel = useWorkingLabel(selectedLabelForAnnotation?._id ?? "");

  const calculateCameraPosition = useCallback(
    (direction: Vector3) => {
      if (
        !sceneBoundingBox ||
        !cameraRef.current ||
        !cameraControlsRef.current
      ) {
        return null;
      }

      const currentCameraPosition = cameraRef.current.position.clone();
      const lookAt = new Vector3();
      cameraControlsRef.current.getTarget(lookAt);

      const currentRadius = currentCameraPosition.distanceTo(lookAt);

      const center = sceneBoundingBox.getCenter(new Vector3());
      const size = sceneBoundingBox.getSize(new Vector3());
      const maxSize = Math.max(size.x, size.y, size.z);
      const minRadius = maxSize * 0.5;
      const maxRadius = maxSize * 3;

      const radius = Math.max(minRadius, Math.min(maxRadius, currentRadius));

      const maxLookAtDistance = maxSize * 1.3;
      const lookAtDistance = lookAt.distanceTo(center);
      let constrainedLookAt = lookAt;

      if (lookAtDistance > maxLookAtDistance) {
        const directionToLookAt = lookAt.clone().sub(center).normalize();
        constrainedLookAt = center
          .clone()
          .add(directionToLookAt.multiplyScalar(maxLookAtDistance));
      }

      const cameraPosition = constrainedLookAt
        .clone()
        .add(direction.clone().multiplyScalar(radius));

      return {
        cameraPosition,
        center: constrainedLookAt,
      };
    },
    [sceneBoundingBox, cameraRef, cameraControlsRef]
  );

  const applyCameraView = useCallback(
    (cameraPosition: Vector3, target: Vector3, viewName: string) => {
      if (!cameraControlsRef.current) {
        return;
      }

      cameraControlsRef.current.setLookAt(
        cameraPosition.x,
        cameraPosition.y,
        cameraPosition.z,
        target.x,
        target.y,
        target.z,
        true
      );

      setCameraViewStatus({
        viewName,
        timestamp: Date.now(),
      });
    },
    [cameraControlsRef, setCameraViewStatus]
  );

  const setCameraView = useCallback(
    (direction: Vector3, viewName: string) => {
      const result = calculateCameraPosition(direction);
      if (!result) {
        return;
      }

      const { cameraPosition, center } = result;
      applyCameraView(cameraPosition, center, viewName);
    },
    [calculateCameraPosition, applyCameraView]
  );

  const handleKeyDown = useCallback(
    (event: KeyboardEvent) => {
      const isInputMode =
        event.target instanceof HTMLInputElement ||
        event.target instanceof HTMLTextAreaElement;

      if (isInputMode) {
        return;
      }

      if (!event.metaKey && event.code === "KeyT") {
        setCameraViewStatus({
          viewName: "Top view",
          timestamp: Date.now(),
        });
        event.preventDefault();
        window.dispatchEvent(new CustomEvent(SET_TOP_VIEW_EVENT));
        return;
      }

      if (!event.metaKey && event.code === "KeyE") {
        setCameraViewStatus({
          viewName: "Ego view",
          timestamp: Date.now(),
        });
        event.preventDefault();
        window.dispatchEvent(new CustomEvent(SET_EGO_VIEW_EVENT));
        return;
      }

      if (!event.metaKey && event.code === "KeyZ") {
        setCameraViewStatus({
          viewName: "Crop",
          timestamp: Date.now(),
        });
        event.preventDefault();
        window.dispatchEvent(new CustomEvent(SET_ZOOM_TO_SELECTED_EVENT));
        return;
      }

      if (
        event.code === "KeyB" &&
        !event.ctrlKey &&
        !event.metaKey &&
        !event.altKey &&
        !event.shiftKey &&
        !event.repeat
      ) {
        setIsFo3dBackgroundOn((prev) => !prev);
        event.preventDefault();
        return;
      }

      if (
        !(event.code.startsWith("Numpad") || event.code.startsWith("Digit")) ||
        !upVector
      ) {
        return;
      }

      const numPressed = event.code.startsWith("Numpad")
        ? event.code.replace("Numpad", "")
        : event.code.replace("Digit", "");

      if (
        numPressed === "1" ||
        numPressed === "2" ||
        numPressed === "3"
        // TODO: restore when re-enabling annotation: || (numPressed === "4" && enableAnnotationPlaneCameraView)
      ) {
        event.preventDefault();
      }

      const isCtrlPressed = event.ctrlKey || event.metaKey;

      let direction: Vector3;
      let viewName: string;

      const isYUp = Math.abs(upVector.y) === 1;
      const isXUp = Math.abs(upVector.x) === 1;
      const isZUp = Math.abs(upVector.z) === 1;

      if (numPressed === "1") {
        if (isCtrlPressed) {
          viewName = "Bottom view";
          if (isYUp) direction = new Vector3(0, -1, 0);
          else if (isXUp) direction = new Vector3(-1, 0, 0);
          else if (isZUp) direction = new Vector3(0, 0, -1);
          else direction = new Vector3(0, 0, -1);
        } else {
          viewName = "Top view";
          if (isYUp) direction = new Vector3(0, 1, 0);
          else if (isXUp) direction = new Vector3(1, 0, 0);
          else if (isZUp) direction = new Vector3(0, 0, 1);
          else direction = new Vector3(0, 0, 1);
        }
      } else if (numPressed === "2") {
        if (isCtrlPressed) {
          viewName = "Left view";
          if (isYUp) direction = new Vector3(-1, 0, 0);
          else if (isXUp) direction = new Vector3(0, -1, 0);
          else if (isZUp) direction = new Vector3(-1, 0, 0);
          else direction = new Vector3(-1, 0, 0);
        } else {
          viewName = "Right view";
          if (isYUp) direction = new Vector3(1, 0, 0);
          else if (isXUp) direction = new Vector3(0, 1, 0);
          else if (isZUp) direction = new Vector3(1, 0, 0);
          else direction = new Vector3(1, 0, 0);
        }
      } else if (numPressed === "3") {
        if (isCtrlPressed) {
          viewName = "Back view";
          if (isYUp) direction = new Vector3(0, 0, -1);
          else if (isXUp) direction = new Vector3(0, 0, 1);
          else if (isZUp) direction = new Vector3(0, 1, 0);
          else direction = new Vector3(0, 1, 0);
        } else {
          viewName = "Front view";
          if (isYUp) direction = new Vector3(0, 0, 1);
          else if (isXUp) direction = new Vector3(0, 0, -1);
          else if (isZUp) direction = new Vector3(0, -1, 0);
          else direction = new Vector3(0, -1, 0);
        }
      // TODO: restore when re-enabling annotation
      // } else if (numPressed === "4" && enableAnnotationPlaneCameraView) {
      //   const quat = new Quaternion(...annotationPlane.quaternion);
      //   const normal = new Vector3(0, 0, 1).applyQuaternion(quat).normalize();
      //   if (isCtrlPressed) {
      //     direction = normal.clone().negate();
      //     viewName = "Annotation plane view 2";
      //   } else {
      //     direction = normal.clone();
      //     viewName = "Annotation plane view 1";
      //   }
      } else {
        return;
      }

      // TODO: restore when re-enabling annotation (zoom-to-label on camera view)
      // if (selectedLabelForAnnotation && cameraControlsRef.current) {
      //   const labelInfo = calculateLabelCentroidAndRadius(workingLabel);
      //   if (labelInfo) {
      //     const { centroid, radius } = labelInfo;
      //     const cameraPosition = centroid.clone().add(direction.clone().multiplyScalar(radius));
      //     applyCameraView(cameraPosition, centroid, viewName);
      //     return;
      //   }
      // }

      setCameraView(direction, viewName);
    },
    [
      upVector,
      setCameraView,
      applyCameraView,
      setCameraViewStatus,
      setIsFo3dBackgroundOn,
    ]
  );

  useEffect(() => {
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [handleKeyDown]);
};
