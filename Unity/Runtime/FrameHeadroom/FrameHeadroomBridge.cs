// Copyright © 2026 Miris, Inc. All rights reserved.


// On iOS/visionOS a DllImport with no symbol fails the UnityFramework link - ifdef out.
#if UNITY_VISIONOS || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
using System;
using System.Runtime.InteropServices;

namespace Miris.Runtime
{
    // Expose FrameHeadroom.h
    public static class FrameHeadroomBridge
    {
        public const int FrameBeginEventId = 0;
        public const int FrameEndEventId = 1;

        [DllImport(MirisApi.AquaUnityPath)]
        public static extern IntPtr AquaFrameHeadroom_GetRenderEventCallbackPtr();

        [DllImport(MirisApi.AquaUnityPath)]
        public static extern long AquaFrameHeadroom_GetLatestSample(
            out double hostGpuBusyMs, out double offQueueBusyMs);

        [DllImport(MirisApi.AquaUnityPath)]
        public static extern int AquaFrameHeadroom_IsMeasuring();

        [DllImport(MirisApi.AquaUnityPath)]
        public static extern void AquaFrameHeadroom_SetActive(int active);

        [DllImport(MirisApi.AquaUnityPath)]
        public static extern double AquaFrameHeadroom_ThreadCpuTimeSeconds();
    }
}
#endif
