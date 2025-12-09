// Copyright © 2025 Miris, Inc. All rights reserved.

using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Miris.Runtime
{
    using ClientHandle = IntPtr;

    /// <summary>
    /// Client API Object used by our C# code
    /// </summary>
    public class Client : IDisposable
    {
#if UNITY_EDITOR
        public static string DoNotWarnAgainPrefsKey = "MirisClient_DoNotWarnAgain";
#endif

        private ClientHandle m_handle = System.IntPtr.Zero;

        public Client()
        {
            MirisDebug.Log("Creating Client");
            try
            {
                m_handle = MirisApi.CreateClient();
            }
            catch (DllNotFoundException ex)
            {
                Debug.LogError($"Failed to create Client: Native library not found. {ex.Message}");
                m_handle = System.IntPtr.Zero;

#if UNITY_EDITOR
                bool ignoreWarning = EditorPrefs.GetBool(DoNotWarnAgainPrefsKey, false);
                if (ignoreWarning)
                {
                    return;
                }

                int option = EditorUtility.DisplayDialogComplex(
                    "Miris SDK - Native Library Missing", 
                    #if MIRIS_INTERNAL
                    "The Miris native library was not found - the Miris SDK will not function until it is installed or built.\n\n" +
                    "Contact our DevOps team if you have trouble building the native library.\n\n" +
                    "You can download the native library using the Miris Release Downloader.\n\n" +
                    #else
                    "The Miris native library was not found - the Miris SDK will not function until it is installed.\n\nYou can download the native library using the Miris Release Downloader.\n\n" +
                    #endif
                    "Details: " + ex.Message,
                    "OK",
                    "Open Release Downloader",
                    "Do not show again");
                
                switch (option)
                {
                    // OK
                    case 0:
                        // Do nothing
                        break;
                    // "Open Release Downloader"
                    case 1:
                        UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Miris/Platform Downloader");
                        break;
                    // "Do not show again"
                    case 2:
                        EditorPrefs.SetBool(DoNotWarnAgainPrefsKey, true);
                        break;
                    default:
                        break;
                }
#endif
                throw;
            }
        }

        public void Dispose()
        {
            if (m_handle != System.IntPtr.Zero)
            {
                MirisDebug.Log("Destroying AquaClient");
                MirisApi.DestroyClient(m_handle);
                m_handle = System.IntPtr.Zero;
            }
        }

        public void RecordFrameInfo(int splatCount)
        {
            FrameInfo frameInfo = new FrameInfo
            {
                m_deltaTimeSeconds = Time.deltaTime,
                m_frameCount = Time.frameCount,
                m_splatCount = splatCount
            };

            MirisApi.RecordFrameInfo(m_handle, ref frameInfo);
        }

        public void SetLodRefinementParameters(LodRefinementParameters refinementParameters)
        {
            MirisApi.SetLodRefinementParameters(m_handle, ref refinementParameters);
        }

        public void performThroughputTest(string payload, string deviceId)
        {
            MirisApi.PerformThroughputTest(m_handle, payload, deviceId);
        }

        /// <summary>
        /// Tells the C API where the USD path and plugins are stored.
        /// See <see cref="StartupLoader"/>
        /// </summary>
        /// <param name="payload">The USD path</param>
        public void SetUsdPath(string payload)
        {
            MirisApi.SetUsdPath(m_handle, payload);
        }

        /// <summary>
        /// Gives the SDK a path to a directory to which it can write persistent data.
        /// </summary>
        /// <param name="dirPath">Path to a writable directory</param>
        /// <returns>true if the directory was deemed as writable, false otherwise</returns>
        public bool SetPersistentDataDirectory(string dirPath)
        {
            return MirisApi.SetPersistentDataDirectory(m_handle, dirPath);
        }

        /// <summary>
        /// Runs a throughput test. For Miris internal use.
        /// </summary>
        /// <param name="payload">A JSON object, for configuring the test</param>
        /// <param name="deviceId">A unique identifier for the client device. Usually <see cref="SystemInfo.deviceUniqueIdentifier"/></param>
        public void PerformThroughputTest(string payload, string deviceId)
        {
            MirisApi.PerformThroughputTest(m_handle, payload, deviceId);
        }

        /// <summary>
        /// Set the key that the SDK will include with every request relevant to fetching assets.
        /// This function should be called once before <see cref="GetAssets"/> is called.
        /// </summary>
        /// <param name="key">The SDK key</param>
        public void SetAssetViewerKey(string key)
        {
            MirisApi.SetAssetViewerKey(m_handle, key);
        }

        /// <summary>
        /// Retrieve all available assets from the server environment.
        /// </summary>
        /// <param name="tags">An optional array of tags for filtering the assets that are retrieved.
        /// The tags are combined using the AND operator, i.e. they are exclusive filters.</param>
        /// <param name="tagsCount">The number of tags</param>
        /// <param name="callback">This callback will be invoked and supplied with the scene data when it is ready</param>
        /// <param name="userData"></param>
        public void GetAssets(IntPtr tags, int tagsCount, FillNativeArrayCallback callback, IntPtr userData)
        {
            MirisApi.GetAssets(m_handle, tags, tagsCount, callback, userData);
        }

        /// <summary>
        /// Get all available server environment names.
        /// </summary>
        /// <param name="callback">Callback for handling retrieved server environment names</param>
        /// <param name="userData"></param>
        public void GetAvailableEnvironments(FillNativeArrayCallback callback, IntPtr userData)
        {
            MirisApi.GetAvailableEnvironments(m_handle, callback, userData);
        }

        public void GetAvailableTags(FillNativeArrayCallback callback, IntPtr userData)
        {
            MirisApi.GetAvailableTags(m_handle, callback, userData);
        }

        public IntPtr GetDefaultEnvironment()
        {
            return MirisApi.GetDefaultEnvironment(m_handle);
        }

        public void SetServerEnvironment(string environment, SetServerEnvironmentCallback callback, IntPtr userData)
        {
            MirisApi.SetServerEnvironment(m_handle, environment, callback, userData);
        }

        public void PrefetchContent(string url)
        {
            MirisApi.PrefetchContent(m_handle, url);
        }

        public void ClearScene()
        {
            Debug.Assert(m_handle != IntPtr.Zero, "ClearScene: m_handle handle is invalid!");
            MirisApi.ClearScene(m_handle);
        }

        public int AddStream(string streamName, string contentUrl, int clientType, bool doNotRefine)
        {
            return MirisApi.AddStream(m_handle, streamName, contentUrl, clientType, doNotRefine);
        }

        public int AddStreamById(string streamName, string assetId, int clientType, bool doNotRefine, AddStreamCallback callback, IntPtr userData)
        {
            return MirisApi.AddStreamById(m_handle, streamName, assetId, clientType, doNotRefine, callback, userData);
        }

        public bool RemoveStream(int streamObjectId)
        {
            Debug.Assert(m_handle != IntPtr.Zero, "RemoveStream: m_handle handle is invalid!");
            return MirisApi.RemoveStream(m_handle, streamObjectId);
        }

        public void UpdateSceneExecution()
        {
            MirisApi.UpdateSceneExecution(m_handle);
        }

        public void WaitForSceneExecution()
        {
            MirisApi.WaitForSceneExecution(m_handle);
        }

        public void CancelAllSceneExecution()
        {
            MirisApi.CancelAllSceneExecution(m_handle);
        }

        public bool LockScene()
        {
            return MirisApi.LockScene(m_handle);
        }

        public void UnlockScene()
        {
            MirisApi.UnlockScene(m_handle);
        }

        public int GetSceneChangesCounts(ref SceneChangeIds sceneChangeIds)
        {
            return MirisApi.GetSceneChangesCounts(m_handle, ref sceneChangeIds);
        }

        public int GetSceneChanges(ref SceneChangeIds sceneChangeIds)
        {
            return MirisApi.GetSceneChanges(m_handle, ref sceneChangeIds);
        }

        public void SetMainCameraTransform(float[] transform)
        {
            MirisApi.SetMainCameraTransform(m_handle, transform);
        }

        public void SetMainCameraViewFrustum(float aspectRatio, float verticalFov, float nearPlane, float farPlane)
        {
            MirisApi.SetMainCameraViewFrustum(m_handle, aspectRatio, verticalFov, nearPlane, farPlane);
        }

        public void SetSceneObjectTransform(int sceneObjectId, float[] transform)
        {
            MirisApi.SetSceneObjectTransform(m_handle, sceneObjectId, transform);
        }

        public void SetLodRefinementParameters(ref LodRefinementParameters refinementParameters)
        {
            MirisApi.SetLodRefinementParameters(m_handle, ref refinementParameters);
        }


        public int GetCameraCount()
        {
            return MirisApi.GetCameraCount(m_handle);
        }

        public void GetCameraIds(int[] cameraIndices)
        {
            MirisApi.GetCameraIds(m_handle, cameraIndices);
        }

        public int GetSceneRootObjectId()
        {
            return MirisApi.GetSceneRootObjectId(m_handle);
        }

        public void PrintSceneObjectHierarchy(int sceneObjectId)
        {
            MirisApi.PrintSceneObjectHierarchy(m_handle, sceneObjectId);
        }

        public int GetSceneObjectType(int sceneObjectId)
        {
            Debug.Assert(m_handle != IntPtr.Zero, "GetSceneObjectType: m_handle handle is invalid!");
            return MirisApi.GetSceneObjectType(m_handle, sceneObjectId);
        }

        public int GetSceneObjectParent(int sceneObjectId)
        {
            return MirisApi.GetSceneObjectParent(m_handle, sceneObjectId);
        }

        public bool IsSceneObjectAncestorOf(int sceneObjectId, int descendantObjectId)
        {
            return MirisApi.IsSceneObjectAncestorOf(m_handle, sceneObjectId, descendantObjectId);
        }

        public IntPtr GetSceneObjectName(int sceneObjectId)
        {
            return MirisApi.GetSceneObjectName(m_handle, sceneObjectId);
        }

        public int GetAttributeCount(int sceneObjectId)
        {
            return MirisApi.GetAttributeCount(m_handle, sceneObjectId);
        }

        public bool HasAttribute(int sceneObjectId, string attributeName)
        {
            return MirisApi.HasAttribute(m_handle, sceneObjectId, attributeName);
        }

        public void GetAttribute(int sceneObjectId, string attributeName, ref AttributeInfo attributeInfo)
        {
            MirisApi.GetAttribute(m_handle, sceneObjectId, attributeName, ref attributeInfo);
        }

        public void GetTransform(int sceneObjectId, float[] transformData)
        {
            MirisApi.GetTransform(m_handle, sceneObjectId, transformData);
        }

        public void GetMetadata(int sceneObjectId, ref AssetMetadata metadata)
        {
            MirisApi.GetMetadata(m_handle, sceneObjectId, ref metadata);
        }

        public void GetBoundingBox(int sceneObjectId, float[] boundingBox)
        {
            MirisApi.GetBoundingBox(m_handle, sceneObjectId, boundingBox);
        }

        public int RecordFrameInfo(ref FrameInfo frameInfo)
        {
            return MirisApi.RecordFrameInfo(m_handle, ref frameInfo);
        }

        public int GetLodIndex(int sceneObjectId)
        {
            return MirisApi.GetLodIndex(m_handle, sceneObjectId);
        }

        public void GetLodMinMaxIndices(ref int minLodIndex, ref int maxLodIndex)
        {
            MirisApi.GetLodMinMaxIndices(m_handle, ref minLodIndex, ref maxLodIndex);
        }

        public int GetSceneOperatorCount()
        {
            return MirisApi.GetSceneOperatorCount(m_handle);
        }

        public void GetSceneMetadata(ref SceneMetadata metadata)
        {
            MirisApi.GetSceneMetadata(m_handle, ref metadata);
        }

        public void GetTimelineConfig(ref TimelineConfig config)
        {
            MirisApi.GetTimelineConfig(m_handle, ref config);
        }

        public void SetTimelineConfig(ref TimelineConfig config)
        {
            MirisApi.SetTimelineConfig(m_handle, ref config);
        }

        public void GetTime(ref Timecode timecode)
        {
            MirisApi.GetTime(m_handle, ref timecode);
        }

        public void GetTimeRange(ref Timecode startTime, ref Timecode endTime)
        {
            MirisApi.GetTimeRange(m_handle, ref startTime, ref endTime);
        }

        public void AdvanceTime(float hostTimeDelta)
        {
            MirisApi.AdvanceTime(m_handle, hostTimeDelta);
        }

        public void SeekToTime(ref Timecode newTime)
        {
            MirisApi.SeekToTime(m_handle, ref newTime);
        }
    }
}
