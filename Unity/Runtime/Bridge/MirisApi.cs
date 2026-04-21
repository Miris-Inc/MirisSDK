// Copyright © 2026 Miris, Inc. All rights reserved.

// C# Standard library
using AOT;
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
    using ClientHandle = IntPtr;
    
    /// <summary>
    /// Details the Miris C# API for Unity.
    /// See $AQUA_ROOT/modules/AquaApi/include/AquaApi/AquaApi.h
    /// for the corresponding C API.
    /// </summary>
    public class MirisApi
    {
        public const string AquaUnityPath =
#if UNITY_IOS && !UNITY_EDITOR
            // We use .framework on iOS
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
        /// Sets the logging level for Aqua. Options are Trace, Debug, Info, 
        /// Warn, Error, Critical.
        /// </summary>
        /// <param name="logLevel">The desired logging level</param>
        [DllImport(AquaUnityPath)]
        static public extern AquaStatus SetLogLevel(LogLevel logLevel);

        /// <summary>
        /// Gets the current logging level for Aqua.
        /// </summary>
        /// <returns>The current logging level</returns>
        [DllImport(AquaUnityPath)]
        static public extern LogLevel GetLogLevel();

        /// <summary>
        /// Gets the platform Aqua is expecting to be running on. Use is
        /// discouraged, most users should continue using <see cref="Application.platform"/>
        /// </summary>
        /// <returns>The Aqua runtime platform</returns>
        [DllImport(AquaUnityPath)]
        static public extern int GetPlatform();

        /// <summary>
        /// Indicates whether Aqua is a debug binary.
        /// </summary>
        /// <returns>1 if a debug binary, 0 otherwise</returns>
        [DllImport(AquaUnityPath)]
        static public extern int LibAquaIsDebug();

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

        [DllImport(AquaUnityPath)]
        static public extern ClientHandle CreateClient();

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus DestroyClient(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern bool SetPersistentDataDirectory(ClientHandle client, string dirPath);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus SetClientSpatialFormat(ClientHandle client, SpatialFormat spatialFormat);

        // ---------------------------------------------------------------
        // Asset Management API
        // ---------------------------------------------------------------

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus SetAssetViewerKey(ClientHandle client, string key);

        [DllImport(AquaUnityPath)]
        static public extern IntPtr GetDefaultEnvironment(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus SetServerEnvironmentAsync(ClientHandle client, string environment, SetServerEnvironmentCallback callback, IntPtr userData);

        // ---------------------------------------------------------------
        // Utility API
        // ---------------------------------------------------------------

        [DllImport(AquaUnityPath)]
        static public extern ulong PrefetchContent(ClientHandle client, string url);

        // ---------------------------------------------------------------
        // Scene API
        // --------------------------------------------------------------- 

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus ClearScene(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern int AddStream(ClientHandle client, string streamName, string contentUrl, bool doNotRefine);

        [DllImport(AquaUnityPath)]
        static public extern int AddStreamById(ClientHandle client, string streamName, string assetId, bool doNotRefine);

        [DllImport(AquaUnityPath)]
        static public extern bool RemoveStream(ClientHandle client, int streamObjectId);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus UpdateSceneExecution(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus WaitForSceneExecution(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus CancelAllSceneExecution(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern bool LockScene(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus UnlockScene(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus GetSceneChangesCounts(ClientHandle client,
            ref SceneChangeIds sceneChangeIds
        );

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus GetSceneChanges(ClientHandle client, 
            ref SceneChangeIds sceneChangeIds
        );

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus SetMainCameraTransform(ClientHandle client, float[] transform);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus SetMainCameraViewFrustum(ClientHandle client, float aspectRatio, float verticalFov, float nearPlane, float farPlane);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus SetSceneObjectTransform(ClientHandle client, int sceneObjectId, float[] transform);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus SetRuntimeSettings(ClientHandle client, ref RuntimeSettings runtimeSettings);

        [DllImport(AquaUnityPath)]
        static public extern int GetCameraCount(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus GetCameraIds(ClientHandle client, int[] cameraIndices);

        // ---------------------------------------------------------------
        // Scene Object API
        // --------------------------------------------------------------- 

        [DllImport(AquaUnityPath)]
        static public extern int GetSceneRootObjectId(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus PrintSceneObjectHierarchy(ClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static public extern int GetSceneObjectType(ClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static public extern int GetSceneObjectParent(ClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static public extern bool IsSceneObjectAncestorOf(ClientHandle client, int sceneObjectId, int descendantObjectId);

        [DllImport(AquaUnityPath)]
        static public extern IntPtr GetSceneObjectName(ClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static public extern int GetAttributeCount(ClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static public extern bool HasAttribute(ClientHandle client, int sceneObjectId, string attributeName);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus GetAttribute(ClientHandle client, int sceneObjectId, string attributeName, ref AttributeInfo attributeInfo);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus GetMosaicDescriptors(Int64 mosaicDescriptorPtr, MosaicDescriptorInfo[] mosaicDescriptor);

        [DllImport(AquaUnityPath)]
        static public extern IntPtr GetRenderEventCallbackPtr();

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus MirisGetLocalTransform(ClientHandle client, int sceneObjectId, float[] transformData);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus GetLocalBoundingBox(ClientHandle client, int sceneObjectId, float[] boundingBox);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus RecordFrameInfo(ClientHandle client, ref FrameInfo FrameInfo);

        [DllImport(AquaUnityPath)]
        static public extern int GetLodIndex(ClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static public extern AquaStatus GetLodMinMaxIndices(ClientHandle client, ref int minLodIndex, ref int maxLodIndex);

        [DllImport(AquaUnityPath)]
        static public extern int GetSceneOperatorCount(ClientHandle client);

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
