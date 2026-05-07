import { Loading, LoadingDots } from "@fiftyone/components";
// TODO: restore when re-enabling annotation
// import useCanAnnotate from "@fiftyone/core/src/components/Modal/Sidebar/Annotate/useCanAnnotate";
import { usePluginSettings } from "@fiftyone/plugins";
import * as fos from "@fiftyone/state";
import type { CameraControls } from "@react-three/drei";
import { useEffect, useMemo, useReducer, useRef } from "react";
import {
  LoadingManager,
  type Group,
  type PerspectiveCamera,
  type Vector3,
} from "three";
// TODO: restore when re-enabling annotation
// import { MultiPanelView } from "../annotation/MultiPanelView";
// import { AnnotationToolbar } from "../annotation/annotation-toolbar/AnnotationToolbar";
// TODO: restore when re-enabling annotation
// import { ANNOTATION_CUBOID, ANNOTATION_POLYLINE } from "../constants";
import {
  useFo3d,
  useFo3dCameraControlsConfig,
  useFo3dCameraInitialization,
  useFo3dCameraViewEvents,
  useFo3dInteractionLifecycle,
  useFo3dPanelRouting,
  useFo3dSceneBounds,
  useFo3dSceneContextState,
  useTrackStatus,
} from "../hooks";
import type { Looker3dSettings } from "../settings";
// TODO: restore when re-enabling annotation
// import { useCurrent3dAnnotationMode } from "../state/accessors";
// import { Annotation3d } from "./Annotation3d";
import {
  FO3D_CAMERA_LIFECYCLE,
  FO3D_CAMERA_LIFECYCLE_ACTION,
  fo3dCameraLifecycleReducer,
  isFo3dSceneReady,
  type Fo3dCameraLifecycleState,
} from "./camera-lifecycle";
import { Fo3dSceneContext } from "./context";
import { FoScene } from "./render-types";
import { SinglePanelView } from "./SinglePanelView";

interface Fo3dPanelsProps {
  upVector: Vector3 | null;
  assetsGroupRef: React.RefObject<Group>;
  foScene: FoScene;
  interactionSample: fos.ModalSample;
  cameraRef: React.RefObject<PerspectiveCamera>;
  cameraControlsRef: React.RefObject<CameraControls>;
  mountCameraPosition: Vector3;
  cameraLifecycleState: Fo3dCameraLifecycleState;
  mode: string;
}

const Fo3dPanels = ({
  upVector,
  assetsGroupRef,
  foScene,
  interactionSample,
  cameraRef,
  cameraControlsRef,
  mountCameraPosition,
  cameraLifecycleState,
  mode,
}: Fo3dPanelsProps) => {
  const { resetActiveNode } = useFo3dInteractionLifecycle({
    cameraLifecycleState,
    interactionSample,
    upVector,
    mode,
    cameraControlsRef,
  });

  // TODO: restore when re-enabling annotation (multi-panel view for annotate mode)
  // if (shouldRenderMultiPanelView) {
  //   return <MultiPanelView ... />;
  // }

  return (
    <SinglePanelView
      assetsGroupRef={assetsGroupRef}
      foScene={foScene}
      cameraRef={cameraRef}
      cameraControlsRef={cameraControlsRef}
      defaultCameraPosition={mountCameraPosition}
      onPointerMissed={resetActiveNode}
    />
  );
};

const Fo3dLoadErrorState = ({ error }: { error: Error | null }) => {
  const message = error?.message
    ? `Failed to load 3D scene: ${error.message}`
    : "Failed to load 3D scene";

  return (
    <Loading
      dataCy="looker3d"
      wrapperStyle={{ textAlign: "center", maxWidth: 420 }}
    >
      <div data-cy="looker-error-info">{message}</div>
    </Loading>
  );
};

