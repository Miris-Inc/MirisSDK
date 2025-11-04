// Copyright © 2024 Miris. All rights reserved.

// Standard library
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// Unity engine
using UnityEngine;

// Unity packages
using Unity.Profiling;

// The functionality in this file is subject to change as the scene API evolves.

namespace Aqua.Runtime
{
    /// <summary>
    /// MirisStreamController streams an Aqua Scene into the Unity scene graph.
    /// Content will be populated under each of the Miris Streams.
    /// </summary>
    [ExecuteInEditMode]
    public class MirisStreamController : MonoBehaviour
    {
        [SerializeField, Tooltip("Used to set the initial XR floor height, and should be the Main Scene Camera.")]
        private GameObject m_cameraOffset;
        [SerializeField]
        private GameObject m_cameraOriginObject;

        [NonSerialized]
        public bool m_loadedMetadata = false;

        [SerializeField]
        public bool m_enableSkybox = true;

        [SerializeField]
        private Dictionary<int, GaussianSplatDataSource> m_idToDataSource = new Dictionary<int, GaussianSplatDataSource>();

        [SerializeField]
        public bool m_enableStreamVisualization = false;

        private BatchRoutineManager m_batchManager = new BatchRoutineManager();

        private static string s_overrideProductionUrl = null;

        [SerializeField]
        private TileCreationMode m_tileCreationMode = TileCreationMode.MergeTilesToOneObject;

        public enum ExecutionMode
        {
            Asynchronous = 0,
            Synchronous
        }
        [SerializeField]
        public ExecutionMode m_executionMode = ExecutionMode.Asynchronous;

        [SerializeField]
        public SceneMetadata m_sceneMetadata = new SceneMetadata
        {
            m_highestLodLimit = 0.8f,
            m_lowestLodLimit = 0.0f,
            m_lodMaxDistance = 20.0f,
            m_verticalOffset = 0.0f,
            m_spawnBehavior = AssetSpawnBehavior.CameraOriented,
        };

        [SerializeField]
        public LodRefinementParameters m_lodRefinementParameters = new LodRefinementParameters
        {
            m_lodSelectionMode = LodSelectionMode.Distance,
            m_lodRequestPriority = LodRequestPriority.CentralityProximity,
            m_graphicsLodCalibratorType = GraphicsLodCalibratorType.Disabled,
            m_graphicsLodCalibratorInfluence = 0.5f,
            m_targetFramesPerSecond = 72.0f,
            m_lowestLodLimit = 0.0f,
            m_highestLodLimit = 0.8f,
            m_lodMaxDistance = 20.0f,
            m_lodUpdateDistance = 5.0f,
            m_lodUpdateRotation = 20.0f,
            m_fixedLodIndex = 10
        };

        // Pointer to native scene object.
        private AquaScene m_scene = new();

        // Pointer to native timeline object.
        private AquaTimeline m_timeline = new();

        // Maps the aqua scene object ID -> the Unity Game Object.
        private Dictionary<int, GameObject> m_sceneObjectIdToGameObject = new();

        // Tracks the available MirisStreams in the current Unity scene.
        private Dictionary<MirisStream, int> m_streamToSceneObjectId = new();

        // Reverse map to go from a stream's scene object ID to associated MirisStream object
        private Dictionary<int, MirisStream> m_streamObjectIdToMirisStream = new();

        // Associates a MirisStream with all the splat data sources for the asset loaded by that stream
        private Dictionary<MirisStream, List<GaussianSplatDataSource>> m_streamToDataSources = new();

        // Miris Stream(s) already have their own GameObject, so we don't need to create
        // them in Populate().  m_streamObjectIds is used for a quick look-up to skip GameObject
        // creation
        private HashSet<int> m_streamObjectIds = new();

        static string s_profilerPrefix = "[MirisStreamController] ";
        static readonly ProfilerMarker s_syncSceneMarker = new ProfilerMarker(
            s_profilerPrefix + "Sync Scene"
        );

        [SerializeField]
        [Range(0.0f, 2.0f)]
        [Tooltip("Amount of time to fully fade in / out a particular tile.  Set this to 0 to disable the fade effect.")]
        public float m_fadeDurationSeconds = 0.3f;

