// Copyright © 2025 Miris, Inc. All rights reserved.

using AOT;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using UnityEngine;

namespace Miris.Runtime
{
    
    /// <summary>
    /// Scene provides access to the C++ Scene without exposing the native C bindings
    /// </summary>
    public class Scene
    {
        private Client m_client;

        internal Scene(Client client)
        {
            m_client = client;
        }

        public SceneObject AddStream(string streamName, string url, bool doNotRefine=false)
        {
            int streamObjectId = m_client.AddStream(streamName, url, MirisApi.UNITY_CLIENT, doNotRefine);
            return GetSceneObject(streamObjectId);
        }

        class AddStreamCallbackUserdata
        {
            public TaskCompletionSource<SceneObject> m_tcs;
            public Client m_client;
        }

        [MonoPInvokeCallback(typeof(AddStreamCallback))]
        private static void NativeAsyncAddStreamCallback(int streamId, System.IntPtr userData)
        {
            var handle = GCHandle.FromIntPtr(userData);
            var castedData = (AddStreamCallbackUserdata)handle.Target;
            handle.Free();

            if (castedData != null)
            {
                castedData.m_tcs.SetResult(new(castedData.m_client, streamId));
            }
        }

        public Task<SceneObject> AddStreamById(string streamName, string uuid, bool doNotRefine=false)
        {
            var tcs = new TaskCompletionSource<SceneObject>();

            if (string.IsNullOrEmpty(uuid))
            {
                tcs.SetResult(null);
            }
            else
            {
                var handle = GCHandle.Alloc(new AddStreamCallbackUserdata() { m_tcs = tcs, m_client = m_client });
                m_client.AddStreamById(streamName, uuid, MirisApi.UNITY_CLIENT, doNotRefine, NativeAsyncAddStreamCallback, GCHandle.ToIntPtr(handle));
            }

            return tcs.Task;
        }

        public bool RemoveStream(SceneObject streamObject)
        {
            return m_client.RemoveStream(streamObject.GetId());
        }

        public SceneObject GetRootObject()
        {
            int sceneRootId = m_client.GetSceneRootObjectId();
            return GetSceneObject(sceneRootId);
        }

        public SceneObject GetSceneObject(int sceneObjectId)
        {
            return new SceneObject(m_client, sceneObjectId);
        }

        public void SetMainCameraTransform(Matrix4x4 cameraTransform)
        {
            float[] matrixArray = ValueConversion.MatrixToFloatArray(cameraTransform);
            m_client.SetMainCameraTransform(matrixArray);
        }

        public void SetMainCameraViewFrustum(Camera camera)
        {
            m_client.SetMainCameraViewFrustum(camera.aspect, camera.fieldOfView, camera.nearClipPlane, camera.farClipPlane);
        }

        public int GetCameraCount()
        {
            return m_client.GetCameraCount();
        }

        public void GetCameraIds(int[] cameraIndices)
        {
            m_client.GetCameraIds(cameraIndices);
        }

        public void Clear()
        {
            m_client.ClearScene();
        }

        public void UpdateExecution()
        {
            m_client.UpdateSceneExecution();
        }

        public void WaitForExecution()
        {
            m_client.WaitForSceneExecution();
        }

        public void GetLodMinMaxIndices(out int minLodIndex, out int maxLodIndex)
        {
            minLodIndex = 0;
            maxLodIndex = 0;
            m_client.GetLodMinMaxIndices(ref minLodIndex, ref maxLodIndex);
        }

        public void GetMetadata(out SceneMetadata metadata){
            metadata = new SceneMetadata();
            m_client.GetSceneMetadata(ref metadata);
        }
    }
}
