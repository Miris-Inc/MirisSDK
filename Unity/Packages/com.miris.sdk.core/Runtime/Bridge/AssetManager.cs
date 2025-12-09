// Copyright © 2025 Miris, Inc. All rights reserved.

using AOT;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Miris.Runtime
{
    public class AssetManager
    {
        private Client m_client;
        private string m_selectedEnvironment;
        public string SelectedEnvironment => m_selectedEnvironment;
        private string[] m_tags = new string[] { };
        public event Action<string> ServerEnvironmentChanged;
        public event Action TagsChanged;

        [MonoPInvokeCallback(typeof(FillNativeArrayCallback))]
        private static void GetAssetsCallback(IntPtr ptr, int count, IntPtr userData)
        {
            InteropUtils.NativeAsyncCallbackArray<AssetInfo>(ptr, count, userData);
        }

        [MonoPInvokeCallback(typeof(FillNativeArrayCallback))]
        private static void GetAvailableEnvironmentsCallback(IntPtr ptr, int count, IntPtr userData)
        {
            InteropUtils.NativeAsyncCallbackStringArray(ptr, count, userData);
        }

        [MonoPInvokeCallback(typeof(FillNativeArrayCallback))]
        private static void GetAvailableTagsCallback(IntPtr ptr, int count, IntPtr userData)
        {
            InteropUtils.NativeAsyncCallbackStringArray(ptr, count, userData);
        }

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

        public Task<AssetInfo[]> GetAssets()
        {
            var tcs = new TaskCompletionSource<AssetInfo[]>();

            var handle = GCHandle.Alloc(tcs);
            using (StringArrayInterop tagsInterop = new (m_tags))
            {
                m_client.GetAssets(tagsInterop.GetUnmanagedStringArray(), m_tags.Length, GetAssetsCallback, GCHandle.ToIntPtr(handle));
            }

            return tcs.Task;
        }

        public Task<string[]> GetAvailableTags()
        {
            var tcs = new TaskCompletionSource<string[]>();

            var handle = GCHandle.Alloc(tcs);
            m_client.GetAvailableTags(GetAvailableTagsCallback, GCHandle.ToIntPtr(handle));

            return tcs.Task;
        }

        public Task<string[]> GetAvailableEnvironments()
        {
            var tcs = new TaskCompletionSource<string[]>();

            var handle = GCHandle.Alloc(tcs);
            m_client.GetAvailableEnvironments(GetAvailableEnvironmentsCallback, GCHandle.ToIntPtr(handle));

            return tcs.Task;
        }

        public async Task<bool> SetServerEnvironment(string environment)
        {
            var tcs = new TaskCompletionSource<bool>();

            var handle = GCHandle.Alloc(tcs);
            m_client.SetServerEnvironment(environment, SetServerEnvironmentCallback, GCHandle.ToIntPtr(handle));

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
            m_tags = tags;
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
