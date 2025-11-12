// Copyright © 2024 Miris. All rights reserved.

// Standard library
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

        public enum ExecutionMode
        {
            Asynchronous = 0,
            Synchronous
        }
        [SerializeField]
        public ExecutionMode m_executionMode = ExecutionMode.Asynchronous;
        private SceneMetadata m_sceneMetadata = new SceneMetadata
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

        // Tracks the available MirisStreams in the current Unity scene.
        private Dictionary<MirisStream, int> m_streamToSceneObjectId = new();

        // Reverse map to go from a stream's scene object ID to associated MirisStream object
        private Dictionary<int, MirisStream> m_streamObjectIdToMirisStream = new();

        // Associates a miris stream to its constituent assets - note that a stream can load more than 1 asset
        private Dictionary<MirisStream, HashSet<int>> m_streamToAssetRootObjectIds = new();

        // Tracks all the splat data sources that belong to an asset root object
        private Dictionary<int, List<GaussianSplatDataSource>> m_assetRootObjectIdToDataSources = new();

        // Associates each GaussianSplat scene object ID to its data source. 
        private Dictionary<int, GaussianSplatDataSource> m_splatObjectIdToDataSource = new Dictionary<int, GaussianSplatDataSource>();

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

        // Tracks the coroutines responsible for transitioning LODs.
        private Dictionary<int, Coroutine> m_fadeCoroutines = new();

        // Lazily update the renderable objects only when data source's active state changes.
        private bool m_updateRenderableObjects = false;

        private AquaClientConfig m_clientConfig;

        private XRUtils m_xrUtils = new XRUtils();

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
            List<GaussianSplatRenderComponent> renderComponents = m_streamToSceneObjectId.Keys
                .SelectMany(stream => stream.m_assetRootObjectIdToRenderComponent.Values)
                .ToList();

            return renderComponents;
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
            m_streamToAssetRootObjectIds.Add(stream, new());
            m_streamObjectIdToMirisStream.Add(sceneObjectId, stream);
            m_streamObjectIds.Add(sceneObjectId);

            // Assign Stream Object to Miris Stream component.
            stream.m_sceneObject = streamObject;
        }

        public void RemoveStream(MirisStream stream)
        {
            // Stop associated coroutines (like fades)
            stream.StopAllCoroutines();

            // Delete Stream object's entries from look-up tables
            if (m_streamToSceneObjectId.TryGetValue(stream, out int streamObjectId))
            {
                m_streamToSceneObjectId.Remove(stream);
                m_streamObjectIdToMirisStream.Remove(streamObjectId);
                foreach (int assetRootId in m_streamToAssetRootObjectIds[stream])
                {
                    m_assetRootObjectIdToDataSources.Remove(assetRootId);
                }
                m_streamToAssetRootObjectIds.Remove(stream);
                m_streamObjectIds.Remove(streamObjectId);
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
                    Populate(changes.m_changeIds.createdObjectIds);
                    SetObjectsDirty(changes.m_changeIds.modifiedObjectIds, changes.m_changeIds.modifiedObjectFlags);

                    HashSet<int> createdObjectIdsSet = new HashSet<int>();
                    foreach(var createdId in changes.m_changeIds.createdObjectIds)
                    {
                        createdObjectIdsSet.Add(createdId);
                    }

                    SetObjectsActiveState(changes.m_changeIds.activatedObjectIds, changes.m_changeIds.deactivatedObjectIds, createdObjectIdsSet);
                    UpdateRenderableObjects();
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
        // Scene object population
        // --------------------------------------------------------------------

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

                // We handle the asset root object by initializing relevant data for the associated MirisStream
                if (sceneObjectType == SceneObjectType.AssetRootObject)
                {
                    MirisStream stream = m_streamObjectIdToMirisStream.First(kv => new AquaSceneObject(kv.Key).IsAncestorOf(sceneObjectId)).Value;
                    m_streamToAssetRootObjectIds[stream].Add(sceneObjectId);
                    m_assetRootObjectIdToDataSources[sceneObjectId] = new();

                    GaussianSplatRenderComponent renderComponent = stream.CreateRenderComponent(sceneObjectId);

                    Matrix4x4 assetRootMatrix = sceneObject.GetTransform();
                    AquaSceneObject spawnOffsetObject = new AquaSceneObject(sceneObject.GetParentId());
                    Matrix4x4 spawnOffsetMatrix = spawnOffsetObject.GetTransform();
                    if (assetRootMatrix.ValidTRS() && spawnOffsetMatrix.ValidTRS())
                    {
                        renderComponent.m_assetMatrix = spawnOffsetMatrix * assetRootMatrix;
                    }
                }
                // Splat data is handled by creating a data source and associating it with the relevant MirisStream that contains it
                else if (sceneObjectType == SceneObjectType.GaussianSplats)
                {
                    GaussianSplatDataSource data = new();
                    data.m_opacity = 0.0f;
                    data.m_object = sceneObject;
                    m_splatObjectIdToDataSource.Add(sceneObjectId, data);

                    foreach (var (assetRootId, dataSources) in m_assetRootObjectIdToDataSources)
                    {
                        if (new AquaSceneObject(assetRootId).IsAncestorOf(sceneObjectId))
                        {
                            dataSources.Add(data);
                            break;
                        }
                    }
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
                if(changeFlags.HasFlag(SceneObjectModifyFlag.ARRAYS)){
                    if(sceneObjectType == SceneObjectType.GaussianSplats)
                    {
                        GaussianSplatDataSource data = m_splatObjectIdToDataSource[modifiedObjectId];
                        data.m_dirty = true;
                    }
                }
            }
        }

        // --------------------------------------------------------------------
        // Scene object active state management
        // --------------------------------------------------------------------
        private void SetObjectsActiveState(Span<int> activeObjectIds, Span<int> deactivatedObjectIds, HashSet<int> createdObjectIdsSet)
        {
            foreach (int sceneObjectId in activeObjectIds)
            {
                SceneObjectType sceneObjectType = m_scene.GetSceneObject(sceneObjectId).GetSceneObjectType();
                if (sceneObjectType == SceneObjectType.GaussianSplats)
                {
                    MirisStream stream = m_streamObjectIdToMirisStream.First(kv => new AquaSceneObject(kv.Key).IsAncestorOf(sceneObjectId)).Value;
                    StopFade(sceneObjectId, stream);
                    GaussianSplatDataSource dataSource = m_splatObjectIdToDataSource[sceneObjectId];
                    m_fadeCoroutines[sceneObjectId] = stream.StartCoroutine(FadeIn(dataSource));
                }
            }

            foreach (int sceneObjectId in deactivatedObjectIds)
            {
                SceneObjectType sceneObjectType = m_scene.GetSceneObject(sceneObjectId).GetSceneObjectType();
                if(sceneObjectType == SceneObjectType.GaussianSplats)
                {
                    GaussianSplatDataSource dataSource = m_splatObjectIdToDataSource[sceneObjectId];
                    if (createdObjectIdsSet.Contains(sceneObjectId))
                    {
                        // If it was a newly created object that is starting out disabled, simply set its active state without fade out.
                        dataSource.m_active = false;
                        SetUpdateRenderableObjects(true);
                    } 
                    else
                    {
                        // Otherwise, start the fade out
                        MirisStream stream = m_streamObjectIdToMirisStream.First(kv => new AquaSceneObject(kv.Key).IsAncestorOf(sceneObjectId)).Value;
                        StopFade(sceneObjectId, stream);
                        m_fadeCoroutines[sceneObjectId] = stream.StartCoroutine(FadeOut(dataSource));
                    }
                }
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

            m_scene.GetLodMinMaxIndices(out int minLodIndex, out int maxLodIndex);

            // Update the data sources of each render component.
            foreach (MirisStream stream in m_streamToAssetRootObjectIds.Keys)
            {
                foreach ((int assetRootId, GaussianSplatRenderComponent renderComponent) in stream.m_assetRootObjectIdToRenderComponent)
                {
                    Debug.Assert(m_assetRootObjectIdToDataSources.ContainsKey(assetRootId));
                
                    var dataSources = m_assetRootObjectIdToDataSources[assetRootId];
                    if (dataSources.Count <= 0)
                    {
                        continue;
                    }

                    renderComponent.m_dataSources = dataSources.ToArray();
                    renderComponent.m_lodHeatMapMinLodIndex = minLodIndex;
                    renderComponent.m_lodHeatMapMaxLodIndex = maxLodIndex;
                }
            }

            Debug.Log("Update Renderable Objects");

            m_updateRenderableObjects = false;
        }
        
        private IEnumerator FadeIn(GaussianSplatDataSource dataSource)
        {
            float fadeDuration = m_fadeDurationSeconds;
            dataSource.m_active = true;
            SetUpdateRenderableObjects(true);

            if (fadeDuration > 0.0)
            {
                while (dataSource.m_opacity < 1.0)
                {
                    float timeFactor = Time.deltaTime / fadeDuration;
                    dataSource.m_opacity += timeFactor;
                    dataSource.m_opacity = Math.Min(dataSource.m_opacity, 1.0f);
                    yield return null;
                }
            }
            else
            {
                dataSource.m_opacity = 1.0f;
            }
        }

        private IEnumerator FadeOut(GaussianSplatDataSource dataSource)
        {
            float fadeDuration = m_fadeDurationSeconds;
            dataSource.m_active = true;
            if (fadeDuration > 0.0)
            {
                while (dataSource.m_opacity > 0.0)
                {
                    float timeFactor = Time.deltaTime / fadeDuration;
                    dataSource.m_opacity -= timeFactor;
                    dataSource.m_opacity = Math.Max(dataSource.m_opacity, 0.0f);
                    yield return null;
                }
            }
            else
            {
                dataSource.m_opacity = 0.0f;
            }

            dataSource.m_active = false;
            SetUpdateRenderableObjects(true);
        }

        private void StopFade(int sceneObjectId, MirisStream stream)
        {
            if (m_fadeCoroutines.TryGetValue(sceneObjectId, out Coroutine coroutine))
            {
                if (coroutine != null)
                {
                    stream.StopCoroutine(coroutine);
                }
            }
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

            m_scene?.Clear();
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