export const MediaTypeFo3dComponent = () => {
  const { interactionSample, sceneSample } = fos.useRenderConfig3dState();
  const settings = usePluginSettings<Looker3dSettings>("3d");
  const mode = fos.useModalMode();
  // TODO: restore when re-enabling annotation
  // const canAnnotate = useCanAnnotate().showAnnotationTab;
  const canAnnotate = false;
  // const current3dAnnotationMode = useCurrent3dAnnotationMode();
  const sceneSampleId = sceneSample.id ?? sceneSample.sample._id;
  const loadingManager = useMemo(() => new LoadingManager(), [sceneSampleId]);

  const {
    foScene,
    isLoading: isParsingFo3d,
    loadError,
    fo3dRoot,
    rootAssetCount,
  } = useFo3d(sceneSample);

  const [cameraLifecycleState, dispatchCameraLifecycle] = useReducer(
    fo3dCameraLifecycleReducer,
    FO3D_CAMERA_LIFECYCLE.WAITING_FOR_SCENE
  );
  const isSceneReady = isFo3dSceneReady({
    cameraLifecycleState,
    foScene,
    rootAssetCount,
  });

  // Reset camera initialization whenever the scene identity changes.
  useEffect(() => {
    dispatchCameraLifecycle({
      type: FO3D_CAMERA_LIFECYCLE_ACTION.WAIT_FOR_SCENE,
    });
  }, [sceneSampleId]);

  const cameraRef = useRef<PerspectiveCamera | null>(null);
  const cameraControlsRef = useRef<CameraControls | null>(null);
  const assetsGroupRef = useRef<Group | null>(null);
  const threeJsLoadingStatus = useTrackStatus(loadingManager, isSceneReady);

  useFo3dCameraControlsConfig({
    cameraControlsRef,
  });

  const {
    sceneBoundingBox,
    recomputeBounds,
    isComputingSceneBoundingBox,
    isBoundsResolved,
  } = useFo3dSceneBounds({
    assetsGroupRef,
    foScene,
    isParsingFo3d,
    rootAssetCount,
    isThreeJsLoading: threeJsLoadingStatus.isLoading,
  });

  const { upVector, effectiveSceneBoundingBox, contextValue } =
    useFo3dSceneContextState({
      foScene,
      settings,
      sceneBoundingBox,
      isComputingSceneBoundingBox,
      rootAssetCount,
      fo3dRoot,
      loadingManager,
      cameraLifecycleState,
      isSceneReady,
    });

  const { currentRenderPath } = useFo3dPanelRouting({
    mode,
    canAnnotate,
    isSceneReady,
    recomputeBounds,
  });

  const { mountCameraPosition } = useFo3dCameraInitialization({
    cameraRef,
    cameraControlsRef,
    currentRenderPath,
    foScene,
    sceneBoundingBox,
    upVector,
    settings,
    isBoundsResolved,
    dispatchCameraLifecycle,
  });

  useFo3dCameraViewEvents({
    cameraRef,
    cameraControlsRef,
    effectiveSceneBoundingBox,
    sceneBoundingBox,
    upVector,
    foScene,
    settings,
    recomputeBounds,
  });

  // TODO: restore when re-enabling annotation
  // const isPolylineAnnotateActive = current3dAnnotationMode === ANNOTATION_POLYLINE;
  // const isCuboidAnnotateActive = current3dAnnotationMode === ANNOTATION_CUBOID;
  // const shouldShowAnnotationToolbar =
  //   mode === fos.ModalMode.ANNOTATE && (isPolylineAnnotateActive || isCuboidAnnotateActive);

  if (isParsingFo3d) {
    return <LoadingDots />;
  }

  if (!foScene) {
    return <Fo3dLoadErrorState error={loadError} />;
  }

  return (
    <Fo3dSceneContext.Provider value={contextValue}>
      {/* TODO: restore when re-enabling annotation: {canAnnotate && <Annotation3d />} */}
      <Fo3dPanels
        upVector={upVector}
        assetsGroupRef={assetsGroupRef}
        foScene={foScene}
        interactionSample={interactionSample}
        cameraRef={cameraRef}
        cameraControlsRef={cameraControlsRef}
        mountCameraPosition={mountCameraPosition}
        cameraLifecycleState={cameraLifecycleState}
        mode={mode}
      />
      {/* TODO: restore when re-enabling annotation: {shouldShowAnnotationToolbar && <AnnotationToolbar />} */}
    </Fo3dSceneContext.Provider>
  );
};
