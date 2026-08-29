// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Runtime.InteropServices;

namespace Miris.Runtime.Experimental
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

        [DllImport(MirisApi.AquaUnityPath)]
        static public extern int AquaSplatRenderer_StreamAsset(int renderer, string viewerKey, string assetId, string serverUrl, int maxSplats);

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
