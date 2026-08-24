// Copyright © 2026 Miris, Inc. All rights reserved.

// C# Standard library
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

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
            // We use .framework on iOS and visionOS
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

        // ---------------------------------------------------------------
        // Renderer and editor entry points
        //
        // What remains after the client API moved to the SWIG-generated bindings in
        // MirisBindings. These are kept hand-written because they are not part of the
        // client surface: the render-event callback pointer and ECC LUT are consumed
        // directly by the renderer, PlotMetric is profiling, and GetMosaicDescriptors
        // reads through a pointer held in AttributeInfo.
        // ---------------------------------------------------------------

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus GetMosaicDescriptors(Int64 mosaicDescriptorPtr, MosaicDescriptorInfo[] mosaicDescriptor);

        [DllImport(AquaUnityPath)]
        static public extern IntPtr GetRenderEventCallbackPtr();

        [DllImport(AquaUnityPath)]
        static public extern void PlotMetric(string name, Int64 value);

        [DllImport(AquaUnityPath)]
        static public extern IntPtr GetEccLutData();

        static public Texture2D GetEccLUT()
        {
            IntPtr data = MirisApi.GetEccLutData();
            if (data.ToInt64() == 0) {
                // MirisApi.GetEccLutData can return 0ULL
                // if that happens, return null before we attempt a bad SetPixelData call
                return null;
            }
            NativeArray<byte> nativeArray = DataFormatUtils.WrapVoidPtrWithNativeArray(data, 256 * 256);
            Texture2D lut = new Texture2D(256, 256, TextureFormat.R8, false, true);
            lut.SetPixelData<byte>(nativeArray, mipLevel: 0);
            lut.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return lut;
        }
    }
}
