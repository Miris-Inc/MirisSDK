// Copyright © 2026 Miris, Inc. All rights reserved.

// Standard library
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

// Unity engine
using UnityEngine;

// Unity packages
using Unity.Profiling;

namespace Miris.Runtime
{
    /// <summary>
    /// MirisStreamController streams an Scene into the Unity scene graph.
    /// Content will be populated under each of the Miris Streams.
    /// </summary>
    [ExecuteInEditMode]
    public class MirisStreamController : MonoBehaviour
    {
        [NonSerialized]
        public bool m_loadedMetadata = false;

        public enum ExecutionMode
        {
            Asynchronous = 0,
            Synchronous
        }
        [SerializeField]
        public ExecutionMode m_executionMode = ExecutionMode.Asynchronous;
        private SceneMetadata m_sceneMetadata;

        [SerializeField]
        public RuntimeSettings m_runtimeSettings = new RuntimeSettings
        {
            m_lodSelectionMode = LodSelectionMode.Distance,
            m_lodRequestPriority = LodRequestPriority.CentralityProximity,
            m_graphicsLodCalibratorType = GraphicsLodCalibratorType.Disabled,
            m_graphicsLodCalibratorInfluence = 0.5f,
            m_targetFramesPerSecond = 72.0f,
            m_lowestLodLimit = 0.0f,
            m_highestLodLimit = 1.0f,
            m_lodMaxDistance = 20.0f,
            m_lodUpdateDistance = 5.0f,
            m_lodUpdateRotation = 20.0f,
            m_fixedLodIndex = 10,
            m_splatCountBudget = 400000,
            m_congestionMinInflightBytes = 256 * 1024,
            m_congestionMaxInflightBytes = 128 * 1024 * 1024,
        };
        // Client instance and API helpers
        private Client m_client;
        private Scene m_scene;
        private AssetManager m_assetManager;

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

        // Tracks the coroutines responsible for transitioning LODs.
        private Dictionary<int, Coroutine> m_fadeCoroutines = new();

        // Lazily update the renderable objects only when data source's active state changes.
        private bool m_updateRenderableObjects = false;

        private ClientConfig m_clientConfig;

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

        public List<GaussianSplatRenderComponent> GetRenderComponents()
        {
            List<GaussianSplatRenderComponent> renderComponents = m_streamToSceneObjectId.Keys
                .SelectMany(stream => stream.GetRenderComponents())
                .ToList();

            return renderComponents;
        }

        public Client GetClient()
        {
            Debug.Assert(m_client != null);
            return m_client;
        }

        public AssetManager GetAssetManager()
        {
            Debug.Assert(m_assetManager != null);
            return m_assetManager;
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

            m_client.RecordFrameInfo(splatCount);
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
            m_streamToAssetRootObjectIds.Add(stream, new());
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
            m_streamToAssetRootObjectIds.Add(stream, new());
            m_streamObjectIdToMirisStream.Add(sceneObjectId, stream);
            m_streamObjectIds.Add(sceneObjectId);

            // Assign Stream Object to Miris Stream component.
            stream.m_sceneObject = streamObject;
        }

        internal void RemoveStream(MirisStream stream)
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

            // Finally, delete underlying stream object, and un-assign from Miris Stream component.
            m_scene.RemoveStream(stream.m_sceneObject);
            stream.m_sceneObject = null;
        }

        public bool IsActive()
        {
            // Unfortunately we cannot rely on soely .isActiveAndEnabled :\
            return isActiveAndEnabled && m_client != null;
        }

