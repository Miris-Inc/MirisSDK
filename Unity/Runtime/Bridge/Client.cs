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

        /// <summary>
        /// Meant for Miris-internal use.
        /// </summary>
        public ClientHandle? GetClientHandleInternal()
        {
            if (m_handle == System.IntPtr.Zero)
            {
                return null;
            }

            return m_handle;
        }

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
                m_handle = MirisBindings.CreateClient();

                // SpatialFormat is a generated binding type, so it owns native memory and
                // is only needed for the duration of the call.
                using (SpatialFormat spatialFormat = PrepareSpatialFormat())
                {
                    SetClientSpatialFormat(spatialFormat);
                }
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
                MirisBindings.DestroyClient(m_handle);
                m_handle = System.IntPtr.Zero;
            }
        }

        public void RecordFrameTime(double frameTimeMs)
        {
            MirisBindings.RecordFrameTime(m_handle, frameTimeMs);
        }

        public void SetRuntimeSettings(RuntimeSettings runtimeSettings)
        {
            MirisBindings.SetRuntimeSettings(m_handle, ref runtimeSettings);
        }

        /// <summary>
        /// Gives the SDK a path to a directory to which it can write persistent data.
        /// </summary>
        /// <param name="dirPath">Path to a writable directory</param>
        /// <returns>true if the directory was deemed as writable, false otherwise</returns>
        public bool SetPersistentDataDirectory(string dirPath)
        {
            return MirisBindings.SetPersistentDataDirectory(m_handle, dirPath);
        }

        /// <summary>
        /// Set the key that the SDK will include with every request relevant to fetching assets.
        /// This function should be called once before <see cref="GetAssets"/> is called.
        /// </summary>
        /// <param name="key">The SDK key</param>
        public void SetAssetViewerKey(string key)
        {
            MirisBindings.SetAssetViewerKey(m_handle, key);
        }

        /// <summary>
        /// Retrieve all available assets from the server environment (blocking).
        /// </summary>
        /// <param name="tags">A vector of tags for filtering the assets that are retrieved.
        /// The tags are combined using the AND operator, i.e. they are exclusive filters.</param>
        /// <returns>Vector of AssetInfo structs</returns>
        public AssetInfoVector GetAssets(StringVector tags, int limit)
        {
            return MirisBindings.GetAssets(m_handle, tags, limit);
        }

        /// <summary>
        /// Retrieve all available assets from the server environment, together with pagination cursors (blocking).
        /// Pass the cursor from a previous AssetInfoResult (m_nextCursor/m_prevCursor) along with the matching
        /// PageDirection to fetch the next/previous page. Leave both at their defaults for the initial request.
        /// </summary>
        /// <param name="tags">A vector of tags for filtering the assets that are retrieved.
        /// The tags are combined using the AND operator, i.e. they are exclusive filters.</param>
        /// <returns>AssetInfoResult containing the assets and the next/prev pagination cursors</returns>
        public AssetInfoResult GetAssetsPaginatedBlocking(StringVector tags, int limit, string cursor = "",
            PageDirection direction = PageDirection.None)
        {
            return MirisBindings.GetAssetsPaginatedBlocking(m_handle, tags, limit, cursor, direction);
        }

        /// <summary>
        /// Get all unique tags from available assets (blocking).
        /// </summary>
        /// <returns>Vector of tag strings</returns>
        public StringVector GetAvailableTags()
        {
            return MirisBindings.GetAvailableTags(m_handle);
        }

        public void PrefetchContent(string url)
        {
            MirisBindings.PrefetchContent(m_handle, url);
        }

        public void ClearScene()
        {
            Debug.Assert(m_handle != IntPtr.Zero, "ClearScene: m_handle handle is invalid!");
            MirisBindings.ClearScene(m_handle);
        }

        public int AddStream(string streamName, string contentUrl, bool doNotRefine)
        {
            return MirisBindings.AddStream(m_handle, streamName, contentUrl, doNotRefine);
        }

        public int AddStreamById(string streamName, string assetId, bool doNotRefine)
        {
            return MirisBindings.AddStreamById(m_handle, streamName, assetId, doNotRefine);
        }

        public bool RemoveStream(int streamObjectId)
        {
            Debug.Assert(m_handle != IntPtr.Zero, "RemoveStream: m_handle handle is invalid!");
            return MirisBindings.RemoveStream(m_handle, streamObjectId);
        }

        public void UpdateSceneExecution()
        {
            MirisBindings.UpdateSceneExecution(m_handle);
        }

        public void WaitForSceneExecution()
        {
            MirisBindings.WaitForSceneExecution(m_handle);
        }

        public void CancelAllSceneExecution()
        {
            MirisBindings.CancelAllSceneExecution(m_handle);
        }

        public bool LockScene()
        {
            return MirisBindings.LockScene(m_handle);
        }

        public void UnlockScene()
        {
            MirisBindings.UnlockScene(m_handle);
        }

        /// <summary>
        /// Check if a render is required (scene content changed, camera/object transforms updated).
        /// The flag is automatically cleared after this call (read-and-clear semantics).
        /// </summary>
        /// <returns>True if rendering is needed, false otherwise.</returns>
        public bool TakeRenderRequired()
        {
            return MirisBindings.TakeRenderRequired(m_handle);
        }

        public Miris.Runtime.AquaStatus GetSceneChangesCounts(ref SceneChangeIds sceneChangeIds)
        {
            return MirisBindings.GetSceneChangesCounts(m_handle, ref sceneChangeIds);
        }

        public Miris.Runtime.AquaStatus GetSceneChanges(ref SceneChangeIds sceneChangeIds)
        {
            return MirisBindings.GetSceneChanges(m_handle, ref sceneChangeIds);
        }

        public void SetMainCameraTransform(float[] transform)
        {
            MirisBindings.SetMainCameraTransform(m_handle, transform);
        }

        public void SetMainCameraViewFrustum(float aspectRatio, float verticalFov, float nearPlane, float farPlane,
            int viewportHeightPixels)
        {
            MirisBindings.SetMainCameraViewFrustum(m_handle, aspectRatio, verticalFov, nearPlane, farPlane,
                viewportHeightPixels);
        }

        public void SetSceneObjectTransform(int sceneObjectId, float[] transform)
        {
            MirisBindings.SetSceneObjectTransform(m_handle, sceneObjectId, transform);
        }



        public int GetCameraCount()
        {
            return MirisBindings.GetCameraCount(m_handle);
        }

        public void GetCameraIds(int[] cameraIndices)
        {
            MirisBindings.GetCameraIds(m_handle, cameraIndices);
        }

        public int GetSceneRootObjectId()
        {
            return MirisBindings.GetSceneRootObjectId(m_handle);
        }

        public void PrintSceneObjectHierarchy(int sceneObjectId)
        {
            MirisBindings.PrintSceneObjectHierarchy(m_handle, sceneObjectId);
        }

        public int GetSceneObjectType(int sceneObjectId)
        {
            Debug.Assert(m_handle != IntPtr.Zero, "GetSceneObjectType: m_handle handle is invalid!");
            return MirisBindings.GetSceneObjectType(m_handle, sceneObjectId);
        }

        public int GetSceneObjectParent(int sceneObjectId)
        {
            return MirisBindings.GetSceneObjectParent(m_handle, sceneObjectId);
        }

        public bool IsSceneObjectAncestorOf(int sceneObjectId, int descendantObjectId)
        {
            return MirisBindings.IsSceneObjectAncestorOf(m_handle, sceneObjectId, descendantObjectId);
        }

        public IntPtr GetSceneObjectName(int sceneObjectId)
        {
            return MirisBindings.GetSceneObjectName(m_handle, sceneObjectId);
        }

        public int GetAttributeCount(int sceneObjectId)
        {
            return MirisBindings.GetAttributeCount(m_handle, sceneObjectId);
        }

        public bool HasAttribute(int sceneObjectId, string attributeName)
        {
            return MirisBindings.HasAttribute(m_handle, sceneObjectId, attributeName);
        }

        public void GetAttribute(int sceneObjectId, string attributeName, ref AttributeInfo attributeInfo)
        {
            MirisBindings.GetAttribute(m_handle, sceneObjectId, attributeName, ref attributeInfo);
        }

        public void GetTransform(int sceneObjectId, float[] transformData)
        {
            MirisBindings.MirisGetLocalTransform(m_handle, sceneObjectId, transformData);
        }

        public void GetMetadata(int sceneObjectId, AssetMetadata metadata)
        {
            MirisBindings.GetMetadata(m_handle, sceneObjectId, metadata);
        }

        public void GetLocalBoundingBox(int sceneObjectId, float[] boundingBox)
        {
            MirisBindings.GetLocalBoundingBox(m_handle, sceneObjectId, boundingBox);
        }

        public void GetWorldBoundingBox(int sceneObjectId, float[] boundingBox)
        {
             MirisBindings.GetWorldBoundingBox(m_handle, sceneObjectId, boundingBox);
        }

        public int GetLodIndex(int sceneObjectId)
        {
            return MirisBindings.GetLodIndex(m_handle, sceneObjectId);
        }

        public void GetLodMinMaxIndices(out int minLodIndex, out int maxLodIndex)
        {
            MirisBindings.GetLodMinMaxIndices(m_handle, out minLodIndex, out maxLodIndex);
        }

        public int GetSceneOperatorCount()
        {
            return MirisBindings.GetSceneOperatorCount(m_handle);
        }

        public void GetSceneMetadata(SceneMetadata metadata)
        {
            MirisBindings.GetSceneMetadata(m_handle, metadata);
        }
        public void SetClientSpatialFormat(SpatialFormat spatialFormat)
        {
            MirisBindings.SetClientSpatialFormat(m_handle, spatialFormat);
        }
    }
}
