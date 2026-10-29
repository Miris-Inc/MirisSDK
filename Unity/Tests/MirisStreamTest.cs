// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Threading.Tasks;
using UnityEngine;
using Miris.Runtime;

namespace Miris.Tests
{
    public class MirisStreamTest : MirisStream
    {
        [SerializeField, Tooltip("The URL to stream from.")]
        public string m_url = "";

        /// <summary>
        /// Tracks the previous URL to handle changes 
        /// </summary>
        private string m_loadedUrl = "";

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
                m_url = GetFormattedUrl(contentPath);
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
            return m_url;
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
        /// Checks whether the content URL has changed, and reloads the
        /// MirisStreamController if so.
        /// </summary>
        protected override Task CheckContentChanged()
        {
            string resolvedUrl = GetResolvedUrl();
            if (resolvedUrl != m_loadedUrl)
            {
                UnloadStream();
                LoadStream();
            }

            return Task.CompletedTask;
        }

        #region MirisStream Overrides
        protected override Task LoadStream()
        {
            Debug.Assert(m_streamController != null);

            if (m_url == "")
            {
                Debug.Log("Got empty URL, aborting");
            }
            else
            {
                string resolvedUrl = GetResolvedUrl();
                Debug.Log($"Loading stream at {resolvedUrl}");
                m_streamController.AddStream(this, resolvedUrl);
                m_loadedUrl = resolvedUrl;
            }

            return Task.CompletedTask;
        }

        protected override void UnloadStream()
        {
            base.UnloadStream();

            m_loadedUrl = "";
        }

        private string GetFormattedUrl(string contentPath)
        {
            if (!Uri.TryCreate(contentPath, UriKind.Absolute, out var uriResult) || !uriResult.IsAbsoluteUri)
            {
                return "";
            }

            return contentPath;
        }
        #endregion
    }
}