        public float m_sceneTransitionFadeDuration
        {
            get
            {
                return m_fadeDurationSeconds * 2.0f;
            }
        }

        // Lazily update the renderable objects only when data source's active state changes.
        private bool m_updateRenderableObjects = false;

        private AquaClientConfig m_clientConfig;

        private XRUtils m_xrUtils = new XRUtils();

        private Dictionary<SceneObjectType, BaseObjectAdapter> m_adapters;
        public bool fadeLargeSplats
        {
            get
            {
                foreach (GaussianSplatRenderComponent renderComponent in GetRenderComponents())
                {
                    return renderComponent.m_fadeLargeSplats;
                }

                return false;
            }

            set
            {
                foreach (GaussianSplatRenderComponent renderComponent in GetRenderComponents())
                {
                    renderComponent.m_fadeLargeSplats = value;
                }
            }
        }

        public List<Action> m_onMetadataLoadedActions = new List<Action>();

        private bool IsEditMode => !Application.isPlaying;

        // --------------------------------------------------------------------
        // Public API
        // --------------------------------------------------------------------

        static public void SetProductionURLPrefix(string url)
        {
            s_overrideProductionUrl = url;
        }

        // Get the fully resolved & addressable scene path after variable expansion.
        public string ResolveUrl(string unresolvedUrl)
        {
            #if !MIRIS_INTERNAL
            // For external builds, do not perform any variable expansion.
            return unresolvedUrl;
            #else
            var replacements = new Dictionary<string, string>
            {
                {"devlocalhost", m_clientConfig.devlocalhost},
                {"devlocalhost_fqdn", m_clientConfig.devlocalhost_fqdn},
            };
            return StringUtils.ExpandVars(unresolvedUrl, replacements);
            #endif
        }

        // Get the version of the currently loaded asset
        public string GetAssetVersion()
        {
            return m_sceneMetadata.m_version;
        }

        public AquaScene GetScene()
        {
            return m_scene;
        }

        public GameObject GetMainCameraObject()
        {
            return m_cameraOriginObject;
        }

        public TileCreationMode GetTileCreationMode()
        {
            return m_tileCreationMode;
        }

        public void SetUpdateRenderableObjects(bool updateFlag)
        {
            m_updateRenderableObjects = updateFlag;
        }

        public string GetFormattedUrl(string contentPath)
        {

            if (!Uri.TryCreate(contentPath, UriKind.Absolute, out var uriResult) || !uriResult.IsAbsoluteUri)
            {
                return "";
            }

            return contentPath;
        }

        public List<GaussianSplatRenderComponent> GetRenderComponents()
        {
            List<GaussianSplatRenderComponent> renderComponents = new List<GaussianSplatRenderComponent>();
            foreach (MirisStream stream in m_streamToSceneObjectId.Keys)
            {
                renderComponents.AddRange(stream.gameObject.GetComponentsInChildren<GaussianSplatRenderComponent>());
            }
            return renderComponents;
        }

        public BaseObjectAdapter GetAdapter(SceneObjectType adapterType)
        {
            if (m_adapters.TryGetValue(adapterType, out BaseObjectAdapter adapter))
            {
                return adapter;
            }
            return null;
        }

        public void Initialize()
        {
            if (m_clientConfig != null)
            {
                // Already initialized.
                return;
            }

            m_clientConfig = AquaClientConfig.Load();
            if (!string.IsNullOrWhiteSpace(m_clientConfig.asset_viewer_key))
            {
                AquaUnityApi.SetAssetViewerKey(m_clientConfig.asset_viewer_key);
            }
            m_adapters = SceneObjectAdapterRegistry.s_instance.CreateAdapters();
        }

        // --------------------------------------------------------------------
        // Non-scene data synchronization
        // --------------------------------------------------------------------

        private void SyncClientParameters()
        {
            int splatCount = 0;
            foreach (GaussianSplatRenderComponent renderComponent in GetRenderComponents())
            {
                splatCount += renderComponent.GetSplatCount();
            }

            AquaClient.RecordFrameInfo(splatCount);
            AquaClient.SetLodRefinementParameters(m_lodRefinementParameters);
        }

