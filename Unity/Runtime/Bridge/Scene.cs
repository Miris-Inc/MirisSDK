// Copyright © 2026 Miris, Inc. All rights reserved.

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
            int streamObjectId = m_client.AddStream(streamName, url, doNotRefine);
            return GetSceneObject(streamObjectId);
        }

        public SceneObject AddStreamById(string streamName, string uuid, bool doNotRefine=false)
        {
            int streamObjectId = m_client.AddStreamById(streamName, uuid, doNotRefine);
            return GetSceneObject(streamObjectId);
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
            m_client.GetLodMinMaxIndices(out minLodIndex, out maxLodIndex);
        }

        public void GetMetadata(SceneMetadata metadata){
            m_client.GetSceneMetadata(metadata);
        }
    }
}
