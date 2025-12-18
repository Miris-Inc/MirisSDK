// Copyright © 2025 Miris, Inc. All rights reserved.

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

        /// <summary>
        /// Tracks the previous asset id to handle changes 
        /// </summary>
        private string m_loadedAssetId = "";

        // Associated scene object.
        [NonSerialized]
        internal SceneObject m_sceneObject = null;

        // A stream can be composed of multiple assets. This map allows you to obtain the render component associated with an asset
        [NonSerialized]
        private Dictionary<int, GaussianSplatRenderComponent> m_assetRootObjectIdToRenderComponent = new();

        public List<Action> m_onLoadActions = new List<Action>();
        public List<Action> m_onUnloadedActions = new List<Action>();

        #region Public API
        /// <summary>
        /// Validates whether the underlying SceneObject is properly initialized
        /// </summary>
        /// <returns>True if the underlying m_sceneObject is non-null, false otherwise</returns>
        public bool IsLoaded()
        {
            return m_sceneObject != null;
        }

        public Bounds GetObjectBounds()
        {
            // TODO: Cache this when data sources get updated

            Vector3 minBound = Vector3.positiveInfinity;
            Vector3 maxBound = Vector3.negativeInfinity;
            foreach (var renderComponent in m_assetRootObjectIdToRenderComponent.Values)
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
            // TODO: Cache this when data sources get updated

            Vector3 minBound = Vector3.positiveInfinity;
            Vector3 maxBound = Vector3.negativeInfinity;
            foreach (var renderComponent in m_assetRootObjectIdToRenderComponent.Values)
            {
                minBound = Vector3.Min(minBound, renderComponent.GetWorldBounds().min);
                maxBound = Vector3.Max(maxBound, renderComponent.GetWorldBounds().max);
            }

            Bounds bounds = new();
            bounds.SetMinMax(minBound, maxBound);
            return bounds;
        }

        public GaussianSplatRenderComponent[] GetRenderComponents()
        {
            return m_assetRootObjectIdToRenderComponent.Values.ToArray();
        }

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
            foreach (var renderComponent in m_assetRootObjectIdToRenderComponent.Values)
            {
                renderComponent.Update(transform);
            }
        }

        protected void OnDrawGizmosSelected()
        {
            Bounds worldBounds = GetWorldBounds();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
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

            MirisDebug.Log($"Loading stream with asset {m_assetId}");
            m_loadedAssetId = m_assetId;

            // Invoke attempt load stream ballbacks
            foreach (Action action in m_onLoadActions)
            {
                action.Invoke();
            }

            await m_streamController.AddStreamById(this, m_assetId);
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

            // Invoke unloaded stream ballbacks
            foreach (Action action in m_onUnloadedActions)
            {
                action.Invoke();
            }
        }

        /// <summary>
        /// Checks whether the asset id has changed, and reloads the
        /// MirisStreamController if so.
        /// </summary>
        protected virtual async Task CheckContentChanged()
        {
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

        internal GaussianSplatRenderComponent CreateRenderComponent(int assetRootObjectId)
        {
            GaussianSplatRenderComponent renderComponent = new();
            Debug.Assert(!m_assetRootObjectIdToRenderComponent.ContainsKey(assetRootObjectId));
            m_assetRootObjectIdToRenderComponent.Add(assetRootObjectId, renderComponent);
            return renderComponent;
        }

        internal GaussianSplatRenderComponent GetRenderComponent(int assetRootObjectId)
        {
            return m_assetRootObjectIdToRenderComponent[assetRootObjectId];
        }

        private void ClearRenderResources()
        {
            foreach (var renderComponent in m_assetRootObjectIdToRenderComponent.Values)
            {
                renderComponent.Dispose();
            }
            m_assetRootObjectIdToRenderComponent.Clear();
        }
    }
}