        // --------------------------------------------------------------------
        // Miris Stream management
        // --------------------------------------------------------------------

        public bool HasStream(MirisStream stream)
        {
            return m_streamToSceneObjectId.ContainsKey(stream);
        }

        public void AddStream(MirisStream stream, string url)
        {
            // Update flags.
            m_updateRenderableObjects = true;
            m_loadedMetadata = false;

            // Update XR State 
            // TODO: This is Miris Player specific behavior and shoulud be re-factored as such.
            m_scene.SetXRFloorHeight(m_xrUtils.GetXRFloorHeight(m_cameraOffset));

            AquaSceneObject streamObject = m_scene.AddStream(stream.name, url, doNotRefine: IsEditMode);

            // Track the stream object.
            int sceneObjectId = streamObject.GetId();
            m_streamToSceneObjectId.Add(stream, sceneObjectId);
#if UNITY_EDITOR
            if (IsEditMode)
            {
                m_streamObjectIdToMirisStream.Add(sceneObjectId, stream);
            }
#endif
            m_streamObjectIds.Add(sceneObjectId);
            m_sceneObjectIdToGameObject.Add(sceneObjectId, stream.gameObject);

            // Assign Stream Object to Miris Stream component.
            stream.m_sceneObject = streamObject;
        }

        public void RemoveStream(MirisStream stream)
        {
            // Stop associated coroutines (like fades)
            stream.StopAllCoroutines();

            // Clean-up descendent scene object state.
            foreach (AquaSceneObjectReference sceneObjectRef in stream.GetComponentsInChildren<AquaSceneObjectReference>(includeInactive: true))
            {
                m_sceneObjectIdToGameObject.Remove(sceneObjectRef.m_sceneObject.GetId());
            }

            // Clean-up adapter state.
            foreach (AquaSceneObjectReference sceneObjectRef in stream.GetComponentsInChildren<AquaSceneObjectReference>())
            {
                CleanupAdapterState(sceneObjectRef);
            }

            // Remove child objects.
            foreach (Transform child in stream.transform)
            {
                Destroy(child.gameObject);
            }

#if UNITY_EDITOR
            // Remove preview objects
            foreach (var renderComponent in stream.GetComponentsInChildren<GaussianSplatRenderComponent>())
            { 
                DestroyImmediate(renderComponent);
            }
#endif

            // Delete Stream object's entries from look-up tables
            if (m_streamToSceneObjectId.TryGetValue(stream, out int streamObjectId))
            {
                m_streamToSceneObjectId.Remove(stream);
#if UNITY_EDITOR
                m_streamObjectIdToMirisStream.Remove(streamObjectId);
                m_streamToDataSources.Remove(stream);
#endif
                m_streamObjectIds.Remove(streamObjectId);
                m_sceneObjectIdToGameObject.Remove(streamObjectId);
            }
            else
            {
                Debug.LogError($"Could not find stream {stream.name} to remove");
            }

            // Finally, delete underlying Aqua stream object, and un-assign from Miris Stream component.
            m_scene.RemoveStream(stream.m_sceneObject);
            stream.m_sceneObject = null;
        }

        // --------------------------------------------------------------------
        // Unity scene management
        // --------------------------------------------------------------------
        private void GetSceneMetadata()
        {
            // layer in structure file scene metadata values
            m_scene.GetMetadata(out m_sceneMetadata);
            m_lodRefinementParameters.m_lodMaxDistance = m_sceneMetadata.m_lodMaxDistance;
            m_lodRefinementParameters.m_highestLodLimit = m_sceneMetadata.m_highestLodLimit;
            m_lodRefinementParameters.m_lowestLodLimit = m_sceneMetadata.m_lowestLodLimit;
        }

