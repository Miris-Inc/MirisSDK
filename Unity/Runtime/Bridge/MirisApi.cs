// Copyright © 2025 Miris, Inc. All rights reserved.

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

    public static class InteropUtils
    {
        public static T[] MarshalArrayFromPtr<T>(IntPtr ptr, int count) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            var array = new T[count];

            for (int i = 0; i < count; i++)
            {
                array[i] = Marshal.PtrToStructure<T>(IntPtr.Add(ptr, i * size));
            }

            return array;
        }

        public static string[] MarshalStringArrayFromPtr(IntPtr ptr, int count)
        {
            int size = Marshal.SizeOf<IntPtr>();
            var array = new string[count];

            for (int i = 0; i < count; i++)
            {
                var strPtr = Marshal.ReadIntPtr(IntPtr.Add(ptr, i * size));
                array[i] = Marshal.PtrToStringAnsi(strPtr);
            }

            return array;
        }

        public static void NativeAsyncCallbackArray<T>(IntPtr ptr, int count, IntPtr userData) where T : struct
        {
            var handle = GCHandle.FromIntPtr(userData);
            var tcs = (TaskCompletionSource<T[]>)handle.Target;
            handle.Free();
            tcs.SetResult(MarshalArrayFromPtr<T>(ptr, count));
        }

        public static void NativeAsyncCallbackStringArray(IntPtr ptr, int count, IntPtr userData)
        {
            var handle = GCHandle.FromIntPtr(userData);
            var tcs = (TaskCompletionSource<string[]>)handle.Target;
            handle.Free();
            tcs.SetResult(MarshalStringArrayFromPtr(ptr, count));
        }
    }
    
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
        static public extern void SetLogLevel(LogLevel logLevel);

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

        [DllImport(AquaUnityPath)]
        static public extern ClientHandle CreateClient();

        [DllImport(AquaUnityPath)]
        static public extern void DestroyClient(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern bool SetPersistentDataDirectory(ClientHandle client, string dirPath);

        [DllImport(AquaUnityPath)]
        static public extern void SetClientSpatialFormat(ClientHandle client, SpatialFormat spatialFormat);

        // ---------------------------------------------------------------
        // Asset Management API
        // ---------------------------------------------------------------

        [DllImport(AquaUnityPath)]
        static public extern void SetAssetViewerKey(ClientHandle client, string key);

        [DllImport(AquaUnityPath)]
        static public extern void GetAssets(ClientHandle client, IntPtr tags, int tagsCount, FillNativeArrayCallback callback, IntPtr userData);

        [DllImport(AquaUnityPath)]
        static public extern void GetAvailableTags(ClientHandle client, FillNativeArrayCallback callback, IntPtr userData);

        [DllImport(AquaUnityPath)]
        static public extern void GetAvailableEnvironments(ClientHandle client, FillNativeArrayCallback callback, IntPtr userData);

        [DllImport(AquaUnityPath)]
        static public extern IntPtr GetDefaultEnvironment(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern void SetServerEnvironment(ClientHandle client, string environment, SetServerEnvironmentCallback callback, IntPtr userData);

        // ---------------------------------------------------------------
        // Utility API
        // ---------------------------------------------------------------

        [DllImport(AquaUnityPath)]
        static public extern ulong PrefetchContent(ClientHandle client, string url);

        // ---------------------------------------------------------------
        // Scene API
        // --------------------------------------------------------------- 

        [DllImport(AquaUnityPath)]
        static public extern void ClearScene(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern int AddStream(ClientHandle client, string streamName, string contentUrl, int clientType, bool doNotRefine);

        [DllImport(AquaUnityPath)]
        static public extern int AddStreamById(ClientHandle client, string streamName, string assetId, int clientType, bool doNotRefine, AddStreamCallback callback, IntPtr userData);

        [DllImport(AquaUnityPath)]
        static public extern bool RemoveStream(ClientHandle client, int streamObjectId);

        [DllImport(AquaUnityPath)]
        static public extern void UpdateSceneExecution(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern void WaitForSceneExecution(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern void CancelAllSceneExecution(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern bool LockScene(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern void UnlockScene(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern int GetSceneChangesCounts(ClientHandle client,
            ref SceneChangeIds sceneChangeIds
        );

        [DllImport(AquaUnityPath)]
        static public extern int GetSceneChanges(ClientHandle client, 
            ref SceneChangeIds sceneChangeIds
        );

        [DllImport(AquaUnityPath)]
        static public extern void SetMainCameraTransform(ClientHandle client, float[] transform);

        [DllImport(AquaUnityPath)]
        static public extern void SetMainCameraViewFrustum(ClientHandle client, float aspectRatio, float verticalFov, float nearPlane, float farPlane);

        [DllImport(AquaUnityPath)]
        static public extern void SetSceneObjectTransform(ClientHandle client, int sceneObjectId, float[] transform);

        [DllImport(AquaUnityPath)]
        static public extern void SetLodRefinementParameters(ClientHandle client, ref LodRefinementParameters refinementParameters);

        [DllImport(AquaUnityPath)]
        static public extern int GetCameraCount(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern void GetCameraIds(ClientHandle client, int[] cameraIndices);

        // ---------------------------------------------------------------
        // Scene Object API
        // --------------------------------------------------------------- 

        [DllImport(AquaUnityPath)]
        static public extern int GetSceneRootObjectId(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern void PrintSceneObjectHierarchy(ClientHandle client, int sceneObjectId);

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
        static public extern void GetAttribute(ClientHandle client, int sceneObjectId, string attributeName, ref AttributeInfo attributeInfo);

        [DllImport(AquaUnityPath)]
        static public extern void GetMosaicDescriptors(Int64 mosaicDescriptorPtr, MosaicDescriptorInfo[] mosaicDescriptor);

        [DllImport(AquaUnityPath)]
        static public extern IntPtr GetRenderEventCallbackPtr();

        [DllImport(AquaUnityPath)]
        static public extern void GetTransform(ClientHandle client, int sceneObjectId, float[] transformData);

        [DllImport(AquaUnityPath)]
        static public extern void GetMetadata(ClientHandle client, int sceneObjectId, ref AssetMetadata metadata);

        [DllImport(AquaUnityPath)]
        static public extern void GetBoundingBox(ClientHandle client, int sceneObjectId, float[] boundingBox);

        [DllImport(AquaUnityPath)]
        static public extern int RecordFrameInfo(ClientHandle client, ref FrameInfo FrameInfo);

        [DllImport(AquaUnityPath)]
        static public extern int GetLodIndex(ClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static public extern void GetLodMinMaxIndices(ClientHandle client, ref int minLodIndex, ref int maxLodIndex);

        [DllImport(AquaUnityPath)]
        static public extern int GetSceneOperatorCount(ClientHandle client);

        [DllImport(AquaUnityPath)]
        static public extern void GetSceneMetadata(ClientHandle client, ref SceneMetadata metadata);

        [DllImport(AquaUnityPath)]
        static public extern void MarkAttributeArrayAccessed(uint hash0, uint hash1, uint hash2, uint hash3);

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
