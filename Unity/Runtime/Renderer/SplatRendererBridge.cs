// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Runtime.InteropServices;

namespace Miris.Runtime
{
    // P/Invoke surface for the native splat renderer - mirrors SplatRendererBridge.h.
    public static class SplatRendererBridge
    {
        // Event IDs for the render event callback - mirrors kAquaSplatRendererEvent* in
        // SplatRendererBridge.h. Frame renders a frame; Shutdown releases everything Shark owns,
        // which has to happen on the render thread. See SplatRenderer.Teardown.
        public const int FrameEventId = 0;
        public const int ShutdownEventId = 1;

        [DllImport(MirisApi.AquaUnityPath)]
        static public extern int AquaSplatRenderer_Create(int width, int height);

        [DllImport(MirisApi.AquaUnityPath)]
        static public extern void AquaSplatRenderer_Destroy(int renderer);

        // Attaches the renderer to the aqua client the Miris SDK owns, so the SDK streams the asset
        // and Shark only renders it. Deferred to the render thread like Create, so 0 means
        // accepted, not adopted.
        [DllImport(MirisApi.AquaUnityPath)]
        static public extern int AquaSplatRenderer_AdoptClient(int renderer, IntPtr client, string assetId, int maxSplats);

        // Whether AdoptClient has taken effect. It only queues the request, and the asset must not
        // be asked for until this returns 1: changes drained before native has a context are
        // consumed by the SDK and lost. 1 adopted, 0 not yet, -1 bad renderer.
        [DllImport(MirisApi.AquaUnityPath)]
        static public extern int AquaSplatRenderer_IsClientAdopted(int renderer);

        // Sets the transform of the streamed content, so moving the MirisStream moves its splats. Both
        // arrays are 16 floats, column-major - see SplatRenderer.ToColumnMajor. Stages only, so it is
        // cheap to call every frame; the upload rides the next render event.
        [DllImport(MirisApi.AquaUnityPath)]
        static public extern void AquaSplatRenderer_SetModelTransform(int renderer, int modelRootObjectId, float[] transform, float[] inverse);

        // Hands native the scene changes the SDK drained this frame. Must be called from inside the
        // drain scope (MirisStreamController.SceneChangesDrained): the scene lock is held there and
        // the arrays are freed when it exits, so this one is NOT deferred to the render thread.
        // Returns 1 if an upload is pending, 0 if nothing applied, -1 if rejected.
        [DllImport(MirisApi.AquaUnityPath)]
        static public extern int AquaSplatRenderer_ApplySceneChanges(int renderer, ref SceneChangeIds sceneChangeIds);

        // Surfaces per view. Read from native rather than mirrored here - the two must agree
        // exactly or the composite samples a surface Shark is mid-write on.
        [DllImport(MirisApi.AquaUnityPath)]
        static public extern int AquaSplatRenderer_GetRingDepth();

        [DllImport(MirisApi.AquaUnityPath)]
        static public extern int AquaSplatRenderer_CreateTarget(int renderer, int viewIndex, int slot, int width, int height, out IntPtr outUnityTexture);

        // view and proj must be 16 floats in COLUMN-major order - see
        // SplatRenderer.ToColumnMajor. ValueConversion.MatrixToFloatArray is row-major and is
        // the wrong helper for this call.
        [DllImport(MirisApi.AquaUnityPath)]
        static public extern void AquaSplatRenderer_SetView(int renderer, int viewIndex, float[] view, float[] proj);

        // Which ring slot to sample for a view this frame - the most recently completed one, not
        // the one being written. See SplatRendererBridge.h.
        [DllImport(MirisApi.AquaUnityPath)]
        static public extern int AquaSplatRenderer_GetPresentSlot(int renderer, int viewIndex);

        [DllImport(MirisApi.AquaUnityPath)]
        static public extern IntPtr AquaSplatRenderer_GetRenderEventCallbackPtr();

        [DllImport(MirisApi.AquaUnityPath)]
        static public extern void AquaSplatRenderer_Shutdown();
    }
}
