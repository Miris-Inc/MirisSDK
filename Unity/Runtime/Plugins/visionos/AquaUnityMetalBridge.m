// Copyright © 2026 Miris, Inc. All rights reserved.
//
// Hands Unity's MTLDevice and MTLCommandQueue to AquaUnity.framework on visionOS.
//
// The framework cannot get them itself. Unity only calls UnityPluginLoad for plugins it discovers
// and loads; on visionOS AquaUnity.framework is embedded and linked by UnityFramework instead, so
// IUnityGraphicsMetalV2 never arrives. UnityRegisterPlugin is not an escape - it is the
// subsystem-plugin API and crashes Unity when called by a plugin that registers no subsystems. And
// UnityGetMetalDevice, though declared in the public Classes/Unity/UnityInterface.h, is compiled
// with hidden visibility into the app target, so dlsym from another image cannot reach it either.
//
// What is left is this: a source file Unity compiles into the generated Xcode project, where
// UnityGetMetalDevice resolves at link time like any other call in that target. Managed code pulls
// the objects out through here and pushes them into the framework, so every hop is link-time
// resolved and none of it depends on symbol visibility or plugin registration.

#import <Foundation/Foundation.h>
#include "IUnityInterface.h"
#include "UnityInterface.h"

void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API AquaUnityGetUnityMetalObjects(void** outDevice, void** outCommandQueue)
{
    // __bridge, not __bridge_retained: Unity owns both objects for the lifetime of the player and
    // the framework only borrows them. Compiled with ARC, hence the cast is needed at all.
    if (outDevice != NULL)
    {
        *outDevice = (__bridge void*)UnityGetMetalDevice();
    }
    if (outCommandQueue != NULL)
    {
        *outCommandQueue = (__bridge void*)UnityGetMetalCommandQueue();
    }
}