        public void GetAssetMetadata(AquaSceneObject sceneObject = null, GameObject unityObject = null)
        {
            if (m_loadedMetadata)
            {
                return;
            }

            // Layer in structure file object metadata values
            if (sceneObject != null && unityObject != null)
            {
                sceneObject.GetMetadata(out unityObject.GetComponent<AquaAssetRoot>().m_assetMetadata);
                AssetMetadata metadata = unityObject.GetComponent<AquaAssetRoot>().m_assetMetadata;
            }

            GetSceneMetadata();

            // Invoke metadata loaded callbacks
            foreach (Action action in m_onMetadataLoadedActions)
            {
                action.Invoke();
            }

            m_loadedMetadata = true;
        }

#if UNITY_EDITOR
        private void PreviewSync(AquaSceneChangeTracker.Changes changes)
        {
            PreviewPopulate(changes.m_changeIds.createdObjectIds);
            PreviewSetObjectsDirty(changes.m_changeIds.modifiedObjectIds, changes.m_changeIds.modifiedObjectFlags);

            HashSet<int> createdObjectIdsSet = new HashSet<int>();
            foreach(var createdId in changes.m_changeIds.createdObjectIds)
            {
                createdObjectIdsSet.Add(createdId);
            }

            PreviewSetObjectsActiveState(changes.m_changeIds.activatedObjectIds, changes.m_changeIds.deactivatedObjectIds, createdObjectIdsSet);
            PreviewUpdateRenderableObjects();
        }
#endif

