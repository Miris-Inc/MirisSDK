// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Miris.Runtime
{

    /// <summary>
    /// 
    /// </summary>
    [ExecuteInEditMode]
    public class MirisStream : MonoBehaviour
    {
        [SerializeField, Tooltip("Non-optional. The MirisStreamController governing this object.")]
        public MirisStreamController m_streamController;

        [Tooltip("The ID of the asset to be streamed.")]
        public string m_assetId = "";

        // Debounce before demoting to Prefetch, to avoid thrashing near Opacity == 0.
        private const float c_prefetchDebounceSeconds = 0.1f;

        [SerializeField, Range(0.0f, 1.0f), Tooltip("Opacity applied to the whole asset. 1 is fully opaque, 0 is invisible.")]
        private float m_opacity = 1.0f;

        /// <summary>
        /// Tracks the previous asset id to handle changes 
        /// </summary>
        private string m_loadedAssetId = "";

        // Associated scene object.
        [NonSerialized]
        internal SceneObject m_sceneObject = null;

        [NonSerialized]
        internal SceneObject m_defaultCameraObject;
        [NonSerialized]
        internal SceneObject m_viewingVolumeObject;

        private const float c_relativeDeterminantEpsilon = 1e-6f;

        // A stream can be composed of multiple assets. This map allows you to obtain the render component associated with an asset
        [NonSerialized]
        private Dictionary<int, MirisAssetRenderComponent> m_modelRootObjectIdToRenderComponent = new();

        [NonSerialized]
        private bool m_firedLoadedActions = false;

        // Last fetch mode applied; avoids redundant SetStreamFetchMode calls. See SyncFetchMode.
        [NonSerialized]
        private StreamFetchMode m_lastAppliedFetchMode = StreamFetchMode.Regular;

        // How long Opacity has continuously read 0. See SyncFetchMode.
        [NonSerialized]
        private float m_zeroOpacitySeconds = 0f;

        // When true, fetch mode is always Regular regardless of opacity.
        public static bool PrefetchDisabledGlobally = false;

        [NonSerialized]
        private bool m_renderedExternallyValue = false;

        /// <summary>
        /// Whether something other than the SDK's own renderer draws this stream's splats - Shark,
        /// through SharkSplatRenderSystem. Set by MirisStreamController when the stream registers.
        /// </summary>
        public bool RenderedExternally
        {
            get => m_renderedExternallyValue;
            set
            {
                if (m_renderedExternallyValue == value)
                {
                    return;
                }
                m_renderedExternallyValue = value;

                foreach (MirisAssetRenderComponent renderComponent in m_modelRootObjectIdToRenderComponent.Values)
                {
                    renderComponent.SetRenderingSuppressed(value);
                }
            }
        }

        public List<Action> m_onLoadActions = new List<Action>();
        public List<Action> m_onLoadedActions = new List<Action>();
        public List<Action> m_onUnloadedActions = new List<Action>();

        public enum Status
        {
            Disabled,

            NoController,

            ControllerInactive,

            NoAssetId,

            NotLoaded,

            Streaming,

            NoData,

            Ready,

            Rendered
        }

        #region Public API
        public float Opacity
        {
            get => m_opacity;
            set => m_opacity = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Validates whether the underlying SceneObject is properly initialized.
        ///
        /// Note that this becomes true as soon as the stream is registered with the
        /// scene, which is well before any render data arrives. Use
        /// <see cref="GetStatus"/> to tell those apart.
        /// </summary>
        /// <returns>True if the underlying m_sceneObject is non-null, false otherwise</returns>
        public bool IsLoaded()
        {
            return m_sceneObject != null;
        }

        /// <summary>
        /// Reports how far this stream has progressed towards rendering. Each state is
        /// derived from current state on every call, so it always reflects the stream as
        /// it stands rather than the last transition someone remembered to record.
        /// </summary>
        /// <returns>The stream's current <see cref="Status"/></returns>
        public Status GetStatus()
        {
            if (!isActiveAndEnabled)
            {
                return Status.Disabled;
            }

            if (m_streamController == null)
            {
                return Status.NoController;
            }

            if (!m_streamController.IsActive())
            {
                return Status.ControllerInactive;
            }

            if (string.IsNullOrEmpty(m_assetId))
            {
                return Status.NoAssetId;
            }

            if (!IsLoaded())
            {
                return Status.NotLoaded;
            }

            // asset is loaded but there are no components yet
            if (m_modelRootObjectIdToRenderComponent.Count == 0)
            {
                return Status.Streaming;
            }

            // Any single render component drawing render data is enough to call the whole
            // stream rendered, matching how the bounds accessors treat components.
            bool hasValidAsset = false;
            foreach (MirisAssetRenderComponent renderComponent in m_modelRootObjectIdToRenderComponent.Values)
            {
                if (renderComponent.GetSplatCount() > 0)
                {
                    return Status.Rendered;
                }

                hasValidAsset |= renderComponent.IsAssetValid();
            }

            if (hasValidAsset && m_renderedExternallyValue)
            {
                return Status.Rendered;
            }

            return hasValidAsset ? Status.Ready : Status.NoData;
        }

        public Bounds GetObjectBounds()
        {
            // TODO: Cache this when data sources get updated

            Vector3 minBound = Vector3.positiveInfinity;
            Vector3 maxBound = Vector3.negativeInfinity;
            foreach (var renderComponent in m_modelRootObjectIdToRenderComponent.Values)
            {
                minBound = Vector3.Min(minBound, renderComponent.GetObjectBounds().min);
                maxBound = Vector3.Max(maxBound, renderComponent.GetObjectBounds().max);
            }

            Bounds bounds = new();
            bounds.SetMinMax(minBound, maxBound);
            return bounds;
        }

        public Bounds GetWorldBounds()
        {
            // The scene object only exists between registration and unload, so callers that run
            // outside that window (gizmos, framing polled before load) get an empty bounds rather
            // than a null dereference.
            if (!IsLoaded())
            {
                return new Bounds();
            }

            return m_sceneObject.GetWorldBoundingBox();
        }

        private bool TryGetWorldTransform(SceneObject sceneObject, out Matrix4x4 transform)
        {
            transform = Matrix4x4.identity;

            if (sceneObject == null || !IsLoaded())
            {
                return false;
            }

            transform = sceneObject.GetWorldTransform();
            return true;
        }

        public bool TryGetDefaultCameraWorldTransform(out Matrix4x4 transform)
        {
            return TryGetWorldTransform(m_defaultCameraObject, out transform);
        }

        public bool TryGetViewingVolumeBounds(out Bounds localBounds, out Matrix4x4 volumeToWorld)
        {
            localBounds = new Bounds();

            if (!TryGetWorldTransform(m_viewingVolumeObject, out volumeToWorld))
            {
                return false;
            }

            Vector3 lossyScale = volumeToWorld.lossyScale;
            float scaleMagnitude = Mathf.Max(
                Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
            float determinantEpsilon =
                c_relativeDeterminantEpsilon * scaleMagnitude * scaleMagnitude * scaleMagnitude;

            if (Mathf.Abs(volumeToWorld.determinant) < determinantEpsilon)
            {
                return false;
            }

            localBounds = m_viewingVolumeObject.GetLocalBoundingBox();
            return BoundsUtils.HasFiniteSize(localBounds);
        }

        public MirisAssetRenderComponent[] GetRenderComponents()
        {
            return m_modelRootObjectIdToRenderComponent.Values.ToArray();
        }

        /// <summary>
        /// How many ModelRoots this stream has populated so far. Separate from
        /// GetModelRootObjectIds because that allocates, and a caller watching for new ModelRoots
        /// every frame only needs the count.
        /// </summary>
        public int ModelRootCount => m_modelRootObjectIdToRenderComponent.Count;

        /// <summary>
        /// Scene object ids of the ModelRoots this stream has populated so far. An external renderer
        /// needs these to address a specific asset - a transform, for instance, belongs to a ModelRoot
        /// rather than to the stream, and one stream can populate more than one. Grows as the stream
        /// arrives, so callers should not cache it.
        /// </summary>
        public int[] GetModelRootObjectIds()
        {
            return m_modelRootObjectIdToRenderComponent.Keys.ToArray();
        }

        /// <summary>
        /// The ModelRoots this stream has populated so far, keyed by scene object id, together with
        /// the render component that carries each one's placement within the streamed scene.
        /// </summary>
        internal Dictionary<int, MirisAssetRenderComponent> ModelRoots => m_modelRootObjectIdToRenderComponent;

        #endregion

        // --------------------------------------------------------------------
        // Unity overrides
        // --------------------------------------------------------------------

        #region MonoBehaviour
        protected async void OnEnable()
        {
            if (m_streamController != null && m_streamController.isActiveAndEnabled)
            {
                // Only run this after MirisStreamController.OnEnable() has been called
                await LoadStream();
            }
        }

        protected void OnDisable()
        {
            ClearRenderResources();

            if (m_streamController != null && m_streamController.isActiveAndEnabled)
            {
                // Prevent this from being run if MirisStreamController.OnDisable() has been already called.
                UnloadStream();
                DeregisterController();
            }
        }

        protected async void Update()
        {
            RegisterController();
            await CheckContentChanged();
            foreach (var renderComponent in m_modelRootObjectIdToRenderComponent.Values)
            {
                renderComponent.Opacity = m_opacity;
                renderComponent.Update(transform);
            }

            CheckDataLoaded();
            SyncFetchMode();
        }

        protected void OnDrawGizmosSelected()
        {
            Bounds worldBounds = GetWorldBounds();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);

            if (TryGetViewingVolumeBounds(out Bounds volumeLocalBounds, out Matrix4x4 volumeToWorld))
            {
                Matrix4x4 previousMatrix = Gizmos.matrix;
                Gizmos.matrix = volumeToWorld;
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(volumeLocalBounds.center, volumeLocalBounds.size);
                Gizmos.matrix = previousMatrix;
            }

            if (TryGetDefaultCameraWorldTransform(out Matrix4x4 cameraTransform) && cameraTransform.ValidTRS())
            {
                float frustumLength = Mathf.Max(worldBounds.size.magnitude * 0.15f, 0.3f);

                Matrix4x4 previousMatrix = Gizmos.matrix;
                Gizmos.matrix = cameraTransform;
                Gizmos.color = Color.magenta;
                Gizmos.DrawFrustum(Vector3.zero, 60.0f, frustumLength, 0.0f, 16.0f / 9.0f);
                Gizmos.matrix = previousMatrix;
            }
        }
        #endregion

        // --------------------------------------------------------------------
        // Private
        // --------------------------------------------------------------------

        /// <summary>
        /// Loads the content with id m_assetId into the current stream.
        /// </summary>
        protected virtual async Task LoadStream()
        {
            Debug.Assert(m_streamController != null);

            if (string.IsNullOrEmpty(m_assetId))
            {
                MirisDebug.Log($"No Asset Id for {nameof(MirisStream)} on GameObject {name}, aborting");
                return;
            }

            // Returning without touching m_loadedAssetId, so CheckContentChanged retries next frame.
            if (!m_streamController.IsReadyForStreams())
            {
                return;
            }

            MirisDebug.Log($"Loading stream with asset {m_assetId}");
            m_loadedAssetId = m_assetId;

            // Invoke attempt load stream callbacks
            foreach (Action action in m_onLoadActions)
            {
                action.Invoke();
            }

            m_streamController.AddStreamById(this, m_assetId);

            // The octree always starts Regular; SyncFetchMode corrects it once one exists. Born
            // invisible skips the demote debounce, which is for mid-fade dips, not the initial state.
            m_lastAppliedFetchMode = StreamFetchMode.Regular;
            m_zeroOpacitySeconds = m_opacity > 0f ? 0f : c_prefetchDebounceSeconds;
        }

        /// <summary>
        /// Unloads content in the current MirisStreamController
        /// </summary>
        protected virtual void UnloadStream()
        {
            if (IsLoaded() && m_streamController != null)
            {
                ClearRenderResources();
                m_streamController.RemoveStream(this);
            }
            m_loadedAssetId = "";
            m_lastAppliedFetchMode = StreamFetchMode.Regular;
            m_zeroOpacitySeconds = 0f;

            // Arm the loaded callbacks again so that the next asset fires them.
            m_firedLoadedActions = false;

            // Invoke unloaded stream ballbacks
            foreach (Action action in m_onUnloadedActions)
            {
                action.Invoke();
            }
        }

        /// <summary>
        /// Invokes the loaded callbacks the first time this stream holds renderable data.
        ///
        /// There is no single point in the load path to hang this off: the controller
        /// creates render components and populates their data sources from its own
        /// LateUpdate, so arrival is detected by polling rather than announced.
        /// </summary>
        private void CheckDataLoaded()
        {
            if (m_firedLoadedActions)
            {
                return;
            }

            Status status = GetStatus();
            if (status != Status.Ready && status != Status.Rendered)
            {
                return;
            }

            m_firedLoadedActions = true;

            CheckForViewComponents();
            m_streamController.OnStreamLoaded(this);

            // Invoke loaded stream callbacks
            foreach (Action action in m_onLoadedActions)
            {
                action.Invoke();
            }
        }

        // Drives fetch mode from Opacity: >0 is Regular, 0 is Prefetch (debounced).
        private void SyncFetchMode()
        {
            if (m_streamController == null || !IsLoaded())
            {
                m_zeroOpacitySeconds = 0f;
                return;
            }

            if (PrefetchDisabledGlobally || m_opacity > 0f)
            {
                m_zeroOpacitySeconds = 0f;
                TryApplyFetchMode(StreamFetchMode.Regular);
                return;
            }

            m_zeroOpacitySeconds += Time.unscaledDeltaTime;
            if (m_zeroOpacitySeconds >= c_prefetchDebounceSeconds)
            {
                TryApplyFetchMode(StreamFetchMode.Prefetch);
            }
        }

        // No-op if already applied, or if the stream isn't ready yet (retried next frame).
        private void TryApplyFetchMode(StreamFetchMode desiredMode)
        {
            if (desiredMode == m_lastAppliedFetchMode)
            {
                return;
            }

            if (m_streamController.TrySetStreamFetchMode(this, desiredMode))
            {
                m_lastAppliedFetchMode = desiredMode;
            }
        }

        /// <summary>
        /// Checks whether the asset id has changed, and reloads the
        /// MirisStreamController if so.
        /// </summary>
        protected virtual async Task CheckContentChanged()
        {
            // prevents asset from entering a load loop if the stream controller is not active
            // but a streamID has already been set by the user ( i.e. it won't re-load as assetid has not changed)
            if (m_streamController == null || !m_streamController.IsActive())
            {
                return;
            }

            // Also here, not just in LoadStream: while the renderer is still coming up m_loadedAssetId
            // stays empty, which reads as a content change, and the UnloadStream below would fire the
            // unloaded callbacks once per frame for as long as the wait lasts.
            if (!m_streamController.IsReadyForStreams())
            {
                return;
            }

            if (m_assetId != m_loadedAssetId)
            {
                UnloadStream();
                await LoadStream();
            }
        }

        /// <summary>
        /// Finds a MirisStreamController to register with.
        /// </summary>
        /// <exception cref="UnityException"></exception>
        private void RegisterController()
        {
            if (m_streamController == null)
            {
                // TODO: We should remove the FindObjectsByType invocation if we can, it's pretty expensive.
                MirisStreamController[] streamControllers = FindObjectsByType<MirisStreamController>(FindObjectsSortMode.None);
                if (streamControllers.Length == 1)
                {
                    m_streamController = streamControllers[0];
                }
                else if (streamControllers.Length == 0)
                {
                    throw new UnityException($"Unable to find a {nameof(MirisStreamController)}, please create one in scene '{SceneManager.GetActiveScene().name}'.");
                }
                else
                {
                    throw new UnityException($"Found {streamControllers.Length} {nameof(MirisStreamController)}s, please only have one in your scene.");
                }
            }
        }

        /// <summary>
        /// Deregisters with the current MirisStreamController
        /// </summary>
        private void DeregisterController()
        {
            if (m_streamController == null)
            {
                return;
            }

            m_streamController = null;
        }

        internal MirisAssetRenderComponent CreateRenderComponent(int modelRootObjectId)
        {
            MirisAssetRenderComponent renderComponent = new(m_renderedExternallyValue);
            Debug.Assert(!m_modelRootObjectIdToRenderComponent.ContainsKey(modelRootObjectId));
            m_modelRootObjectIdToRenderComponent.Add(modelRootObjectId, renderComponent);
            return renderComponent;
        }

        internal MirisAssetRenderComponent GetRenderComponent(int modelRootObjectId)
        {
            return m_modelRootObjectIdToRenderComponent[modelRootObjectId];
        }

        private void ClearRenderResources()
        {
            foreach (var renderComponent in m_modelRootObjectIdToRenderComponent.Values)
            {
                renderComponent.Dispose();
            }
            m_modelRootObjectIdToRenderComponent.Clear();

            m_defaultCameraObject = null;
            m_viewingVolumeObject = null;
        }

        private void CheckForViewComponents()
        {
            Client client = m_streamController.GetClient();
            int defaultCameraId = m_sceneObject.GetDefaultCameraId();
            int viewingVolumeId = m_sceneObject.GetViewingVolumeId();

            m_defaultCameraObject = defaultCameraId >= 0 ? new SceneObject(client, defaultCameraId) : null;
            m_viewingVolumeObject = viewingVolumeId >= 0 ? new SceneObject(client, viewingVolumeId) : null;
        }

        /// <summary>
        /// Forces this stream into an unloaded state without going through OnDisable(). Used by
        /// MirisStreamController.Teardown() when its own OnEnable/OnDisable cycle re-fires (e.g.
        /// Editor Undo) without this MirisStream's OnDisable running in lockstep -- otherwise this
        /// stream keeps render components bound to the controller's now-destroyed Client.
        /// </summary>
        internal void ForceUnload()
        {
            ClearRenderResources();
            m_loadedAssetId = "";
            m_lastAppliedFetchMode = StreamFetchMode.Regular;
            m_zeroOpacitySeconds = 0f;
        }
    }
}
