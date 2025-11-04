using System;
using UnityEngine;

namespace Aqua.Runtime
{

    /// <summary>
    /// 
    /// </summary>
    [ExecuteInEditMode]
    public class MirisStream : MonoBehaviour
    {
        [SerializeField, Tooltip("Non-optional. The MirisStreamController governing this object.")]
        public MirisStreamController m_streamController;

        [SerializeField, Tooltip("The URL to stream from.")]
        public string m_url = "";

        /// <summary>
        /// Tracks the previous URL to handle changes 
        /// </summary>
        private string m_loadedUrl = "";

        // Associated scene object.
        [NonSerialized]
        public AquaSceneObject m_sceneObject = null;


        #region Public API
        /// <summary>
        /// Sets a new URL to stream from. If this script is enabled, on the 
        /// next Update loop, the previous content will be unloaded, and the 
        /// new content loaded.
        /// </summary>
        /// <param name="contentPath">New URL or content path to load from</param>
        /// <param name="experimentalPath">Miris-internal parameter</param>
        public void SetUrlFromContentPath(string contentPath, bool experimentalPath = false)
        {
            Debug.Assert(m_streamController != null);

            if (contentPath != "")
            {
                m_url = m_streamController.GetFormattedUrl(contentPath);
            }
            else
            {
                m_url = "";
            }
        }

        /// <summary>
        /// Uses the MirisStreamController to resolve the current content URL, 
        /// expanding known variables.
        /// See <see cref="MirisStreamController.ResolveUrl"/>
        /// </summary>
        /// <returns>The resolved, expanded URL</returns>
        public string GetResolvedUrl()
        {
            return m_streamController.ResolveUrl(m_url);
        }

        /// <summary>
        /// Clears the current content URL. If this script is enabled, on the 
        /// next Update loop, the previous content will be unloaded.
        /// </summary>
        public void ClearUrl()
        {
            m_url = "";
        }

        /// <summary>
        /// Reloads the current content URL. If this script is enabled, on the 
        /// next Update loop, the current content will be re-loaded
        /// </summary>
        public void Reload()
        {
            m_loadedUrl = "";
        }

        /// <summary>
        /// Validates whether the underlying AquaSceneObject is properly initialized
        /// </summary>
        /// <returns>True if the underlying m_sceneObject is non-null, false otherwise</returns>
        public bool IsLoaded()
        {
            return m_sceneObject != null;
        }

        #endregion

        // --------------------------------------------------------------------
        // Unity overrides
        // --------------------------------------------------------------------

        #region MonoBehaviour
        protected void OnEnable()
        {
            // Temp silliness until we get rid of the render component as a required element
            {
                var renderComponent = GetComponent<GaussianSplatRenderComponent>();
                if (renderComponent != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(renderComponent);
                    }
                    else
                    {
                        DestroyImmediate(renderComponent);
                    }
                }
            }

            RegisterController();
            LoadStream();
        }

        protected void OnDisable()
        {
            // Skip cleanup if application is quitting - native client may already be destroyed
            if (m_streamController != null && m_streamController.IsApplicationQuitting)
            {
                return;
            }

            UnloadStream();
            DeregisterController();
        }

        protected void Update()
        {
            if (m_streamController == null)
            {
                return;
            }

            CheckUrlChanged();
        }
        #endregion

        // --------------------------------------------------------------------
        // Private
        // --------------------------------------------------------------------

        /// <summary>
        /// Loads the content at m_url into the current stream.
        /// </summary>
        private void LoadStream()
        {
            Debug.Assert(m_streamController != null);

            if (m_url == "")
            {
                Debug.Log("Got empty URL, aborting");
                return;
            }

            string resolvedUrl = GetResolvedUrl();
            Debug.Log($"Loading stream at {resolvedUrl}");
            m_streamController.AddStream(this, resolvedUrl);
            m_loadedUrl = resolvedUrl;
        }

        /// <summary>
        /// Unloads content in the current MirisStreamController
        /// </summary>
        private void UnloadStream()
        {
            if (IsLoaded() && m_streamController != null)
            {
                m_streamController.RemoveStream(this);
                m_loadedUrl = "";
            }
        }

        /// <summary>
        /// Checks whether the content URL has changed, and reloads the
        /// MirisStreamController if so.
        /// </summary>
        private void CheckUrlChanged()
        {
            string resolvedUrl = GetResolvedUrl();
            if (resolvedUrl != m_loadedUrl)
            {
                UnloadStream();
                LoadStream();
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
                    throw new UnityException("Unable to find any MirisStreamControllers, please create one in your scene.");
                }
                else
                {
                    throw new UnityException($"Found {streamControllers.Length} MirisStreamControllers, please only have one in your scene.");
                }
            }

            Debug.Log($"Miris Stream '{name}' found Controller '{m_streamController.name}'");
            m_streamController.Initialize();
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
    }
}
