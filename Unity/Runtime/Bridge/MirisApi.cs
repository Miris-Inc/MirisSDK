// Copyright © 2026 Miris, Inc. All rights reserved.

// C# Standard library
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

// Unity packages
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The functionality in this file is subject to change as the scene API evolves.

namespace Miris.Runtime
{

    /// <summary>
    /// Details the Miris C# API for Unity.
    /// See $AQUA_ROOT/modules/AquaApi/include/AquaApi/AquaApi.h
    /// for the corresponding C API.
    /// </summary>
    public class MirisApi
    {
        public const string AquaUnityPath =
#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
            // We use .framework on iOS and visionOS. __Internal rather than the library name even
            // though AquaUnity.framework is a dynamic library there: IL2CPP treats a named library
            // as a path to dlopen at runtime and looks for "/AquaUnity", which does not exist.
            // __Internal resolves the symbols at link time instead, which works because
            // UnityFramework already links the embedded framework.
            "__Internal";
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            "libAquaUnity.so";
#else
            // We use dynamic libraries on other platforms
            "AquaUnity";
#endif

        static public int UNITY_CLIENT = 1;

        // C to C# Mapping:
        // void* == IntPtr
        // const char* == string
        // char* == StringBuilder

        /// <summary>
        /// Gets the raw version of the Aqua native library.
        /// Formatted as "vX_Y_Z"
        /// Must be marshalled, like <see cref="Marshal.PtrToStringAnsi(IntPtr)"/>.
        /// </summary>
        /// <returns>An IntPtr to the version string</returns>
        [DllImport(AquaUnityPath)]
        static public extern IntPtr GetLibAquaVersion();

        /// <summary>
        /// Gets the raw version of the Aqua native library as a managed string.
        /// Formatted as "vX_Y_Z"
        /// </summary>
        /// <returns>The version string</returns>
        static public string GetLibAquaVersionString()
        {
            IntPtr versionPtr = GetLibAquaVersion();
            if (versionPtr == IntPtr.Zero)
                return string.Empty;
            return Marshal.PtrToStringAnsi(versionPtr);
        }
    }
}
