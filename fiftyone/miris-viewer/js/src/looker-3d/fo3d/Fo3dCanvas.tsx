import * as fos from "@fiftyone/state";
import {
  AdaptiveDpr,
  AdaptiveEvents,
  CameraControls,
  OrbitControls,
  PerspectiveCamera as PerspectiveCameraDrei,
} from "@react-three/drei";
// TODO: restore when re-enabling annotation
// import { useAtomValue } from "jotai";
import type * as THREE from "three";
import type { Vector3 } from "three";
import { SpinningCube } from "../SpinningCube";
import { StatusTunnel } from "../StatusBar";
// TODO: restore when re-enabling annotation
// import { AnnotationPlane } from "../annotation/AnnotationPlane";
// import { CreateCuboidRenderer } from "../annotation/CreateCuboidRenderer";
// import { Crosshair3D } from "../annotation/Crosshair3D";
// import { SegmentPolylineRenderer } from "../annotation/SegmentPolylineRenderer";
import { PANEL_ID_MAIN } from "../constants";
import { FrustumCollection } from "../frustum";
import { useCameraViews } from "../hooks/use-camera-views";
import { ThreeDLabels, type ThreeDLabelsProps } from "../labels";
import { RaycastService } from "../services/RaycastService";
import { FoSceneComponent } from "./FoScene";
import { Gizmos } from "./Gizmos";
import type { Fo3dPointCloudSettings } from "./context";
import {
  MirisLiveLabelsProvider,
  useMirisLiveLabels,
} from "./MirisLiveLabelsContext";
import { FoScene } from "./render-types";
import { SceneControls } from "./scene-controls/SceneControls";

interface Fo3dSceneContentProps {
  cameraPosition: Vector3;
  upVector: Vector3 | null;
  fov?: number;
  near?: number;
  far?: number;
  aspect?: number;
  zoom?: number;
  autoRotate: boolean;
  cameraControlsRef: React.RefObject<CameraControls>;
  foScene: FoScene;
  isSceneInitialized: boolean;
  pointCloudSettings: Fo3dPointCloudSettings;
  assetsGroupRef: React.RefObject<THREE.Group>;
  cameraRef?: React.RefObject<THREE.PerspectiveCamera>;
  isGizmoHelperVisible?: boolean;
  /** Extra labels to render alongside FiftyOne dataset labels (e.g. from Miris stream) */
  extraLabels?: ThreeDLabelsProps["extraLabels"];
}

/**
 * Wraps the scene content in MirisLiveLabelsProvider so that MirisStream (a
 * descendant via FoSceneComponent) and ThreeDLabels share the same context
 * within the same R3F Canvas fiber tree — no cross-Canvas-boundary issues.
 */
export const Fo3dSceneContent = (props: Fo3dSceneContentProps) => {
  return (
    <MirisLiveLabelsProvider>
      <Fo3dSceneContentInner {...props} />
    </MirisLiveLabelsProvider>
  );
};

const Fo3dSceneContentInner = ({
  cameraPosition,
  upVector,
  fov = 50,
  near = 0.1,
  far = 2500,
  aspect = 1,
  zoom = 100,
  autoRotate,
  cameraControlsRef,
  foScene,
  isSceneInitialized,
  isGizmoHelperVisible,
  pointCloudSettings,
  assetsGroupRef,
  cameraRef,
  extraLabels,
}: Fo3dSceneContentProps) => {
  // TODO: restore when re-enabling annotation
  // const mode = useAtomValue(fos.modalMode);
  const { activeSampleMap: labelSampleMap } = (fos as any).useRenderConfig3dState();
  const { liveDetections } = useMirisLiveLabels();

  // For Miris scenes the Three.js LoadingManager never tracks the stream's
  // assets, so isSceneInitialized may stay false indefinitely. Treat the
  // scene as ready as soon as the first LOD activation arrives.
  const effectiveInit = isSceneInitialized || liveDetections.length > 0;

  useCameraViews({
    cameraRef,
    cameraControlsRef,
  });

  return (
    <>
      <RaycastService panelId={PANEL_ID_MAIN} />
      <StatusTunnel.Out />
      <AdaptiveDpr pixelated />
      <AdaptiveEvents />

      <PerspectiveCameraDrei
        makeDefault
        ref={cameraRef as React.MutableRefObject<THREE.PerspectiveCamera>}
        position={cameraPosition}
        up={upVector ?? [0, 1, 0]}
        fov={foScene?.cameraProps.fov ?? fov}
        near={foScene?.cameraProps.near ?? near}
        far={foScene?.cameraProps.far ?? far}
        aspect={foScene?.cameraProps.aspect ?? aspect}
        onUpdate={(cam) => cam.updateProjectionMatrix()}
      />

      {!autoRotate ? (
        <CameraControls
          smoothTime={0.1}
          dollySpeed={0.1}
          dollyToCursor
          ref={cameraControlsRef}
        />
      ) : (
        <OrbitControls autoRotate={autoRotate} makeDefault />
      )}

      <SceneControls scene={foScene} cameraControlsRef={cameraControlsRef} />

      <Gizmos
        isGizmoHelperVisible={isGizmoHelperVisible}
        isGridVisible={true}
      />
      {!effectiveInit && <SpinningCube />}

      <group ref={assetsGroupRef} visible={effectiveInit}>
        <FoSceneComponent scene={foScene} />
      </group>

      {effectiveInit && (
        <>
          <ThreeDLabels
            sampleMap={labelSampleMap}
            extraLabels={[...(extraLabels ?? []), ...liveDetections]}
          />
          <FrustumCollection />
        </>
      )}

      {/* TODO: restore when re-enabling annotation: {mode === "annotate" && <AnnotationControls />} */}
    </>
  );
};

// TODO: restore when re-enabling annotation
// const AnnotationControls = () => {
//   return (
//     <>
//       <AnnotationPlane panelType="main" viewType="top" />
//       <SegmentPolylineRenderer />
//       <CreateCuboidRenderer />
//       <Crosshair3D panelId={PANEL_ID_MAIN} />
//     </>
//   );
// };
