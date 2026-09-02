// Copyright © 2026 Miris, Inc. All rights reserved.

#if UNITY_VISIONOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Miris.Runtime
{
    // Gives AquaUnity.framework Unity's MTLDevice and MTLCommandQueue on visionOS.
    //
    // The framework has no way to reach them on its own. Unity only calls UnityPluginLoad for
    // plugins it discovers and loads, and on visionOS the framework is embedded and linked by
    // UnityFramework instead, so IUnityGraphicsMetalV2 never arrives. UnityRegisterPlugin is the
    // subsystem-plugin API and crashes Unity when used by a plugin that registers no subsystems.
    // UnityGetMetalDevice is compiled into the app target with hidden visibility, so dlsym from
    // another image cannot see it either.
    //
    // So the objects travel: Unity's generated Xcode project -> AquaUnityMetalBridge.m (compiled
    // into the app target, where UnityGetMetalDevice links normally) -> here -> the framework.
    // Every hop resolves at link time, depending on neither symbol visibility nor registration.
    static class VisionOSMetalBridge
    {
        // In the generated Xcode project, not the framework - hence __Internal.
        [DllImport("__Internal")]
        static extern void AquaUnityGetUnityMetalObjects(out IntPtr device, out IntPtr commandQueue);

        [DllImport(MirisApi.AquaUnityPath)]
        static extern void AquaUnity_SetUnityMetalObjects(IntPtr device, IntPtr commandQueue);

        // AfterAssembliesLoaded: the graphics device exists by now, and this is well before any
        // camera render event can ask the bridge for a device.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Install()
        {
            AquaUnityGetUnityMetalObjects(out IntPtr device, out IntPtr commandQueue);
            AquaUnity_SetUnityMetalObjects(device, commandQueue);
            if (device == IntPtr.Zero || commandQueue == IntPtr.Zero)
            {
                Debug.LogError("Miris: Unity returned no Metal device or command queue - the splat bridge "
                               + "cannot share surfaces with Unity");
                return;
            }
            Debug.Log("Miris: handed Unity's Metal device and command queue to AquaUnity");
        }
    }
}
#endif
