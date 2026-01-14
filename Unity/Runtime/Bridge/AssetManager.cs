// Copyright © 2025 Miris, Inc. All rights reserved.

using AOT;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Miris.Runtime
{
    public class AssetManager : IDisposable
    {
        private readonly Client m_client;
        private string m_selectedEnvironment;
        public string SelectedEnvironment => m_selectedEnvironment;
        private StringVector m_tags = new StringVector();
        public event Action<string> ServerEnvironmentChanged;
        public event Action TagsChanged;

        [MonoPInvokeCallback(typeof(SetServerEnvironmentCallback))]
        private static void SetServerEnvironmentCallback(bool success, IntPtr userData)
        {
            var handle = GCHandle.FromIntPtr(userData);
            var tcs = (TaskCompletionSource<bool>)handle.Target;
            handle.Free();
            tcs.SetResult(success);
        }

        internal AssetManager(Client client)
        {
            m_client = client;
            m_selectedEnvironment = GetDefaultEnvironment();
        }

        public void Dispose()
        {
            m_tags?.Dispose();
            m_tags = null;
        }

        /// <summary>
        /// Get all available assets from the server environment.
        /// Runs blocking native call on background thread to avoid blocking Unity main thread.
        /// </summary>
        public Task<AssetInfo[]> GetAssets()
        {
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

                    using (var result = client.GetAssets(tags))
                    {
                        // Create new AssetInfo objects with copied string data
                        // We must read all string properties BEFORE disposing the vector
                        // because the vector owns the native memory for the strings
                        var assets = new AssetInfo[(int)result.Count];
                        for (int i = 0; i < (int)result.Count; i++)
                        {
                            var src = result[i];
                            // Extract strings while native memory is still valid
                            string uuid = src.m_uuid;
                            string name = src.m_name;
                            string contentUrl = src.m_contentUrl;
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
                                assets[i] = new AssetInfo(uuid, name, contentUrl, thumbnailUrl, copiedTags);
                            }
                        }
                        return assets;
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

        /// <summary>
        /// Get all available server environment names.
        /// Runs blocking native call on background thread to avoid blocking Unity main thread.
        /// </summary>
        public Task<string[]> GetAvailableEnvironments()
        {
            var client = m_client;
            return Task.Run(() =>
            {
                using (var result = client.GetAvailableEnvironments())
                {
                    var environments = new string[(int)result.Count];
                    for (int i = 0; i < (int)result.Count; i++)
                    {
                        environments[i] = result[i];
                    }
                    return environments;
                }
            });
        }

        public async Task<bool> SetServerEnvironment(string environment)
        {
            var tcs = new TaskCompletionSource<bool>();

            var handle = GCHandle.Alloc(tcs);
            m_client.SetServerEnvironmentAsync(environment, SetServerEnvironmentCallback, GCHandle.ToIntPtr(handle));

            bool result = await tcs.Task;

            if (result)
            {
                m_selectedEnvironment = environment;
                ServerEnvironmentChanged?.Invoke(environment);
            }

            return result;
        }

        public void SetViewerKey(string viewerKey)
        {
            m_client.SetAssetViewerKey(viewerKey);
            ServerEnvironmentChanged?.Invoke(m_selectedEnvironment);
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

        public string GetDefaultEnvironment()
        {
            IntPtr ptr = m_client.GetDefaultEnvironment();
            return Marshal.PtrToStringAnsi(ptr);
        }

        public Task<bool> ResetEnvironmentToDefault()
        {
            return SetServerEnvironment(GetDefaultEnvironment());
        }
    }
}
