// Copyright © 2026 Miris, Inc. All rights reserved.

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

        /// Unity does not seem to allow the user to modify these global scene settings
        private SpatialFormat PrepareSpatialFormat()
        {
            return new SpatialFormat{
                m_upAxis = UpAxis.Y,
                m_metersPerUnit = 1.0f,
                m_matrixOrder = MatrixOrder.ColumnMajor,
                m_handedness = Handedness.Left
            };
        }

        public Client()
        {
            MirisDebug.Log("Creating Client");
            try
            {
                m_handle = MirisApi.CreateClient();
                SetClientSpatialFormat(PrepareSpatialFormat());
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
                MirisDebug.Log("Destroying MirisClient");
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

        public void SetRuntimeSettings(RuntimeSettings runtimeSettings)
        {
            MirisApi.SetRuntimeSettings(m_handle, ref runtimeSettings);
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
        /// Set the key that the SDK will include with every request relevant to fetching assets.
        /// This function should be called once before <see cref="GetAssets"/> is called.
        /// </summary>
        /// <param name="key">The SDK key</param>
        public void SetAssetViewerKey(string key)
        {
            MirisApi.SetAssetViewerKey(m_handle, key);
        }

        /// <summary>
        /// Retrieve all available assets from the server environment (blocking).
        /// </summary>
        /// <param name="tags">A vector of tags for filtering the assets that are retrieved.
        /// The tags are combined using the AND operator, i.e. they are exclusive filters.</param>
        /// <returns>Vector of AssetInfo structs</returns>
        public AssetInfoVector GetAssets(StringVector tags)
        {
            return MirisBindings.GetAssets(m_handle, tags);
        }

        /// <summary>
        /// Get all available server environments with their URLs (blocking).
        /// </summary>
        /// <returns>Vector of EnvironmentInfo structs containing name and baseUrl</returns>
        public EnvironmentInfoVector GetAvailableEnvironments()
        {
            return MirisBindings.GetAvailableEnvironments(m_handle);
        }

        /// <summary>
        /// Get all unique tags from available assets (blocking).
        /// </summary>
        /// <returns>Vector of tag strings</returns>
        public StringVector GetAvailableTags()
        {
            return MirisBindings.GetAvailableTags(m_handle);
        }

        public IntPtr GetDefaultEnvironment()
        {
            return MirisApi.GetDefaultEnvironment(m_handle);
        }

        public void SetServerEnvironmentAsync(string environment, SetServerEnvironmentCallback callback, IntPtr userData)
        {
            MirisApi.SetServerEnvironmentAsync(m_handle, environment, callback, userData);
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

        public int AddStream(string streamName, string contentUrl, bool doNotRefine)
        {
            return MirisApi.AddStream(m_handle, streamName, contentUrl, doNotRefine);
        }

        public int AddStreamById(string streamName, string assetId, bool doNotRefine)
        {
            return MirisApi.AddStreamById(m_handle, streamName, assetId, doNotRefine);
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

        public Miris.Runtime.AquaStatus GetSceneChangesCounts(ref SceneChangeIds sceneChangeIds)
        {
            return MirisApi.GetSceneChangesCounts(m_handle, ref sceneChangeIds);
        }

        public Miris.Runtime.AquaStatus GetSceneChanges(ref SceneChangeIds sceneChangeIds)
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

        public void SetRuntimeSettings(ref RuntimeSettings runtimeSettings)
        {
            MirisApi.SetRuntimeSettings(m_handle, ref runtimeSettings);
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

        public void GetMetadata(int sceneObjectId, AssetMetadata metadata)
        {
            MirisBindings.GetMetadata(m_handle, sceneObjectId, metadata);
        }

        public void GetBoundingBox(int sceneObjectId, float[] boundingBox)
        {
            MirisApi.GetBoundingBox(m_handle, sceneObjectId, boundingBox);
        }

        public Miris.Runtime.AquaStatus RecordFrameInfo(ref FrameInfo frameInfo)
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

        public void GetSceneMetadata(SceneMetadata metadata)
        {
            MirisBindings.GetSceneMetadata(m_handle, metadata);
        }
        public void SetClientSpatialFormat(SpatialFormat spatialFormat)
        {
            MirisApi.SetClientSpatialFormat(m_handle, spatialFormat);
        }
    }
}
