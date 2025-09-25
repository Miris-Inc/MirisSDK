// Copyright © 2024 Miris. All rights reserved.

// C# Standard library
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AOT;

// Unity packages
using UnityEngine;

// The functionality in this file is subject to change as the scene API evolves.

namespace Aqua.Runtime
{
    using AquaClientHandle = IntPtr;

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
    /// See $AQUA_ROOT/modules/AquaUnity/include/AquaUnity/AquaSceneC.h
    /// for the corresponding C API.
    /// </summary>
    public class AquaUnityApi : IDisposable
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

        #region Private declarations of the non-global portion of the C API

        // Enforce singleton behavior of AquaUnityApi while still allowing the C API to be an instantiated object with a concrete lifetime
        static private AquaUnityApi s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static private void Initialize()
        {
            Application.quitting += Destroy;

            s_instance = new AquaUnityApi();
        }

        static private void Destroy()
        {
            s_instance.Dispose();
            s_instance = null;
        }

        private AquaClientHandle m_client = System.IntPtr.Zero;

        private AquaUnityApi()
        {
            m_client = CreateClient();
        }

        public void Dispose()
        {
            if (m_client != System.IntPtr.Zero)
            {
                DestroyClient(m_client);
                m_client = System.IntPtr.Zero;
            }
        }

        ~AquaUnityApi()
        {
            Dispose();
        }

        [DllImport(AquaUnityPath)]
        static private extern AquaClientHandle CreateClient();

        [DllImport(AquaUnityPath)]
        static private extern void DestroyClient(AquaClientHandle client);

        [DllImport(AquaUnityPath)]
        static private extern void SetUsdPath(AquaClientHandle client, string payload);

        [DllImport(AquaUnityPath)]
        static private extern bool SetPersistentDataDirectory(AquaClientHandle client, string dirPath);

        [DllImport(AquaUnityPath)]
        static private extern void PerformThroughputTest(AquaClientHandle client, string payload, string deviceId);

        // ---------------------------------------------------------------
        // Asset Management API
        // ---------------------------------------------------------------

        [DllImport(AquaUnityPath)]
        static private extern void SetAssetViewerKey(AquaClientHandle client, string key);

        [DllImport(AquaUnityPath)]
        static private extern void GetAssets(AquaClientHandle client, IntPtr tags, int tagsCount, FillNativeArrayCallback callback, IntPtr userData);

        [DllImport(AquaUnityPath)]
        static private extern void GetAvailableEnvironments(AquaClientHandle client, FillNativeArrayCallback callback, IntPtr userData);

        [DllImport(AquaUnityPath)]
        static private extern IntPtr GetDefaultEnvironment(AquaClientHandle client);

        [DllImport(AquaUnityPath)]
        static private extern void SetServerEnvironment(AquaClientHandle client, string environment, SetServerEnvironmentCallback callback, IntPtr userData);

        // ---------------------------------------------------------------
        // Utility API
        // ---------------------------------------------------------------

        [DllImport(AquaUnityPath)]
        static private extern void GetImageFromUrl(AquaClientHandle client, string url, GetImagePixelBufferCallback callback, IntPtr userData);

        [DllImport(AquaUnityPath)]
        static public extern ulong PrefetchContent(AquaClientHandle client, string url);

        // ---------------------------------------------------------------
        // Scene API
        // --------------------------------------------------------------- 

        [DllImport(AquaUnityPath)]
        static private extern void ClearScene(AquaClientHandle client);

        [DllImport(AquaUnityPath)]
        static private extern int AddStream(AquaClientHandle client, string streamName, string contentUrl, int clientType);

        [DllImport(AquaUnityPath)]
        static private extern bool RemoveStream(AquaClientHandle client, int streamObjectId);

        [DllImport(AquaUnityPath)]
        static private extern void UpdateSceneExecution(AquaClientHandle client);

        [DllImport(AquaUnityPath)]
        static private extern void WaitForSceneExecution(AquaClientHandle client);

        [DllImport(AquaUnityPath)]
        static private extern void CancelAllSceneExecution(AquaClientHandle client);

        [DllImport(AquaUnityPath)]
        static private extern bool LockScene(AquaClientHandle client);

