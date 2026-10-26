// Copyright © 2026 Miris, Inc. All rights reserved.

// Standard library
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// Unity engine
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

// Unity packages
using Unity.Profiling;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Miris.Runtime
{
    /// <summary>
    /// MirisStreamController streams an Scene into the Unity scene graph.
    /// Content will be populated under each of the Miris Streams.
    ///
    /// Created on first use of <see cref="Instance"/> and ticked from the PlayerLoop after
    /// LateUpdate. Edit mode and Play mode each get their own.
    /// </summary>
    public sealed class MirisStreamController
    {
        private static MirisStreamController s_instance;

        private static bool s_shuttingDown;

        // Set when Start throws, so later accesses don't retry it every frame (and, with the native
        // library missing, reopen its modal dialog). Cleared on entering or leaving play mode.
        private static bool s_startFailed;

        /// <summary>
        /// The controller, created on first access. Null while the application is quitting or
        /// leaving play mode, and after the controller failed to start: the access that started it
        /// throws, and later ones return null.
        /// </summary>
        public static MirisStreamController Instance
        {
            get
            {
                if (s_instance == null && !s_shuttingDown && !s_startFailed)
                {
                    s_instance = new MirisStreamController();
                    try
                    {
                        s_instance.Start();
                    }
                    catch
                    {
                        // Otherwise every later access returns the half-started controller.
                        Shutdown();
                        s_startFailed = true;
                        throw;
                    }
                }

                return s_instance;
            }
        }

        public static MirisStreamController Current => s_instance;

        public static bool TryGetActive(out MirisStreamController controller)
        {
            controller = s_instance;
            return controller != null && controller.IsActive();
        }

        internal static void Shutdown()
        {
            if (s_instance == null)
            {
                return;
            }

            MirisStreamController instance = s_instance;
            s_instance = null;
            instance.Stop();
        }

        [NonSerialized]
        public bool m_loadedMetadata = false;

        internal enum ExecutionMode
        {
            Asynchronous = 0,
            Synchronous
        }
        internal ExecutionMode m_executionMode = ExecutionMode.Asynchronous;
        private SceneMetadata m_sceneMetadata;

        internal RuntimeSettings m_runtimeSettings = new RuntimeSettings
        {

            m_targetFramesPerSecond = 90.0f,

            m_splatCountBudget = 400000,
            m_congestionMinInflightBytes = 256 * 1024,
            m_congestionMaxInflightBytes = 128 * 1024 * 1024,
            m_budgetSplitMode = 1,
            m_xrModeActive = false,
            m_prefetchFlushFraction = 0.2f,
            m_prefetchBudgetWeight = 1.0f,
            m_prefetchBudgetMode = 0,
        };
        // Client instance and API helpers
        private Context m_context;
        private Client m_client;
        private Scene m_scene;

        private AssetManager m_assetManager;

        // Tracks the available MirisStreams in the current Unity scene.
        private Dictionary<MirisStream, int> m_streamToSceneObjectId = new();

        // Reverse map to go from a stream's scene object ID to associated MirisStream object
        private Dictionary<int, MirisStream> m_streamObjectIdToMirisStream = new();

        // Associates a miris stream to its constituent assets - note that a stream can load more than 1 asset
        private Dictionary<MirisStream, HashSet<int>> m_streamToModelRootObjectIds = new();

        // Tracks all the splat data sources that belong to an asset root object
        private Dictionary<int, List<MirisAssetDataSource>> m_modelRootObjectIdToDataSources = new();

        // Associates each MirisAsset scene object ID to its data source. 
        private Dictionary<int, MirisAssetDataSource> m_splatObjectIdToDataSource = new Dictionary<int, MirisAssetDataSource>();

        // Miris Stream(s) already have their own GameObject, so we don't need to create
        // them in Populate().  m_streamObjectIds is used for a quick look-up to skip GameObject
        // creation
        private HashSet<int> m_streamObjectIds = new();

        static string s_profilerPrefix = "[MirisStreamController] ";
        static readonly ProfilerMarker s_syncSceneMarker = new ProfilerMarker(
            s_profilerPrefix + "Sync Scene"
        );

        // Lazily update the render components' data sources only when the set of data sources changes.
        private bool m_updateRenderableObjects = false;

        private bool m_warnedNoMainCamera = false;

        internal ClientConfig m_clientConfig;

        // Set by SetViewerKey, and wins over the scene's and project's keys until cleared.
        private string m_runtimeViewerKey;

        private string m_viewerKey = "";

        /// <summary>
        /// Current viewer key in effect. Use <see cref="SetViewerKey"/> to change it — assigning
        /// a field directly would leave the AssetManager out of sync.
        /// </summary>
        public string ViewerKey => m_viewerKey;

        // Why Shark cannot draw this controller's splats, or null when it can. Set by Initialize.
        private string m_sharkUnavailableReason;

        // Latched by a failed bring-up so the restart that follows does not retry it. The next
        // controller - on entering or leaving play mode - tries again.
        private bool m_sharkFailed;

        private bool m_pendingNativeRestart = false;

        public const float MinNativeRenderScale = 0.5f;

        private float m_nativeRenderScale = 1.0f;

        // Requested by SetNativeRenderScale, carried out on the next Tick.
        private float? m_pendingNativeRenderScale;

        /// <summary>
        /// The fraction of the display resolution Shark renders at before
        /// MetalFX upscales it. Lower is cheaper and softer.
        /// </summary>
        public float NativeRenderScale => m_nativeRenderScale;

        private bool m_deviceResolutionViaMetalFx = false;

        /// <summary>
        /// Whether <see cref="NativeRenderScale"/> below 1 targets device resolution via MetalFX,
        /// versus presenting the smaller render directly. Read at native renderer creation, so a
        /// running native renderer restarts to pick up a change.
        /// </summary>
        public bool DeviceResolutionViaMetalFx => m_deviceResolutionViaMetalFx;

        public const float MaxReprojectionOverscan = 1.3f;

        private float m_reprojectionOverscan = 1.0f;

        /// <summary>
        /// How much wider than the display's frustum Shark renders, to
        /// give the reprojection warp real edge pixels on a missed vsync. 1 disables it. Read at
        /// native renderer creation, so a running native renderer restarts to pick up a change.
        /// </summary>
        public float ReprojectionOverscan => m_reprojectionOverscan;

        // Whether Shark draws this controller's splats. Where it cannot, nothing does.
        internal bool IsSharkActive => m_sharkUnavailableReason == null;

        private bool m_surfelMsaaEnabled = false;

        /// <summary>
        /// Whether Shark antialiases the surfel pass's disc edge at 4x.
        /// Not runtime toggleable without restarting the renderer.
        /// </summary>
        public bool SurfelMsaaEnabled => m_surfelMsaaEnabled;

        private float m_outputOpacity = 1.0f;

        /// <summary>
        /// Opacity applied to the composited image of every stream this controller draws, 0 to 1.
        /// Unlike <see cref="MirisStream.Opacity"/>, which each primitive type takes differently -
        /// surfels dither, and native-backend glass only disappears at 0 - this fades the picture
        /// as a whole. It does not demote streams to Prefetch or save any rendering cost.
        /// </summary>
        public float OutputOpacity
        {
            get => m_outputOpacity;
            set => m_outputOpacity = Mathf.Clamp01(value);
        }

        // Requested by SetSurfelMsaaEnabled, carried out on the next Tick.
        private bool? m_pendingSurfelMsaaEnabled;

        private bool m_surfelLessTextureSplatsEnabled = false;

        /// <summary>
        /// Whether Shark renders textured gaussians on pixels no surfel
        /// covers. Takes effect on the next frame.
        /// </summary>
        public bool SurfelLessTextureSplatsEnabled => m_surfelLessTextureSplatsEnabled;

        /// <summary>
        /// Raised whenever the effective viewer key changes, so other packages (e.g. app_kit) can
        /// keep dev UI or saved preferences in sync.
        /// </summary>
        public event Action<string> ViewerKeyChanged;

        /// <summary>
        /// Raised from inside SyncScene's SceneChangeTracker scope, so an alternative renderer can
        /// consume the same changes without calling the destructive GetSceneChanges drain itself.
        ///
        /// Handlers run with the scene lock held and must consume the Changes synchronously -
        /// SyncScene frees them on return, so the struct must not be retained.
        /// </summary>
        public event Action<SceneChangeTracker.Changes> SceneChangesDrained;

        /// <summary>
        /// Raised each time a controller brings a client up - on creation, and again whenever a
        /// renderer restart rebuilds it. Handlers run before any MirisStream can register with that
        /// client, so this is where client-wide configuration (e.g. the server ENV) belongs.
        /// </summary>
        public static event Action<MirisStreamController> ClientStarted;

        /// <summary>
        /// Raised as a controller starts, before its client is created, so values the native
        /// renderer reads only at creation - its resolution, overscan, MetalFX - can be set with no
        /// restart.
        /// </summary>
        public static event Action<MirisStreamController> Starting;

        public List<Action> m_onMetadataLoadedActions = new List<Action>();

        private bool IsEditMode => !Application.isPlaying;

        // --------------------------------------------------------------------
        // Public API
        // --------------------------------------------------------------------

        public Client GetClient()
        {
            Debug.Assert(m_client != null);
            return m_client;
        }

        public void SetPrefetchSettings(float flushFraction, float budgetWeight, bool subtractiveBudgetWeight = false)
        {
            Debug.Assert(m_client != null);
            m_runtimeSettings.m_prefetchFlushFraction = Mathf.Clamp(flushFraction, 0.0f, 0.8f);
            m_runtimeSettings.m_prefetchBudgetWeight = Mathf.Clamp(budgetWeight, 0.05f, 4.0f);
            m_runtimeSettings.m_prefetchBudgetMode = subtractiveBudgetWeight ? 1 : 0;
            m_client.SetRuntimeSettings(m_runtimeSettings);
        }

        public AssetManager GetAssetManager()
        {
            Debug.Assert(m_assetManager != null);
            return m_assetManager;
        }

        /// <summary>
        /// Sets <see cref="NativeRenderScale"/>, clamped to [<see cref="MinNativeRenderScale"/>, 1].
        /// The native renderer's resolution is fixed once it is up, so on a controller already
        /// rendering natively this restarts it on the next Tick and re-streams every loaded
        /// asset.
        /// </summary>
        public void SetNativeRenderScale(float scale)
        {
            scale = Mathf.Clamp(scale, MinNativeRenderScale, 1.0f);
            if (Mathf.Approximately(scale, m_nativeRenderScale))
            {
                m_pendingNativeRenderScale = null;
                return;
            }

            if (m_client == null || !IsSharkActive)
            {
                m_nativeRenderScale = scale;
                return;
            }

            m_pendingNativeRenderScale = scale;
        }

        /// <summary>
        /// Sets <see cref="SurfelMsaaEnabled"/>. On a running controller this restarts the native
        /// renderer and re-streams every loaded asset - the sample count is fixed for the life of
        /// the renderer, not a per-frame value.
        /// </summary>
        public void SetSurfelMsaaEnabled(bool enabled)
        {
            if (enabled == m_surfelMsaaEnabled)
            {
                m_pendingSurfelMsaaEnabled = null;
                return;
            }

            if (m_client == null || !IsSharkActive)
            {
                m_surfelMsaaEnabled = enabled;
                return;
            }

            m_pendingSurfelMsaaEnabled = enabled;
        }

        /// <summary>
        /// Sets <see cref="SurfelLessTextureSplatsEnabled"/>. Unlike <see cref="SetSurfelMsaaEnabled"/>
        /// this needs no restart.
        /// </summary>
        public void SetSurfelLessTextureSplatsEnabled(bool enabled)
        {
            m_surfelLessTextureSplatsEnabled = enabled;
        }

        /// <summary>
        /// Sets <see cref="DeviceResolutionViaMetalFx"/>, restarting a running native renderer on
        /// the next Tick.
        /// </summary>
        public void SetDeviceResolutionViaMetalFx(bool enabled)
        {
            if (enabled != m_deviceResolutionViaMetalFx)
            {
                m_deviceResolutionViaMetalFx = enabled;
                m_pendingNativeRestart |= m_client != null && IsSharkActive;
            }
        }

        /// <summary>
        /// Sets <see cref="ReprojectionOverscan"/>, clamped to [1, <see cref="MaxReprojectionOverscan"/>],
        /// restarting a running native renderer on the next Tick.
        /// </summary>
        public void SetReprojectionOverscan(float overscan)
        {
            overscan = Mathf.Clamp(overscan, 1.0f, MaxReprojectionOverscan);
            if (!Mathf.Approximately(overscan, m_reprojectionOverscan))
            {
                m_reprojectionOverscan = overscan;
                m_pendingNativeRestart |= m_client != null && IsSharkActive;
            }
        }

        private void ApplyPendingSurfelMsaaEnabled()
        {
            if (m_pendingSurfelMsaaEnabled == null)
            {
                return;
            }

            m_surfelMsaaEnabled = m_pendingSurfelMsaaEnabled.Value;
            m_pendingSurfelMsaaEnabled = null;
            if (IsSharkActive)
            {
                RestartRenderer();
            }
        }

        private void ApplyPendingNativeRenderScale()
        {
            if (m_pendingNativeRenderScale == null)
            {
                return;
            }

            m_nativeRenderScale = m_pendingNativeRenderScale.Value;
            m_pendingNativeRenderScale = null;
            if (IsSharkActive)
            {
                RestartRenderer();
            }
        }

        // The whole client, not just the streams: Shark adopts the client, and every loaded asset is
        // streamed again for the renderer that comes back up.
        private void RestartRenderer()
        {
            Teardown();
            Initialize();
        }

        /// <summary>
        /// The MirisStreams this controller has loaded. Grows as streams register, so callers
        /// should not cache it.
        /// </summary>
        internal IEnumerable<MirisStream> Streams => m_streamToSceneObjectId.Keys;

        /// <summary>
        /// Whether a MirisStream may add its asset yet: once native confirms it has adopted the
        /// client, and never where Shark is unavailable, since nothing would draw it.
        /// </summary>
        internal bool IsReadyForStreams()
        {
            if (m_client == null || !IsSharkActive)
            {
                return false;
            }
            return SharkSplatRenderSystem.m_instance.IsAdopted;
        }

        // --------------------------------------------------------------------
        // Non-scene data synchronization
        // --------------------------------------------------------------------

        private void SyncClientParameters()
        {
            // Unscaled: we're measuring the render time, not to do with the "game" time.
            // Prefer the measurement though, if available. (eg. on AVP)
            bool isXR = XRUtils.IsXR();
            double frameTimeMs = Time.unscaledDeltaTime * 1000.0;
            if (isXR && FrameHeadroom.TryGetEffectiveFrameMs(out double effectiveMs))
            {
                frameTimeMs = effectiveMs;
            }
            // Edit mode ticks only when something changes, so the time since the last tick says
            // nothing about how fast the editor draws - and read as a frame time, it starves the
            // budget. Report the target instead.
            if (IsEditMode)
            {
                frameTimeMs = 1000.0 / m_runtimeSettings.m_targetFramesPerSecond;
            }
            m_context.BeginFrame(frameTimeMs);
            m_runtimeSettings.m_xrModeActive = isXR;
            m_client.SetRuntimeSettings(m_runtimeSettings);
        }

        // --------------------------------------------------------------------
        // Miris Stream management
        // --------------------------------------------------------------------

        public void AddStream(MirisStream stream, string url)
        {
            // Update flags.
            m_updateRenderableObjects = true;
            m_loadedMetadata = false;

            SceneObject streamObject = m_scene.AddStream(stream.name, url, doNotRefine: IsEditMode);

            // Track the stream object.
            int sceneObjectId = streamObject.GetId();
            m_streamToSceneObjectId.Add(stream, sceneObjectId);
            m_streamToModelRootObjectIds.Add(stream, new());
            m_streamObjectIdToMirisStream.Add(sceneObjectId, stream);
            m_streamObjectIds.Add(sceneObjectId);

            // Assign Stream Object to Miris Stream component.
            stream.m_sceneObject = streamObject;
        }

        /// <summary>
        /// This is the same as AddStream but using uuid. Eventually, this should replace AddStream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="uuid"></param>
        internal void AddStreamById(MirisStream stream, string uuid)
        {
            // Update flags.
            m_updateRenderableObjects = true;
            m_loadedMetadata = false;

            SceneObject streamObject = m_scene.AddStreamById(stream.name, uuid, doNotRefine: IsEditMode);

            MirisDebug.Log($"Got stream object {streamObject}");

            // Track the stream object.
            int sceneObjectId = streamObject.GetId();
            m_streamToSceneObjectId.Add(stream, sceneObjectId);
            m_streamToModelRootObjectIds.Add(stream, new());
            m_streamObjectIdToMirisStream.Add(sceneObjectId, stream);
            m_streamObjectIds.Add(sceneObjectId);

            // Assign Stream Object to Miris Stream component.
            stream.m_sceneObject = streamObject;
        }

        internal void OnStreamLoaded(MirisStream stream)
        {
            GetAssetMetadata();

            CameraUtils.TryFitClipPlanesToContent(
                Camera.main, Streams.Select(loadedStream => loadedStream.GetWorldBounds()));
        }

        /// <summary>
        /// Attempts to set a stream's fetch mode (Regular or Prefetch), in either direction.
        /// Returns false if the stream isn't far enough along yet to support a mode change -- keep
        /// retrying until this returns true. Readiness is checked natively by Scene.SetStreamFetchMode.
        /// </summary>
        internal bool TrySetStreamFetchMode(MirisStream stream, StreamFetchMode mode)
        {
            if (!m_streamToSceneObjectId.ContainsKey(stream))
            {
                return false;
            }

            return m_scene.SetStreamFetchMode(stream.m_sceneObject, mode);
        }

        /// <summary>
        /// Reads a stream's current native fetch mode. Returns StreamFetchMode.Regular if the
        /// stream isn't registered with this controller.
        /// </summary>
        internal StreamFetchMode GetStreamFetchMode(MirisStream stream)
        {
            if (!m_streamToSceneObjectId.ContainsKey(stream))
            {
                return StreamFetchMode.Regular;
            }

            return m_scene.GetStreamFetchMode(stream.m_sceneObject);
        }

        internal void RemoveStream(MirisStream stream)
        {
            // Delete Stream object's entries from look-up tables
            if (m_streamToSceneObjectId.TryGetValue(stream, out int streamObjectId))
            {
                m_streamToSceneObjectId.Remove(stream);
                m_streamObjectIdToMirisStream.Remove(streamObjectId);
                foreach (int modelRootId in m_streamToModelRootObjectIds[stream])
                {
                    m_modelRootObjectIdToDataSources.Remove(modelRootId);
                }
                m_streamToModelRootObjectIds.Remove(stream);
                m_streamObjectIds.Remove(streamObjectId);
            }
            else
            {
                Debug.LogError($"Could not find stream {stream.name} to remove");
            }

            // Finally, delete underlying stream object, and un-assign from Miris Stream component.
            m_scene.RemoveStream(stream.m_sceneObject);
            stream.m_sceneObject = null;
            stream.m_status = StreamStatus.Unknown;
        }

        public bool IsActive()
        {
            return m_client != null;
        }

        // --------------------------------------------------------------------
        // Unity scene management
        // --------------------------------------------------------------------
        private void GetSceneMetadata()
        {
            // layer in structure file scene metadata values
            m_scene.GetMetadata(m_sceneMetadata);
            m_runtimeSettings.m_splatCountBudget = m_sceneMetadata.m_splatCountBudget;
            m_runtimeSettings.m_congestionMinInflightBytes = m_sceneMetadata.m_congestionMinInflightBytes;
            m_runtimeSettings.m_congestionMaxInflightBytes = m_sceneMetadata.m_congestionMaxInflightBytes;
        }

        public void GetAssetMetadata()
        {
            if (m_loadedMetadata)
            {
                return;
            }

            GetSceneMetadata();

            // Invoke metadata loaded callbacks
            foreach (Action action in m_onMetadataLoadedActions)
            {
                action.Invoke();
            }

            m_loadedMetadata = true;
        }

        private void SyncScene()
        {
            using (s_syncSceneMarker.Auto())
            {
                using (SceneChangeTracker changeTracker = new SceneChangeTracker(m_client))
                {
                    if (!changeTracker.IsSceneLocked())
                    {
                        return;
                    }


                    // Send updates to internal scene
                    SyncStreamingCamera();
                    foreach (MirisStream stream in m_streamToSceneObjectId.Keys)
                    {
                        stream.m_sceneObject.SetTransform(stream.transform.localToWorldMatrix);
                    }

                    SceneChangeTracker.Changes changes = changeTracker.GetSceneChanges();
                    Populate(changes.m_changeIds.createdObjectIds);
                    SetObjectsDirty(changes.m_changeIds.modifiedObjectIds, changes.m_changeIds.modifiedObjectFlags);
                    RemoveDeletedObjects(changes.m_changeIds.deletedObjectIds);
                    UpdateRenderableObjects();

                    foreach (MirisStream stream in m_streamToSceneObjectId.Keys)
                    {
                        stream.m_status = m_scene.GetStreamStatus(stream.m_sceneObject);
                    }

                    // After this controller's own bookkeeping, while the lock is still held and
                    // before the arrays are freed. See SceneChangesDrained.
                    SceneChangesDrained?.Invoke(changes);
                    changes.m_changeIds.Free();
                }
            }

            switch (m_executionMode)
            {
                case ExecutionMode.Asynchronous:
                    {
                        // Trigger execution update
                        m_scene.UpdateExecution();
                        break;
                    }
                case ExecutionMode.Synchronous:
                    {
                        // Wait for all scene operators to complete
                        m_scene.WaitForExecution();
                        break;
                    }
            }
        }

        // --------------------------------------------------------------------
        // Render Component Transform Calculation
        // --------------------------------------------------------------------        
        void UpdateRenderComponentTransform(int sceneObjectId)
        {
            SceneObject sceneObject = m_scene.GetSceneObject(sceneObjectId);

            KeyValuePair<int, MirisStream> streamEntry = m_streamObjectIdToMirisStream.First(kv => m_scene.GetSceneObject(kv.Key).IsAncestorOf(sceneObjectId));
            MirisAssetRenderComponent renderComponent = streamEntry.Value.GetRenderComponent(sceneObjectId);
            if (renderComponent != null)
            {
                // obtain stream relative transform
                Matrix4x4 assetMatrix = sceneObject.GetTransformRelativeToAncestor(streamEntry.Key);
                if (assetMatrix.ValidTRS())
                {
                    renderComponent.m_assetMatrix = assetMatrix;
                }
            }
        }



        // --------------------------------------------------------------------
        // Scene object population
        // --------------------------------------------------------------------
        private void Populate(Span<int> createdObjectIds)
        {
            if (createdObjectIds.Length > 0)
            {
                MirisDebug.Log($"Populating {createdObjectIds.Length} scene objects");
            }

            foreach (int sceneObjectId in createdObjectIds)
            {
                // Skip Miris Stream game objects -- they are already created 
                if (m_streamObjectIds.Contains(sceneObjectId))
                {
                    continue;
                }

                SceneObject sceneObject = m_scene.GetSceneObject(sceneObjectId);
                SceneObjectType sceneObjectType = sceneObject.GetSceneObjectType();

                // We handle the asset root object by initializing relevant data for the associated MirisStream
                if (sceneObjectType == SceneObjectType.ModelRoot)
                {
                    MirisStream stream = m_streamObjectIdToMirisStream.First(kv => m_scene.GetSceneObject(kv.Key).IsAncestorOf(sceneObjectId)).Value;
                    m_streamToModelRootObjectIds[stream].Add(sceneObjectId);
                    m_modelRootObjectIdToDataSources[sceneObjectId] = new();
                    stream.CreateRenderComponent(sceneObjectId);
                    UpdateRenderComponentTransform(sceneObjectId);
                }
                // Splat data is handled by creating a data source and associating it with the relevant MirisStream that contains it
                else if (sceneObjectType == SceneObjectType.GaussianSplats)
                {
                    int streamObjectId = m_streamObjectIdToMirisStream.First(kv => m_scene.GetSceneObject(kv.Key).IsAncestorOf(sceneObjectId)).Key;

                    MirisAssetDataSource data = new();
                    data.m_object = sceneObject;
                    data.m_ancestorSceneObjectId = streamObjectId;
                    m_splatObjectIdToDataSource.Add(sceneObjectId, data);

                    foreach (var (modelRootId, dataSources) in m_modelRootObjectIdToDataSources)
                    {
                        if (m_scene.GetSceneObject(modelRootId).IsAncestorOf(sceneObjectId))
                        {
                            dataSources.Add(data);
                            break;
                        }
                    }
                    m_updateRenderableObjects = true;
                }
            }
        }

        // --------------------------------------------------------------------
        // Scene object dirty propagation
        // --------------------------------------------------------------------
        private void SetObjectsDirty(Span<int> modifiedObjectIds, Span<int> modifiedObjectFlags)
        {
            for (int modifiedIndex = 0; modifiedIndex < modifiedObjectIds.Length; modifiedIndex++)
            {
                int modifiedObjectId = modifiedObjectIds[modifiedIndex];
                SceneObjectType sceneObjectType = m_scene.GetSceneObject(modifiedObjectId).GetSceneObjectType();
                SceneObjectModifyFlagState changeFlags;
                changeFlags.m_flags = (SceneObjectModifyFlag)modifiedObjectFlags[modifiedIndex];
                if (changeFlags.HasFlag(SceneObjectModifyFlag.ARRAYS))
                {
                    if (sceneObjectType == SceneObjectType.GaussianSplats)
                    {
                        MirisAssetDataSource data = m_splatObjectIdToDataSource[modifiedObjectId];
                        data.m_dirty = true;
                    }

                }
                if (changeFlags.HasFlag(SceneObjectModifyFlag.TRANSFORM))
                {
                    UpdateRenderComponentTransform(modifiedObjectId);
                    MarkDescendantDataSourcesDirty(modifiedObjectId);
                }
            }
        }

        private void MarkDescendantDataSourcesDirty(int modifiedObjectId)
        {
            SceneObject modifiedObject = m_scene.GetSceneObject(modifiedObjectId);
            foreach (var (splatObjectId, data) in m_splatObjectIdToDataSource)
            {
                if (splatObjectId == modifiedObjectId || modifiedObject.IsAncestorOf(splatObjectId))
                {
                    data.m_dirty = true;
                }
            }
        }

        // --------------------------------------------------------------------
        // Scene object deletion propagation
        // --------------------------------------------------------------------
        private void RemoveDeletedObjects(Span<int> deletedObjectIds)
        {
            foreach (int sceneObjectId in deletedObjectIds)
            {
                if (!m_splatObjectIdToDataSource.TryGetValue(sceneObjectId, out MirisAssetDataSource dataSource))
                {
                    continue;
                }

                m_splatObjectIdToDataSource.Remove(sceneObjectId);

                // Drop it from whichever model root's renderable list currently holds it, so the
                // renderer stops being handed a data source whose native scene object is gone.
                foreach (var dataSources in m_modelRootObjectIdToDataSources.Values)
                {
                    if (dataSources.Remove(dataSource))
                    {
                        break;
                    }
                }

                m_updateRenderableObjects = true;
            }
        }

        // --------------------------------------------------------------------
        // Update renderables
        // --------------------------------------------------------------------
        void UpdateRenderableObjects()
        {
            if (!m_updateRenderableObjects)
            {
                return;
            }

            // Update the data sources of each render component.
            foreach (var pair in m_streamToModelRootObjectIds)
            {
                MirisStream stream = pair.Key;
                HashSet<int> modelRootIds = pair.Value;
                foreach (int modelRootId in modelRootIds)
                {

                    Debug.Assert(m_modelRootObjectIdToDataSources.ContainsKey(modelRootId));
                    MirisAssetRenderComponent renderComponent = stream.GetRenderComponent(modelRootId);

                    var dataSources = m_modelRootObjectIdToDataSources[modelRootId];
                    if (dataSources.Count <= 0)
                    {
                        continue;
                    }

                    renderComponent.m_dataSources = dataSources.ToArray();
                }
            }

            m_updateRenderableObjects = false;
        }

        // --------------------------------------------------------------------
        // Initialization
        // --------------------------------------------------------------------

        private void Initialize()
        {
            if (m_client != null)
            {
                return;
            }

            // Initialize the context and client instance
            m_context = new();
            m_client = new(m_context);
            m_scene = new Scene(m_client);
            m_assetManager = new AssetManager(m_client);
            // SceneMetadata must be created here (not as field initializer) because it's a SWIG type
            // that triggers P/Invoke on construction. Field initializers run before the native
            // library is loaded, causing "Plugin loading is only allowed on main thread" errors.
            m_sceneMetadata = new SceneMetadata
            {
                m_verticalOffset = 0.0f,
                m_splatCountBudget = 400000,
            };

            // Initialize client config.
            m_clientConfig = ClientConfig.Load();

            m_viewerKey = ResolveViewerKey();
            ApplyViewerKey();

            PreparePersistentDataDir(m_client);

            m_sharkUnavailableReason = GetSharkUnavailableReason();
            if (m_sharkUnavailableReason != null)
            {
                Debug.LogError($"{nameof(MirisStreamController)}: splats will not render - "
                               + m_sharkUnavailableReason);
            }

            // Before any MirisStream loads - the profile is fixed per asset at load.
            Client.SetPreferSharkEncodingProfiles(true);

            ClientStarted?.Invoke(this);
        }

        // IsSupported and the pipeline before Activate, which only reserves Shark's single context and
        // succeeds even where the renderer cannot be created.
        private string GetSharkUnavailableReason()
        {
            if (!SharkSplatRenderSystem.IsSupported)
            {
                return "this platform has no native splat renderer (Shark runs on Apple platforms only)";
            }
            if (!SharkSplatRenderSystem.SupportsRenderPipeline(GraphicsSettings.currentRenderPipeline))
            {
                return "Shark supports the built-in render pipeline and URP only, not "
                       + GraphicsSettings.currentRenderPipeline.GetType().Name;
            }
            if (m_sharkFailed)
            {
                return "Shark could not be brought up; the next controller will retry";
            }
            if (!SharkSplatRenderSystem.m_instance.Activate(this))
            {
                return "another MirisStreamController already holds the native splat renderer";
            }
            return null;
        }

        private void ApplyViewerKey()
        {
            if (m_assetManager == null)
            {
                return;
            }

            m_assetManager.SetViewerKey(m_viewerKey);
            ViewerKeyChanged?.Invoke(m_viewerKey);
        }

        // The runtime key, else the active scene's, else the project's, else ClientConfig's.
        private string ResolveViewerKey()
        {
            if (!string.IsNullOrEmpty(m_runtimeViewerKey))
            {
                return m_runtimeViewerKey;
            }

            string settingsViewerKey = MirisSceneSettings.ResolveViewerKey()?.Trim();
            if (!string.IsNullOrEmpty(settingsViewerKey))
            {
                return settingsViewerKey;
            }

            return m_clientConfig != null ? m_clientConfig.GetAssetViewerKey() : "";
        }

        /// <summary>
        /// Sets a viewer key that wins over the scene's and project's keys, and pushes it to the
        /// AssetManager immediately. Pass null, empty, or whitespace to clear it, so the active
        /// scene's key applies, else the project's, else the one configured in ClientConfig.
        /// </summary>
        public void SetViewerKey(string viewerKey)
        {
            m_runtimeViewerKey = viewerKey?.Trim();
            m_viewerKey = ResolveViewerKey();
            ApplyViewerKey();
        }

        static private void PreparePersistentDataDir(Client client)
        {
            string dirPath = Path.Combine(Application.persistentDataPath, "miris");
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            client.SetPersistentDataDirectory(dirPath);
        }

        private void Teardown()
        {
            // Before the streams and the client go: Shark holds a handler on SceneChangesDrained and
            // a native context built around this client.
            SharkSplatRenderSystem.m_instance.Deactivate(this);

            // Normal cleanup path for non-quit scenarios (e.g., scene changes, disabling in editor)
            MirisStream[] streams = m_streamToSceneObjectId.Keys.ToArray();
            foreach (MirisStream stream in streams)
            {
                RemoveStream(stream);
                stream.ForceUnload();
            }

            m_streamToSceneObjectId.Clear();
            m_splatObjectIdToDataSource.Clear();
            m_clientConfig = null;

            m_scene?.Clear();

            // Teardown API objects
            m_assetManager?.Dispose();
            m_assetManager = null;
            m_scene = null;
            m_sceneMetadata?.Dispose();
            m_sceneMetadata = null;

            // Teardown the client, then the context it was created against.
            m_client?.Dispose();
            m_client = null;
            m_context?.Dispose();
            m_context = null;
        }

        // --------------------------------------------------------------------
        // Settings
        // --------------------------------------------------------------------

        private void OnSettingsChanged()
        {
            string viewerKey = ResolveViewerKey();
            if (viewerKey != m_viewerKey)
            {
                m_viewerKey = viewerKey;
                ApplyViewerKey();
            }
        }

        // The controller outlives scenes, so a fade one scene left part-way would carry over.
        private void OnActiveSceneChanged()
        {
            m_outputOpacity = 1.0f;
            OnSettingsChanged();
        }

        // --------------------------------------------------------------------
        // Lifetime and ticking
        // --------------------------------------------------------------------

        private void Start()
        {
            Starting?.Invoke(this);
            Initialize();

            MirisSettings.Changed += OnSettingsChanged;
            MirisSceneAsset.Changed += OnSettingsChanged;
            MirisSceneAsset.ActiveSceneChanged += OnActiveSceneChanged;
            InsertIntoPlayerLoop();
        }

        private void Stop()
        {
            RemoveFromPlayerLoop();
            MirisSceneAsset.ActiveSceneChanged -= OnActiveSceneChanged;
            MirisSceneAsset.Changed -= OnSettingsChanged;
            MirisSettings.Changed -= OnSettingsChanged;

            Teardown();
        }

        // Marks this controller's entry in the PlayerLoop.
        private struct MirisStreamControllerUpdate { }

        // Right after ScriptRunBehaviourLateUpdate, so camera rigs have moved before the camera and
        // stream transforms sync. Edit mode ticks only when the editor runs the PlayerLoop.
        private static void InsertIntoPlayerLoop()
        {
            EditPreLateUpdate(systems =>
            {
                if (systems.Any(system => system.type == typeof(MirisStreamControllerUpdate)))
                {
                    return;
                }

                int lateUpdateIndex = systems.FindIndex(
                    system => system.type == typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate));
                systems.Insert(lateUpdateIndex + 1, new PlayerLoopSystem
                {
                    type = typeof(MirisStreamControllerUpdate),
                    updateDelegate = OnPlayerLoopUpdate,
                });
            });
        }

        // Also before a domain reload: the PlayerLoop would otherwise keep calling a delegate into
        // the unloaded domain.
        private static void RemoveFromPlayerLoop()
        {
            EditPreLateUpdate(
                systems => systems.RemoveAll(system => system.type == typeof(MirisStreamControllerUpdate)));
        }

        private static void EditPreLateUpdate(Action<List<PlayerLoopSystem>> edit)
        {
            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            int phaseIndex = Array.FindIndex(root.subSystemList, phase => phase.type == typeof(PreLateUpdate));
            if (phaseIndex < 0)
            {
                return;
            }

            List<PlayerLoopSystem> systems = root.subSystemList[phaseIndex].subSystemList.ToList();
            edit(systems);
            root.subSystemList[phaseIndex].subSystemList = systems.ToArray();
            PlayerLoop.SetPlayerLoop(root);
        }

        private static void OnPlayerLoopUpdate()
        {
            s_instance?.Tick();
        }

        private void Tick()
        {
            if (m_client == null)
            {
                return;
            }

            ApplyPendingNativeRestart();
            ApplyPendingNativeRenderScale();
            ApplyPendingSurfelMsaaEnabled();
            TickRenderBackend();
            SyncClientParameters();
            SyncScene();
        }

        // Hands the client the camera it chooses detail and budget for. Without one, which in edit
        // mode is just a scene nobody has tagged yet, it warns once rather than fail every frame.
        private void SyncStreamingCamera()
        {
            Camera camera = StreamingCamera();
            if (camera == null)
            {
                if (!m_warnedNoMainCamera)
                {
                    Debug.LogWarning($"{nameof(MirisStreamController)}: no camera is tagged MainCamera, "
                                     + "so streaming has nothing to choose detail for");
                    m_warnedNoMainCamera = true;
                }
                return;
            }

            // Every sync, not once: a resized view or a changed FOV or clip plane is a new frustum, and
            // the client ignores one that has not changed.
            m_scene.SetMainCameraTransform(camera.transform.localToWorldMatrix);
            m_scene.SetMainCameraViewFrustum(camera);
        }

        // With Shark in edit mode, the Scene view the user is working in rather than a Game camera
        // they are not looking at.
        private Camera StreamingCamera()
        {
            Camera mainCamera = Camera.main;
#if UNITY_EDITOR
            if (IsEditMode && IsSharkActive)
            {
                UnityEditor.SceneView sceneView = UnityEditor.SceneView.lastActiveSceneView;
                if (sceneView != null && sceneView.camera != null && (mainCamera == null || sceneView.hasFocus))
                {
                    return sceneView.camera;
                }
            }
#endif
            return mainCamera;
        }

        // Before the other pending changes, any of which restarts anyway and would pick these up.
        private void ApplyPendingNativeRestart()
        {
            if (!m_pendingNativeRestart)
            {
                return;
            }

            m_pendingNativeRestart = false;
            if (IsSharkActive && m_pendingNativeRenderScale == null && m_pendingSurfelMsaaEnabled == null)
            {
                RestartRenderer();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            Shutdown();

            // Covers entering play mode with domain reload disabled, where statics carry over.
            s_shuttingDown = false;
            s_startFailed = false;
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting()
        {
            s_shuttingDown = true;
            Shutdown();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditorLifetime()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // Edit mode and play mode each get their own controller, so a play session starts from a
        // fresh client and Shark context rather than inheriting the editor's.
        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    Shutdown();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    s_shuttingDown = true;
                    Shutdown();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    s_shuttingDown = false;
                    s_startFailed = false;
                    break;
            }
        }
#endif

        // Shark cannot come up until the camera and, in XR, the compositor's drawable exist, so it
        // is brought up here rather than in Initialize.
        private void TickRenderBackend()
        {
            if (!IsSharkActive)
            {
                return;
            }

            SharkSplatRenderSystem.m_instance.SurfelMsaaEnabled = m_surfelMsaaEnabled;
            SharkSplatRenderSystem.m_instance.SurfelLessTextureSplatsEnabled = m_surfelLessTextureSplatsEnabled;
            SharkSplatRenderSystem.m_instance.OutputOpacity = m_outputOpacity;
            SharkSplatRenderSystem.m_instance.Tick();
            if (SharkSplatRenderSystem.m_instance.Failed)
            {
                // The restart drops the streams already loaded, which nothing would draw.
                m_sharkFailed = true;
                RestartRenderer();
            }
        }
    }
}
