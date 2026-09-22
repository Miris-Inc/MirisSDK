// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Threading.Tasks;

namespace Miris.Runtime
{
    /// <summary>
    /// A page of managed <see cref="AssetInfo"/> results together with the pagination cursors from the request.
    /// </summary>
    public class AssetsResultPage
    {
        public AssetInfo[] Assets;
        public string NextCursor;
        public string PrevCursor;

        public bool HasNextPage => !string.IsNullOrEmpty(NextCursor);
        public bool HasPrevPage => !string.IsNullOrEmpty(PrevCursor);
    }

    public class AssetManager : IDisposable
    {
        private readonly Client m_client;
        private StringVector m_tags = new StringVector();
        public event Action TagsChanged;

        // Fired when a caller-owned setting that affects GetAssets results (e.g. the dev panel's
        // asset limit) changes, so listeners (see MirisController) know to re-fetch. Unlike
        // TagsChanged, AssetManager does not itself own or store this value -- the caller passes
        // it into GetAssets() directly each time -- so there is no corresponding SetAssetLimit.
        public event Action AssetLimitChanged;

        public void NotifyAssetLimitChanged()
        {
            AssetLimitChanged?.Invoke();
        }

        internal AssetManager(Client client)
        {
            m_client = client;
        }

        public void Dispose()
        {
            m_tags?.Dispose();
            m_tags = null;
        }

        /// <summary>
        /// Get available assets from the server environment, together with pagination cursors.
        /// Pass the cursor from a previous result's NextCursor/PrevCursor along with the matching
        /// PageDirection to fetch the next/previous page. Leave both at their defaults for the first page.
        /// Runs blocking native call on background thread to avoid blocking Unity main thread.
        /// </summary>
        public Task<AssetsResultPage> GetAssets(int limit = 0, string cursor = "", PageDirection direction = PageDirection.None)
        {
            limit = Math.Max(0, limit);

            // Capture tag strings (not SWIG objects) for use in background thread
            var tagStrings = new string[(int)m_tags.Count];
            for (int i = 0; i < (int)m_tags.Count; i++)
            {
                tagStrings[i] = m_tags[i];
            }

            // Capture client handle for background thread
            var client = m_client;

            return Task.Run(() =>
            {
                // Create SWIG objects on the same thread where they'll be used
                using (var tags = new StringVector())
                {
                    foreach (var tag in tagStrings)
                    {
                        tags.Add(tag);
                    }

                    using (var result = client.GetAssetsPaginatedBlocking(tags, limit, cursor, direction))
                    {
                        // Extract the cursor strings and assets vector BEFORE disposing result -
                        // result owns the native memory for all of its members.
                        string nextCursor = result.m_nextCursor;
                        string prevCursor = result.m_prevCursor;

                        using (var nativeAssets = result.m_assets)
                        {
                            // Create new AssetInfo objects with copied string data
                            // We must read all string properties BEFORE disposing the vector
                            // because the vector owns the native memory for the strings
                            var assets = new AssetInfo[(int)nativeAssets.Count];
                            for (int i = 0; i < (int)nativeAssets.Count; i++)
                            {
                                var src = nativeAssets[i];
                                // Extract strings while native memory is still valid
                                string uuid = src.m_uuid;
                                string name = src.m_name;
                                string thumbnailUrl = src.m_thumbnailUrl;

                                // Copy tags into a new StringVector, pass to AssetInfo, then dispose
                                // The native AssetInfo constructor copies the vector data
                                var srcTags = src.m_tags;
                                using (var copiedTags = new StringVector())
                                {
                                    if (srcTags != null)
                                    {
                                        for (int j = 0; j < (int)srcTags.Count; j++)
                                        {
                                            copiedTags.Add(srcTags[j]);
                                        }
                                    }

                                    // Create a new AssetInfo that owns its own memory
                                    assets[i] = new AssetInfo(uuid, name, thumbnailUrl, copiedTags);
                                }
                            }

                            return new AssetsResultPage
                            {
                                Assets = assets,
                                NextCursor = nextCursor,
                                PrevCursor = prevCursor,
                            };
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Get all unique tags from available assets.
        /// Runs blocking native call on background thread to avoid blocking Unity main thread.
        /// </summary>
        public Task<string[]> GetAvailableTags()
        {
            var client = m_client;
            return Task.Run(() =>
            {
                using (var result = client.GetAvailableTags())
                {
                    var tags = new string[(int)result.Count];
                    for (int i = 0; i < (int)result.Count; i++)
                    {
                        tags[i] = result[i];
                    }
                    return tags;
                }
            });
        }

        public void SetViewerKey(string viewerKey)
        {
            m_client.SetAssetViewerKey(viewerKey);
        }

        public void SetTags(string[] tags)
        {
            m_tags.Clear();
            foreach (var tag in tags)
            {
                m_tags.Add(tag);
            }
            TagsChanged?.Invoke();
        }
    }
}