        [DllImport(AquaUnityPath)]
        static private extern void UnlockScene(AquaClientHandle client);

        [DllImport(AquaUnityPath)]
        static private extern int GetSceneChangesCounts(AquaClientHandle client,
            ref SceneChangeIds sceneChangeIds
        );

        [DllImport(AquaUnityPath)]
        static private extern int GetSceneChanges(AquaClientHandle client, 
            ref SceneChangeIds sceneChangeIds
        );

        [DllImport(AquaUnityPath)]
        static private extern void SetMainCameraTransform(AquaClientHandle client, float[] transform);

        [DllImport(AquaUnityPath)]
        static private extern void SetMainCameraViewFrustum(AquaClientHandle client, float aspectRatio, float verticalFov, float nearPlane, float farPlane);

        [DllImport(AquaUnityPath)]
        static private extern void SetSceneObjectTransform(AquaClientHandle client, int sceneObjectId, float[] transform);

        [DllImport(AquaUnityPath)]
        static private extern void SetXRFloorHeight(AquaClientHandle client, float xrFloorHeight);

        [DllImport(AquaUnityPath)]
        static private extern void SetLodRefinementParameters(AquaClientHandle client, ref LodRefinementParameters refinementParameters);

        // ---------------------------------------------------------------
        // Scene Object API
        // --------------------------------------------------------------- 

        [DllImport(AquaUnityPath)]
        static private extern int GetSceneRootObjectId(AquaClientHandle client);