        // --------------------------------------------------------------------
        // Unity scene management
        // --------------------------------------------------------------------
        private void GetSceneMetadata()
        {
            // layer in structure file scene metadata values
            m_scene.GetMetadata(m_sceneMetadata);
            m_runtimeSettings.m_lodMaxDistance = m_sceneMetadata.m_lodMaxDistance;
            m_runtimeSettings.m_highestLodLimit = m_sceneMetadata.m_highestLodLimit;
            m_runtimeSettings.m_lowestLodLimit = m_sceneMetadata.m_lowestLodLimit;
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
                    Debug.Assert(Camera.main != null, "Scene must have a main camera");
                    m_scene.SetMainCameraTransform(Camera.main.transform.localToWorldMatrix);
                    foreach (MirisStream stream in m_streamToSceneObjectId.Keys)
                    {
                        stream.m_sceneObject.SetTransform(stream.transform.localToWorldMatrix);
                    }

                    SceneChangeTracker.Changes changes = changeTracker.GetSceneChanges();
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
        // Render Component Transform Calculation
        // --------------------------------------------------------------------        
        void UpdateRenderComponentTransform(int sceneObjectId)
        {
            SceneObject sceneObject = m_scene.GetSceneObject(sceneObjectId);
            Matrix4x4 assetMatrix = sceneObject.GetTransform();

            MirisStream stream = m_streamObjectIdToMirisStream.First(kv => m_scene.GetSceneObject(kv.Key).IsAncestorOf(sceneObjectId)).Value;
            GaussianSplatRenderComponent renderComponent = stream.GetRenderComponent(sceneObjectId);
            if(renderComponent != null){
                SceneObject spawnOffsetObject = m_scene.GetSceneObject(sceneObject.GetParentId());
                Matrix4x4 spawnOffsetMatrix = spawnOffsetObject.GetTransform();
                if (assetMatrix.ValidTRS() && spawnOffsetMatrix.ValidTRS())
                {
                    renderComponent.m_assetMatrix = spawnOffsetMatrix * assetMatrix;
                }
            }
        }



        // --------------------------------------------------------------------
        // Control temporary stall of render culling
        // -------------------------------------------------------------------- 
        public void ToggleRenderComponentCulling()
        {
            List<GaussianSplatRenderComponent> renderComponents = GetRenderComponents();
            foreach (GaussianSplatRenderComponent renderComponent in renderComponents)
            {
                renderComponent.ToggleCulling();
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
                if (sceneObjectType == SceneObjectType.AssetRootObject)
                {
                    MirisStream stream = m_streamObjectIdToMirisStream.First(kv => m_scene.GetSceneObject(kv.Key).IsAncestorOf(sceneObjectId)).Value;
                    m_streamToAssetRootObjectIds[stream].Add(sceneObjectId);
                    m_assetRootObjectIdToDataSources[sceneObjectId] = new();
                    stream.CreateRenderComponent(sceneObjectId);
                    UpdateRenderComponentTransform(sceneObjectId);
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
                        if (m_scene.GetSceneObject(assetRootId).IsAncestorOf(sceneObjectId))
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
                if(changeFlags.HasFlag(SceneObjectModifyFlag.TRANSFORM)){
                    UpdateRenderComponentTransform(modifiedObjectId);
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
                    MirisStream stream = m_streamObjectIdToMirisStream.First(kv => m_scene.GetSceneObject(kv.Key).IsAncestorOf(sceneObjectId)).Value;
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
                        m_updateRenderableObjects = true;
                    } 
                    else
                    {
                        // Otherwise, start the fade out
                        MirisStream stream = m_streamObjectIdToMirisStream.First(kv => m_scene.GetSceneObject(kv.Key).IsAncestorOf(sceneObjectId)).Value;
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
            foreach (var pair in m_streamToAssetRootObjectIds)
            {
                MirisStream stream = pair.Key;
                HashSet<int> assetRootIds = pair.Value;
                foreach (int assetRootId in assetRootIds)
                {
                    Debug.Assert(m_assetRootObjectIdToDataSources.ContainsKey(assetRootId));
                    GaussianSplatRenderComponent renderComponent = stream.GetRenderComponent(assetRootId);
                
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

            m_updateRenderableObjects = false;
        }
        
        private IEnumerator FadeIn(GaussianSplatDataSource dataSource)
        {
            float fadeDuration = m_fadeDurationSeconds;
            dataSource.m_active = true;
            m_updateRenderableObjects = true;

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
            m_updateRenderableObjects = true;
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
        // Initialization
        // --------------------------------------------------------------------

        private void Initialize()
        {
            if (m_client != null)
            {
                return;
            }

            // Initialize the client instance
            m_client = new();
            m_scene = new Scene(m_client);
            m_assetManager = new AssetManager(m_client);
            // SceneMetadata must be created here (not as field initializer) because it's a SWIG type
            // that triggers P/Invoke on construction. Field initializers run before the native
            // library is loaded, causing "Plugin loading is only allowed on main thread" errors.
            m_sceneMetadata = new SceneMetadata
            {
                m_highestLodLimit = 1.0f,
                m_lowestLodLimit = 0.0f,
                m_lodMaxDistance = 20.0f,
                m_verticalOffset = 0.0f,
                m_splatCountBudget = 400000,
            };

            // Initialize client config.
            m_clientConfig = ClientConfig.Load();

            // Seed the viewer key for the default ENV from config resource; it may be dynamically changed thereafter
            string viewerKey = m_clientConfig.GetAssetViewerKey();
            if (viewerKey != null)
            {
                m_assetManager.SetViewerKey(viewerKey);
            }

            PreparePersistentDataDir(m_client);
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
            // Normal cleanup path for non-quit scenarios (e.g., scene changes, disabling in editor)
            MirisStream[] streams = m_streamToSceneObjectId.Keys.ToArray();
            foreach (MirisStream stream in streams)
            {
                RemoveStream(stream);
            }

            m_streamToSceneObjectId.Clear();
            m_clientConfig = null;

            m_scene?.Clear();

            // Teardown API objects
            m_assetManager = null;
            m_scene = null;
            m_sceneMetadata?.Dispose();
            m_sceneMetadata = null;

            // Teardown the client instance
            m_client.Dispose();
            m_client = null;
        }

        // --------------------------------------------------------------------
        // Unity event handling
        // --------------------------------------------------------------------

        protected void OnEnable()
        {
            Initialize();
        }

        protected void OnDisable()
        {
            Teardown();
        }

        protected void Start()
        {
            // Initialize client & scene state
            m_scene.SetMainCameraTransform(Camera.main.transform.localToWorldMatrix);
            m_scene.SetMainCameraViewFrustum(Camera.main);
        }

        protected void LateUpdate()
        {
            SyncClientParameters();
            SyncScene();
        }
    }
}