        private void SyncScene()
        {
            using (s_syncSceneMarker.Auto())
            {
                using (AquaSceneChangeTracker changeTracker = new AquaSceneChangeTracker())
                {
                    if (!changeTracker.IsSceneLocked())
                    {
                        return;
                    }


                    // Send updates to aqua scene
                    Debug.Assert(Camera.main != null, "Scene must have a main camera");
                    m_scene.SetMainCameraTransform(Camera.main.transform.localToWorldMatrix);
                    foreach (MirisStream stream in m_streamToSceneObjectId.Keys)
                    {
                        stream.m_sceneObject.SetTransform(stream.transform.localToWorldMatrix);
                    }

                    AquaSceneChangeTracker.Changes changes = changeTracker.GetSceneChanges();
                    // Consume aqua scene changes in Unity
#if UNITY_EDITOR
                    if (IsEditMode)
                    {
                        PreviewSync(changes);
                    }
                    else
#endif
                    {
                        Populate(changes.m_changeIds.createdObjectIds);
                        SetObjectsDirty(changes.m_changeIds.modifiedObjectIds, changes.m_changeIds.modifiedObjectFlags);

                        HashSet<int> createdObjectIdsSet = new HashSet<int>();
                        foreach(var createdId in changes.m_changeIds.createdObjectIds)
                        {
                            createdObjectIdsSet.Add(createdId);
                        }

                        SetObjectsActiveState(changes.m_changeIds.activatedObjectIds, changes.m_changeIds.deactivatedObjectIds, createdObjectIdsSet);
                        UpdateRenderableObjects();
                    }
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

        private void CleanupAdapterState(AquaSceneObjectReference sceneObjectRef)
        {
            SceneObjectType sceneObjectType = sceneObjectRef.m_sceneObject.GetSceneObjectType();
            if (m_adapters.TryGetValue(sceneObjectType, out BaseObjectAdapter adapter))
            {
                adapter.Destroy(sceneObjectRef.gameObject);
            }
            else
            {
                Debug.LogError($"Failed to find adapter for {sceneObjectType.ToString()}");
            }
        }

        // --------------------------------------------------------------------
        // Scene object population
        // --------------------------------------------------------------------

#if UNITY_EDITOR
        private void PreviewPopulate(Span<int> createdObjectIds)
        {
            if (createdObjectIds.Length > 0)
            {
                Debug.Log($"Populating {createdObjectIds.Length} scene objects");
            }

            foreach (int sceneObjectId in createdObjectIds)
            {
                // Skip Miris Stream game objects -- they are already created 
                if (m_streamObjectIds.Contains(sceneObjectId))
                {
                    continue;
                }

                AquaSceneObject sceneObject = m_scene.GetSceneObject(sceneObjectId);
                SceneObjectType sceneObjectType = sceneObject.GetSceneObjectType();

                // We handle the asset root object by initializing relevant data for the associated MirisStream
                if (sceneObjectType == SceneObjectType.AssetRootObject)
                {
                    MirisStream stream = m_streamObjectIdToMirisStream.First(kv => new AquaSceneObject(kv.Key).IsAncestorOf(sceneObjectId)).Value;

                    GaussianSplatRenderComponent renderComponent = stream.GetComponent<GaussianSplatRenderComponent>();
                    if (renderComponent == null)
                    {
                        renderComponent = stream.gameObject.AddComponent<GaussianSplatRenderComponent>();
                    }

                    Matrix4x4 assetMatrix = sceneObject.GetTransform();
                    if (assetMatrix.ValidTRS())
                    {
                        renderComponent.m_assetMatrix = assetMatrix; // TODO: Ideally we also premultiply the SpawnOffset matrix to maintain total consistency between Edit and Play mode
                    }

                    m_streamToDataSources[stream] = new();
                }
                // Splat data is handled by creating a data source and associating it with the relevant MirisStream that contains it
                else if (sceneObjectType == SceneObjectType.GaussianSplats)
                {
                    GaussianSplatDataSource data = new();
                    data.m_object = sceneObject;
                    m_idToDataSource.Add(sceneObjectId, data);
                    Debug.Log($"Preview Populate {sceneObjectId}");

                    MirisStream stream = m_streamObjectIdToMirisStream.First(kv => new AquaSceneObject(kv.Key).IsAncestorOf(sceneObjectId)).Value;
                    m_streamToDataSources[stream].Add(data);
                }
            }
        }
#endif

        // Only call this from within the scope of a AquaSceneChangeTracker context.
        private void Populate(Span<int> createdObjectIds)
        {

            if (createdObjectIds.Length > 0)
            {
                Debug.Log($"Populating {createdObjectIds.Length} scene objects");
            }

            foreach (int sceneObjectId in createdObjectIds)
            {
                // Skip Miris Stream game objects -- they are already created 
                if (m_streamObjectIds.Contains(sceneObjectId))
                {
                    continue;
                }

                AquaSceneObject sceneObject = m_scene.GetSceneObject(sceneObjectId);
                SceneObjectType sceneObjectType = sceneObject.GetSceneObjectType();

                if (m_adapters.TryGetValue(sceneObjectType, out BaseObjectAdapter adapter))
                {
                    GameObject createdObject = adapter.Populate(sceneObjectId, sceneObject, this);
                    AquaSceneObjectReference sceneObjectRef = createdObject.AddComponent<AquaSceneObjectReference>();
                    sceneObjectRef.m_sceneObject = sceneObject;
                    m_sceneObjectIdToGameObject[sceneObjectId] = createdObject;
                }
                else
                {
                    Debug.LogError($"Failed to find adapter for {sceneObjectType.ToString()}");
                }
            }
        }


        public void SetParent(AquaSceneObject sceneObject, GameObject newGameObject)
        {
            GameObject parentGameObject = this.gameObject;
            int parentSceneObjectId = sceneObject.GetParentId();
            if (m_sceneObjectIdToGameObject.ContainsKey(parentSceneObjectId))
            {
                parentGameObject = m_sceneObjectIdToGameObject[parentSceneObjectId];
            }

            newGameObject.transform.SetParent(parentGameObject.transform, worldPositionStays: false);
        }
        // --------------------------------------------------------------------
        // Scene object dirty propagation
        // --------------------------------------------------------------------
#if UNITY_EDITOR
        private void PreviewSetObjectsDirty(Span<int> modifiedObjectIds, Span<int> modifiedObjectFlags)
        {
            for (int modifiedIndex = 0; modifiedIndex < modifiedObjectIds.Length; modifiedIndex++)
            {
                int modifiedObjectId = modifiedObjectIds[modifiedIndex];
                AquaSceneObject sceneObject = m_scene.GetSceneObject(modifiedObjectId);
                SceneObjectType sceneObjectType = m_scene.GetSceneObject(modifiedObjectId).GetSceneObjectType();
                SceneObjectModifyFlagState changeFlags;
                changeFlags.m_flags = (SceneObjectModifyFlag)modifiedObjectFlags[modifiedIndex];
                if(changeFlags.HasFlag(SceneObjectModifyFlag.ARRAYS)){
                    if(sceneObjectType == SceneObjectType.GaussianSplats)
                    {
                        GaussianSplatDataSource data = m_idToDataSource[modifiedObjectId];
                        data.m_dirty = true;
                        data.DebugPrint();
                        Debug.Log($"Preview Modified id: {modifiedObjectId} splatCount: {data.GetSplatCount()}");
                    }
                }
            }
        }
#endif

        private void SetObjectsDirty(Span<int> modifiedObjectIds, Span<int> modifiedObjectFlags)
        {
            for (int modifiedIndex = 0; modifiedIndex < modifiedObjectIds.Length; modifiedIndex++)
            {
                int modifiedObjectId = modifiedObjectIds[modifiedIndex];
                GameObject currentGameObject = m_sceneObjectIdToGameObject[modifiedObjectId];
                AquaSceneObject sceneObject = m_scene.GetSceneObject(modifiedObjectId);
                SceneObjectType sceneObjectType = m_scene.GetSceneObject(modifiedObjectId).GetSceneObjectType();
                SceneObjectModifyFlagState changeFlags;
                changeFlags.m_flags = (SceneObjectModifyFlag)modifiedObjectFlags[modifiedIndex];
                if (m_adapters.TryGetValue(sceneObjectType, out BaseObjectAdapter adapter))
                {
                    adapter.SetDirty(currentGameObject, sceneObject, changeFlags);
                }
                else
                {
                    Debug.LogError($"Failed to find adapter for {sceneObjectType.ToString()}");
                }
            }
        }

        // --------------------------------------------------------------------
        // Scene object active state management
        // --------------------------------------------------------------------
#if UNITY_EDITOR
        private void PreviewSetObjectsActiveState(Span<int> activeObjectIds, Span<int> deactivatedObjectIds, HashSet<int> createdObjectIdsSet)
        {
            foreach (int sceneObjectId in activeObjectIds)
            {
                SceneObjectType sceneObjectType = m_scene.GetSceneObject(sceneObjectId).GetSceneObjectType();
                if(sceneObjectType == SceneObjectType.GaussianSplats){
                    m_idToDataSource[sceneObjectId].m_active = true;
                }
            }
            foreach (int sceneObjectId in deactivatedObjectIds)
            {
                SceneObjectType sceneObjectType = m_scene.GetSceneObject(sceneObjectId).GetSceneObjectType();
                if(sceneObjectType == SceneObjectType.GaussianSplats){
                    m_idToDataSource[sceneObjectId].m_active = false;
                }
                SetUpdateRenderableObjects(true);
            }
        }
#endif

        private void SetObjectsActiveState(Span<int> activeObjectIds, Span<int> deactivatedObjectIds, HashSet<int> createdObjectIdsSet)
        {
            foreach (int sceneObjectId in activeObjectIds)
            {

                GameObject currentGameObject = m_sceneObjectIdToGameObject[sceneObjectId];
                SceneObjectType sceneObjectType = m_scene.GetSceneObject(sceneObjectId).GetSceneObjectType();
                if (m_adapters.TryGetValue(sceneObjectType, out BaseObjectAdapter adapter))
                {
                    adapter.SetActive(currentGameObject, true, this);
                }
                else
                {
                    Debug.LogError($"Failed to find adapter for {sceneObjectType.ToString()}");
                }
            }
            foreach (int sceneObjectId in deactivatedObjectIds)
            {
                GameObject currentGameObject = m_sceneObjectIdToGameObject[sceneObjectId];
                if (createdObjectIdsSet.Contains(sceneObjectId))
                {
                    // If it was a newly created object that is starting out disabled, simply set its active state without fade out.
                    currentGameObject.SetActive(false);
                    SetUpdateRenderableObjects(true);
                }
                else
                {
                    SceneObjectType sceneObjectType = m_scene.GetSceneObject(sceneObjectId).GetSceneObjectType();
                    if (m_adapters.TryGetValue(sceneObjectType, out BaseObjectAdapter adapter))
                    {
                        adapter.SetActive(currentGameObject, false, this);
                    }
                    else
                    {
                        Debug.LogError($"Failed to find adapter for {sceneObjectType.ToString()}");
                    }
                }
            }
        }

        // --------------------------------------------------------------------
        // Update renderables
        // --------------------------------------------------------------------
#if UNITY_EDITOR
        void PreviewUpdateRenderableObjects()
        {
            if (!m_updateRenderableObjects)
            {
                return;
            }

            m_scene.GetLodMinMaxIndices(out int minLodIndex, out int maxLodIndex);
            
            // Update the data sources of each render components.
            foreach ((GaussianSplatRenderComponent renderComponent, GaussianSplatDataSource[] dataSources) in m_streamToSceneObjectId
                .Keys
                .Select(stream => (stream, stream.GetComponent<GaussianSplatRenderComponent>()))
                .Where(x => x.Item1 != null && x.Item2 != null && m_streamToDataSources.ContainsKey(x.Item1))
                .Select(x => (x.Item2, m_streamToDataSources[x.Item1].ToArray()))
                .Where(x => x.Item2.Length > 0))
            {
                renderComponent.m_dataSourceComponents = null;
                renderComponent.m_dataSources = dataSources;
                renderComponent.m_lodHeatMapMinLodIndex = minLodIndex;
                renderComponent.m_lodHeatMapMaxLodIndex = maxLodIndex;
            }

            Debug.Log("Preview Update Renderable Objects");

            m_updateRenderableObjects = false;
        }
#endif

        void UpdateRenderableObjects()
        {
            // If no objects were created, activated, or de-activated, then 
            if (!m_updateRenderableObjects)
            {
                return;
            }

            m_scene.GetLodMinMaxIndices(out int minLodIndex, out int maxLodIndex);

            // Update the data sources of each render components.  
            // TODO: This could be more refined by introspecting the changed ids.
            foreach (GaussianSplatRenderComponent renderComponent in GetRenderComponents())
            {
                GaussianSplatAquaDataSource[] dataSourceComponents =
                    renderComponent.gameObject.GetComponentsInChildren<GaussianSplatAquaDataSource>();
                renderComponent.m_dataSourceComponents = dataSourceComponents;
                renderComponent.m_dataSources = dataSourceComponents.Select(x => x.m_data).ToArray();
                renderComponent.m_lodHeatMapMinLodIndex = minLodIndex;
                renderComponent.m_lodHeatMapMaxLodIndex = maxLodIndex;
            }

            m_updateRenderableObjects = false;
        }

        // --------------------------------------------------------------------
        // Unity event handling
        // --------------------------------------------------------------------

        protected void OnEnable()
        {
            Initialize();
        }

        private bool m_isApplicationQuitting = false;

        public bool IsApplicationQuitting => m_isApplicationQuitting;

        protected void OnApplicationQuit()
        {
            m_isApplicationQuitting = true;
        }

        protected void OnDisable()
        {

            if (m_isApplicationQuitting)
            {
                // During application quit, skip native calls as the client may already be destroyed
                m_streamToSceneObjectId.Clear();
                m_sceneObjectIdToGameObject.Clear();
                m_adapters = null;
                m_clientConfig = null;
                return;
            }

            // Normal cleanup path for non-quit scenarios (e.g., scene changes, disabling in editor)
            MirisStream[] streams = m_streamToSceneObjectId.Keys.ToArray();
            foreach (MirisStream stream in streams)
            {
                RemoveStream(stream);
            }

            m_streamToSceneObjectId.Clear();
            m_sceneObjectIdToGameObject.Clear();

            m_scene?.Clear();

            m_adapters = null;
            m_clientConfig = null;
        }

        void Start()
        {
            // Initialize client & scene state
            m_scene.SetMainCameraTransform(Camera.main.transform.localToWorldMatrix);
            m_scene.SetMainCameraViewFrustum(Camera.main);
        }

        protected void Update()
        {
            m_timeline.AdvanceTime();
        }

        protected void LateUpdate()
        {
            SyncClientParameters();
            SyncScene();
        }
    }
}