        [DllImport(AquaUnityPath)]
        static private extern void PrintSceneObjectHierarchy(AquaClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static private extern int GetSceneObjectType(AquaClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static private extern int GetSceneObjectParent(AquaClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static private extern IntPtr GetSceneObjectName(AquaClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static private extern int GetAttributeCount(AquaClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static private extern bool HasAttribute(AquaClientHandle client, int sceneObjectId, string attributeName);

        [DllImport(AquaUnityPath)]
        static private extern void GetAttribute(AquaClientHandle client, int sceneObjectId, string attributeName, ref AttributeInfo attributeInfo);

        [DllImport(AquaUnityPath)]
        static public extern void GetMosaicDescriptors(Int64 mosaicDescriptorPtr, MosaicDescriptorInfo[] mosaicDescriptor);

        [DllImport(AquaUnityPath)]
        static public extern IntPtr GetRenderEventCallbackPtr();

        [DllImport(AquaUnityPath)]
        static private extern void GetTransform(AquaClientHandle client, int sceneObjectId, float[] transformData);

        [DllImport(AquaUnityPath)]
        static private extern void GetMetadata(AquaClientHandle client, int sceneObjectId, ref AssetMetadata metadata);

        [DllImport(AquaUnityPath)]
        static private extern void GetBoundingBox(AquaClientHandle client, int sceneObjectId, float[] boundingBox);

        [DllImport(AquaUnityPath)]
        static private extern int RecordFrameInfo(AquaClientHandle client, ref FrameInfo FrameInfo);

        [DllImport(AquaUnityPath)]
        static private extern int GetLodIndex(AquaClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static private extern void GetLodMinMaxIndices(AquaClientHandle client, ref int minLodIndex, ref int maxLodIndex);

        [DllImport(AquaUnityPath)]
        static private extern int GetSceneOperatorCount(AquaClientHandle client);

        [DllImport(AquaUnityPath)]
        static private extern void GetSceneMetadata(AquaClientHandle client, ref SceneMetadata metadata);

        // ---------------------------------------------------------------
        // Teleport Area Object API
        // --------------------------------------------------------------- 

        [DllImport(AquaUnityPath)]
        static private extern void GetTeleportAreaData(AquaClientHandle client, int sceneObjectId, float[] vertexData, int[] triangleData);

        [DllImport(AquaUnityPath)]
        static private extern void GetTeleportAreaDataSizes(AquaClientHandle client, int sceneObjectId, ref int vertexCount, ref int triangleCount);


        // ---------------------------------------------------------------
        // Camera Object API
        // --------------------------------------------------------------- 

        [DllImport(AquaUnityPath)]
        static private extern float GetFieldOfView(AquaClientHandle client, int sceneObjectId);

        [DllImport(AquaUnityPath)]
        static private extern void PlotMetric(AquaClientHandle client, string name, Int64 value);

        // ---------------------------------------------------------------
        // Timeline API
        // ---------------------------------------------------------------

        [DllImport(AquaUnityPath)]
        static private extern void GetTimelineConfig(AquaClientHandle client, ref TimelineConfig config);

        [DllImport(AquaUnityPath)]
        static private extern void SetTimelineConfig(AquaClientHandle client, ref TimelineConfig config);

        [DllImport(AquaUnityPath)]
        static private extern void GetTime(AquaClientHandle client, ref Timecode timecode);

        [DllImport(AquaUnityPath)]
        static private extern void GetTimeRange(AquaClientHandle client, ref Timecode startTime, ref Timecode endTime);

        [DllImport(AquaUnityPath)]
        static private extern void AdvanceTime(AquaClientHandle client, float hostTimeDelta);

        [DllImport(AquaUnityPath)]
        static private extern void SeekToTime(AquaClientHandle client, ref Timecode newTime);

        #endregion

        #region Public calls that forward to the private C API

        /// <summary>
        /// Tells the C API where the USD path and plugins are stored.
        /// See <see cref="StartupLoader"/>
        /// </summary>
        /// <param name="payload">The USD path</param>
        static public void SetUsdPath(string payload)
        {
            SetUsdPath(s_instance.m_client, payload);
        }

        /// <summary>
        /// Gives the SDK a path to a directory to which it can write persistent data.
        /// </summary>
        /// <param name="dirPath">Path to a writable directory</param>
        /// <returns>true if the directory was deemed as writable, false otherwise</returns>
        static public bool SetPersistentDataDirectory(string dirPath)
        {
            return SetPersistentDataDirectory(s_instance.m_client, dirPath);
        }

        /// <summary>
        /// Runs a throughput test. For Miris internal use.
        /// </summary>
        /// <param name="payload">A JSON object, for configuring the test</param>
        /// <param name="deviceId">A unique identifier for the client device. Usually <see cref="SystemInfo.deviceUniqueIdentifier"/></param>
        static public void PerformThroughputTest(string payload, string deviceId)
        {
            PerformThroughputTest(s_instance.m_client, payload, deviceId);
        }

        /// <summary>
        /// Set the key that the SDK will include with every request relevant to fetching assets.
        /// This function should be called once before <see cref="GetAssets"/> is called.
        /// </summary>
        /// <param name="key">The SDK key</param>
        static public void SetAssetViewerKey(string key)
        {
            SetAssetViewerKey(s_instance.m_client, key);
        }

        /// <summary>
        /// Retrieve all available assets from the server environment.
        /// </summary>
        /// <param name="tags">An optional array of tags for filtering the assets that are retrieved.
        /// The tags are combined using the AND operator, i.e. they are exclusive filters.</param>
        /// <param name="tagsCount">The number of tags</param>
        /// <param name="callback">This callback will be invoked and supplied with the scene data when it is ready</param>
        /// <param name="userData"></param>
        static public void GetAssets(IntPtr tags, int tagsCount, FillNativeArrayCallback callback, IntPtr userData)
        {
            GetAssets(s_instance.m_client, tags, tagsCount, callback, userData);
        }

        /// <summary>
        /// Get all available server environment names.
        /// </summary>
        /// <param name="callback">Callback for handling retrieved server environment names</param>
        /// <param name="userData"></param>
        static public void GetAvailableEnvironments(FillNativeArrayCallback callback, IntPtr userData)
        {
            GetAvailableEnvironments(s_instance.m_client, callback, userData);
        }

        static public IntPtr GetDefaultEnvironment()
        {
            return GetDefaultEnvironment(s_instance.m_client);
        }

        static public void SetServerEnvironment(string environment, SetServerEnvironmentCallback callback, IntPtr userData)
        {
            SetServerEnvironment(s_instance.m_client, environment, callback, userData);
        }

        static public void GetImageFromUrl(string url, GetImagePixelBufferCallback callback, IntPtr userData)
        {
            GetImageFromUrl(s_instance.m_client, url, callback, userData);
        }

        static public void PrefetchContent(string url)
        {
            PrefetchContent(s_instance.m_client, url);
        }

        static public void ClearScene()
        {
            ClearScene(s_instance.m_client);
        }

        static public int AddStream(string streamName, string contentUrl, int clientType)
        {
            return AddStream(s_instance.m_client, streamName, contentUrl, clientType);
        }

        static public bool RemoveStream(int streamObjectId)
        {
            return RemoveStream(s_instance.m_client, streamObjectId);
        }

        static public void UpdateSceneExecution()
        {
            UpdateSceneExecution(s_instance.m_client);
        }

        static public void WaitForSceneExecution()
        {
            WaitForSceneExecution(s_instance.m_client);
        }

        static public void CancelAllSceneExecution()
        {
            CancelAllSceneExecution(s_instance.m_client);
        }

        static public bool LockScene()
        {
            return LockScene(s_instance.m_client);
        }

        static public void UnlockScene()
        {
            UnlockScene(s_instance.m_client);
        }

        static public int GetSceneChangesCounts(ref SceneChangeIds sceneChangeIds)
        {
            return GetSceneChangesCounts(s_instance.m_client, ref sceneChangeIds);
        }

        static public int GetSceneChanges(ref SceneChangeIds sceneChangeIds)
        {
            return GetSceneChanges(s_instance.m_client, ref sceneChangeIds);
        }

        static public void SetMainCameraTransform(float[] transform)
        {
            SetMainCameraTransform(s_instance.m_client, transform);
        }

        static public void SetMainCameraViewFrustum(float aspectRatio, float verticalFov, float nearPlane, float farPlane)
        {
            SetMainCameraViewFrustum(s_instance.m_client, aspectRatio, verticalFov, nearPlane, farPlane);
        }

        static public void SetSceneObjectTransform(int sceneObjectId, float[] transform)
        {
            SetSceneObjectTransform(s_instance.m_client, sceneObjectId, transform);
        }

        static public void SetXRFloorHeight(float xrFloorHeight)
        {
            SetXRFloorHeight(s_instance.m_client, xrFloorHeight);
        }

        static public void SetLodRefinementParameters(ref LodRefinementParameters refinementParameters)
        {
            SetLodRefinementParameters(s_instance.m_client, ref refinementParameters);
        }

        static public int GetSceneRootObjectId()
        {
            return GetSceneRootObjectId(s_instance.m_client);
        }

        static public void PrintSceneObjectHierarchy(int sceneObjectId)
        {
            PrintSceneObjectHierarchy(s_instance.m_client, sceneObjectId);
        }

        static public int GetSceneObjectType(int sceneObjectId)
        {
            return GetSceneObjectType(s_instance.m_client, sceneObjectId);
        }

        static public int GetSceneObjectParent(int sceneObjectId)
        {
            return GetSceneObjectParent(s_instance.m_client, sceneObjectId);
        }

        static public IntPtr GetSceneObjectName(int sceneObjectId)
        {
            return GetSceneObjectName(s_instance.m_client, sceneObjectId);
        }

        static public int GetAttributeCount(int sceneObjectId)
        {
            return GetAttributeCount(s_instance.m_client, sceneObjectId);
        }

        static public bool HasAttribute(int sceneObjectId, string attributeName)
        {
            return HasAttribute(s_instance.m_client, sceneObjectId, attributeName);
        }

        static public void GetAttribute(int sceneObjectId, string attributeName, ref AttributeInfo attributeInfo)
        {
            GetAttribute(s_instance.m_client, sceneObjectId, attributeName, ref attributeInfo);
        }

        static public void GetTransform(int sceneObjectId, float[] transformData)
        {
            GetTransform(s_instance.m_client, sceneObjectId, transformData);
        }

        static public void GetMetadata(int sceneObjectId, ref AssetMetadata metadata)
        {
            GetMetadata(s_instance.m_client, sceneObjectId, ref metadata);
        }

        static public void GetBoundingBox(int sceneObjectId, float[] boundingBox)
        {
            GetBoundingBox(s_instance.m_client, sceneObjectId, boundingBox);
        }

        static public int RecordFrameInfo(ref FrameInfo frameInfo)
        {
            return RecordFrameInfo(s_instance.m_client, ref frameInfo);
        }

        static public int GetLodIndex(int sceneObjectId)
        {
            return GetLodIndex(s_instance.m_client, sceneObjectId);
        }

        static public void GetLodMinMaxIndices(ref int minLodIndex, ref int maxLodIndex)
        {
            GetLodMinMaxIndices(s_instance.m_client, ref minLodIndex, ref maxLodIndex);
        }

        static public int GetSceneOperatorCount()
        {
            return GetSceneOperatorCount(s_instance.m_client);
        }

        static public void GetSceneMetadata(ref SceneMetadata metadata)
        {
            GetSceneMetadata(s_instance.m_client, ref metadata);
        }

        static public void GetTeleportAreaData(int sceneObjectId, float[] vertexData, int[] triangleData)
        {
            GetTeleportAreaData(s_instance.m_client, sceneObjectId, vertexData, triangleData);
        }

        static public void GetTeleportAreaDataSizes(int sceneObjectId, ref int vertexCount, ref int triangleCount)
        {
            GetTeleportAreaDataSizes(s_instance.m_client, sceneObjectId, ref vertexCount, ref triangleCount);
        }

        static public float GetFieldOfView(int sceneObjectId)
        {
            return GetFieldOfView(s_instance.m_client, sceneObjectId);
        }

        static public void PlotMetric(string name, Int64 value)
        {
            PlotMetric(s_instance.m_client, name, value);
        }

        static public void GetTimelineConfig(ref TimelineConfig config)
        {
            GetTimelineConfig(s_instance.m_client, ref config);
        }

        static public void SetTimelineConfig(ref TimelineConfig config)
        {
            SetTimelineConfig(s_instance.m_client, ref config);
        }

        static public void GetTime(ref Timecode timecode)
        {
            GetTime(s_instance.m_client, ref timecode);
        }

        static public void GetTimeRange(ref Timecode startTime, ref Timecode endTime)
        {
            GetTimeRange(s_instance.m_client, ref startTime, ref endTime);
        }

        static public void AdvanceTime(float hostTimeDelta)
        {
            AdvanceTime(s_instance.m_client, hostTimeDelta);
        }

        static public void SeekToTime(ref Timecode newTime)
        {
            SeekToTime(s_instance.m_client, ref newTime);
        }

        #endregion
    }


    public class AquaClient
    {
        static public void RecordFrameInfo(int splatCount)
        {
            FrameInfo frameInfo = new FrameInfo
            {
                m_deltaTimeSeconds = Time.deltaTime,
                m_frameCount = Time.frameCount,
                m_splatCount = splatCount
            };

            AquaUnityApi.RecordFrameInfo(ref frameInfo);
        }

        static public void SetLodRefinementParameters(LodRefinementParameters refinementParameters)
        {
            AquaUnityApi.SetLodRefinementParameters(ref refinementParameters);
        }

        static public void performThroughputTest(string payload, string deviceId)
        {
            AquaUnityApi.PerformThroughputTest(payload, deviceId);
        }

    }

    public class AquaAssetManager
    {
        private static string s_selectedEnvironment = GetDefaultEnvironment();
        public static string SelectedEnvironment => s_selectedEnvironment;

        [MonoPInvokeCallback(typeof(FillNativeArrayCallback))]
        private static void GetAssetsCallback(IntPtr ptr, int count, IntPtr userData)
        {
            InteropUtils.NativeAsyncCallbackArray<AssetInfo>(ptr, count, userData);
        }

        [MonoPInvokeCallback(typeof(FillNativeArrayCallback))]
        private static void GetAvailableEnvironmentsCallback(IntPtr ptr, int count, IntPtr userData)
        {
            InteropUtils.NativeAsyncCallbackStringArray(ptr, count, userData);
        }

        [MonoPInvokeCallback(typeof(SetServerEnvironmentCallback))]
        private static void SetServerEnvironmentCallback(bool success, IntPtr userData)
        {
            var handle = GCHandle.FromIntPtr(userData);
            var tcs = (TaskCompletionSource<bool>)handle.Target;
            handle.Free();
            tcs.SetResult(success);
        }

        public static Task<AssetInfo[]> GetAssets()
        {
            var tcs = new TaskCompletionSource<AssetInfo[]>();

            var handle = GCHandle.Alloc(tcs);
            AquaUnityApi.GetAssets(IntPtr.Zero, 0, GetAssetsCallback, GCHandle.ToIntPtr(handle));

            return tcs.Task;
        }

        public static Task<string[]> GetAvailableEnvironments()
        {
            var tcs = new TaskCompletionSource<string[]>();

            var handle = GCHandle.Alloc(tcs);
            AquaUnityApi.GetAvailableEnvironments(GetAvailableEnvironmentsCallback, GCHandle.ToIntPtr(handle));

            return tcs.Task;
        }

        public static Task<bool> SetServerEnvironment(string environment)
        {
            var tcs = new TaskCompletionSource<bool>();

            var handle = GCHandle.Alloc(tcs);
            AquaUnityApi.SetServerEnvironment(environment, SetServerEnvironmentCallback, GCHandle.ToIntPtr(handle));

            return tcs.Task.ContinueWith(t => {
                if (t.Result) {
                    s_selectedEnvironment = environment;
                }
                return t.Result;
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        public static string GetDefaultEnvironment()
        {
            IntPtr ptr = AquaUnityApi.GetDefaultEnvironment();
            return Marshal.PtrToStringAnsi(ptr);
        }

        public static Task<bool> ResetEnvironmentToDefault()
        {
            return SetServerEnvironment(GetDefaultEnvironment());
        }
    }

    public class AquaImage
    {
        public int width;
        public int height;
        public int bytesPerPixel;
        public byte[] pixels;

        public bool IsValid => width > 0 && height > 0 && pixels?.Length == width * height * bytesPerPixel;

        [MonoPInvokeCallback(typeof(GetImagePixelBufferCallback))]
        private static void NativeAsyncCallback(int width, int height, int bytesPerPixel, System.IntPtr pixelData, System.IntPtr userData)
        {
            var handle = GCHandle.FromIntPtr(userData);
            var tcs = (TaskCompletionSource<AquaImage>)handle.Target;
            handle.Free();

            int totalSize = width * height * bytesPerPixel;
            byte[] data = new byte[totalSize];
            if (totalSize > 0)
            {
                Marshal.Copy(pixelData, data, 0, totalSize);
            }

            tcs?.SetResult(new AquaImage() { width = width, height = height, bytesPerPixel = bytesPerPixel, pixels = data});
        }

        public static Task<AquaImage> FromUrl(string url)
        {
            var tcs = new TaskCompletionSource<AquaImage>();

            if (string.IsNullOrEmpty(url))
            {
                tcs.SetResult(new AquaImage());
            }
            else
            {
                var handle = GCHandle.Alloc(tcs);
                AquaUnityApi.GetImageFromUrl(url, NativeAsyncCallback, GCHandle.ToIntPtr(handle));
            }

            return tcs.Task;
        }

        private AquaImage() {}
    }

    /// <summary>
    /// An RAII object to provide a scope for content to be sync'ed from the aqua scene model
    /// to Unity
    /// </summary>
    public class AquaSceneChangeTracker : IDisposable
    {
        private bool m_sceneLocked;

        public class Changes
        {
            public SceneChangeIds m_changeIds;

            static public Changes FromScene()
            {
                Changes changes = new Changes{m_changeIds = new SceneChangeIds()};
               
                AquaUnityApi.GetSceneChangesCounts(ref changes.m_changeIds);
                changes.m_changeIds.AllocateArrays();
                AquaUnityApi.GetSceneChanges(ref changes.m_changeIds);

                return changes;
            }

        }

        public AquaSceneChangeTracker()
        {
            m_sceneLocked = AquaUnityApi.LockScene();
        }

        public bool IsSceneLocked()
        {
            return m_sceneLocked;
        }

        public Changes GetSceneChanges()
        {
            Debug.Assert(m_sceneLocked);
            return Changes.FromScene();
        }

        public void Dispose()
        {
            if (m_sceneLocked)
            {
                AquaUnityApi.UnlockScene();
            }
        }
    }

    /// <summary>
    /// AquaScene provides access to the C++ Aqua Scene without exposing the native C bindings
    /// </summary>
    public class AquaScene
    {
        public AquaSceneObject AddStream(string streamName, string url)
        {
            int streamObjectId = AquaUnityApi.AddStream(streamName, url, AquaUnityApi.UNITY_CLIENT);
            return GetSceneObject(streamObjectId);
        }

        public bool RemoveStream(AquaSceneObject streamObject)
        {
            return AquaUnityApi.RemoveStream(streamObject.GetId());
        }

        public AquaSceneObject GetRootObject()
        {
            int sceneRootId = AquaUnityApi.GetSceneRootObjectId();
            return GetSceneObject(sceneRootId);
        }

        public AquaSceneObject GetSceneObject(int sceneObjectId)
        {
            return new AquaSceneObject(sceneObjectId);
        }

        public AquaTeleportObject GetTeleportObject(int sceneObjectId)
        {
            return new AquaTeleportObject(sceneObjectId);
        }

        public AquaCamera GetCameraObject(int sceneObjectId)
        {
            return new AquaCamera(sceneObjectId);
        }

        public void SetMainCameraTransform(Matrix4x4 cameraTransform)
        {
            float[] matrixArray = ValueConversion.MatrixToFloatArray(cameraTransform);
            AquaUnityApi.SetMainCameraTransform(matrixArray);
        }

        public void SetMainCameraViewFrustum(Camera camera)
        {
            AquaUnityApi.SetMainCameraViewFrustum(camera.aspect, camera.fieldOfView, camera.nearClipPlane, camera.farClipPlane);
        }

        public void SetXRFloorHeight(float xrFloorHeight)
        {
            AquaUnityApi.SetXRFloorHeight(xrFloorHeight);
        }

        public void Clear()
        {
            AquaUnityApi.ClearScene();
        }

        public void UpdateExecution()
        {
            AquaUnityApi.UpdateSceneExecution();
        }

        public void WaitForExecution()
        {
            AquaUnityApi.WaitForSceneExecution();
        }

        public void GetLodMinMaxIndices(out int minLodIndex, out int maxLodIndex)
        {
            minLodIndex = 0;
            maxLodIndex = 0;
            AquaUnityApi.GetLodMinMaxIndices(ref minLodIndex, ref maxLodIndex);
        }

        public void GetMetadata(out SceneMetadata metadata){
            metadata = new SceneMetadata();
            AquaUnityApi.GetSceneMetadata(ref metadata);
        }

    }

    public class AquaTimeline
    {
        public TimelineConfig GetConfig()
        {
            TimelineConfig config = new();
            AquaUnityApi.GetTimelineConfig(ref config);
            return config;
        }

        public void SetConfig(TimelineConfig config)
        {
            AquaUnityApi.SetTimelineConfig(ref config);
        }

        public void GetTimeRange(out Timecode startTime, out Timecode endTime)
        {
            startTime = new();
            endTime = new();
            AquaUnityApi.GetTimeRange(ref startTime, ref endTime);
        }

        public Timecode GetCurrentTime()
        {
            Timecode currentTime = new();
            AquaUnityApi.GetTime(ref currentTime);
            return currentTime;
        }

        public void AdvanceTime()
        {
            AquaUnityApi.AdvanceTime(Time.deltaTime);
        }

        public void SeekToTime(Timecode newTime)
        {
            AquaUnityApi.SeekToTime(ref newTime);
        }
    }

    /// <summary>
    /// AquaScene provides access to the C++ Aqua Object without exposing the native C bindings 
    /// </summary>
    [System.Serializable]
    public class AquaSceneObject
    {
        // SceneObject handle
        protected int m_sceneObjectId;
        protected const int c_invalidIdOrIndex = -1;

        internal AquaSceneObject(int sceneObjectId)
        {
            m_sceneObjectId = sceneObjectId;
        }

        public void PrintHierarchy()
        {
            AquaUnityApi.PrintSceneObjectHierarchy(m_sceneObjectId);
        }

        public int GetId()
        {
            return m_sceneObjectId;
        }

        public string GetName()
        {
            IntPtr namePtr = AquaUnityApi.GetSceneObjectName(m_sceneObjectId);
            return Marshal.PtrToStringAnsi(namePtr);
        }

        public SceneObjectType GetSceneObjectType()
        {
            int objectTypeInt = AquaUnityApi.GetSceneObjectType(m_sceneObjectId);
            DiagnosticUtils.ValidateEnum<SceneObjectType>(objectTypeInt);
            return (SceneObjectType)objectTypeInt;
        }

        public int GetParentId()
        {
            int parentObjectId = AquaUnityApi.GetSceneObjectParent(m_sceneObjectId);
            return parentObjectId;
        }

        public int GetAttributeCount()
        {
            return AquaUnityApi.GetAttributeCount(m_sceneObjectId);
        }

        public bool HasAttribute(String attributeName)
        {
            return AquaUnityApi.HasAttribute(m_sceneObjectId, attributeName);
        }

        // Wraps a NativeArray around a C void* for direct access into unmanaged memory. 
        // Use with caution!
        // TODO: Aqua will supply the actual format value instead of the caller passing it as an argument.
        unsafe public AttributeInfo GetAttribute(String attributeName)
        {
            AttributeInfo attributeInfo = new();
            AquaUnityApi.GetAttribute(m_sceneObjectId, attributeName, ref attributeInfo);
            return attributeInfo;
        }

        unsafe public Bounds GetBoundingBox()
        {
            float[] boundsData = new float[6];
            AquaUnityApi.GetBoundingBox(m_sceneObjectId, boundsData);
            return new Bounds(
                new Vector3(boundsData[0], boundsData[1], boundsData[2]),
                new Vector3(boundsData[3], boundsData[4], boundsData[5])
            );
        }

        unsafe public Matrix4x4 GetTransform()
        {
            float[] matrixData = new float[16];
            AquaUnityApi.GetTransform(m_sceneObjectId, matrixData);

            return new Matrix4x4(
                new Vector4(matrixData[0], matrixData[1], matrixData[2], matrixData[3]),
                new Vector4(matrixData[4], matrixData[5], matrixData[6], matrixData[7]),
                new Vector4(matrixData[8], matrixData[9], matrixData[10], matrixData[11]),
                new Vector4(matrixData[12], matrixData[13], matrixData[14], matrixData[15])
            );
        }

        public int GetLodIndex()
        {
            return AquaUnityApi.GetLodIndex(m_sceneObjectId);
        }

        public void GetMetadata(out AssetMetadata metadata)
        {
            metadata = new AssetMetadata();
            AquaUnityApi.GetMetadata(m_sceneObjectId, ref metadata);
        }

        public void SetTransform(Matrix4x4 transform)
        {
            float[] matrixArray = ValueConversion.MatrixToFloatArray(transform);
            AquaUnityApi.SetSceneObjectTransform(m_sceneObjectId, matrixArray);
        }
    }

    public class AquaTeleportObject : AquaSceneObject
    {
        internal AquaTeleportObject(int sceneObjectId) : base(sceneObjectId)
        {
            m_sceneObjectId = sceneObjectId;
        }

        unsafe public void GetDataSizes(out int vertexCount, out int triangleCount)
        {
            vertexCount = 0;
            triangleCount = 0;
            AquaUnityApi.GetTeleportAreaDataSizes(m_sceneObjectId, ref vertexCount, ref triangleCount);
        }


        unsafe public void GetData(float[] vertexData, int[] triangleData)
        {
            AquaUnityApi.GetTeleportAreaData(m_sceneObjectId, vertexData, triangleData);
        }
    }

    public class AquaCamera : AquaSceneObject
    {
        internal AquaCamera(int sceneObjectId) : base(sceneObjectId)
        {
            m_sceneObjectId = sceneObjectId;
        }

        unsafe public float GetFieldOfView()
        {
            return AquaUnityApi.GetFieldOfView(m_sceneObjectId);
        }
    }
}
