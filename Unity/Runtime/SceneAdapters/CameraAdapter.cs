// Copyright © 2024 Miris. All rights reserved.

// Unity engine
using UnityEngine;

// Standard library
using System;
using System.Collections;
using System.Collections.Generic;

namespace Aqua.Runtime
{

    public class CameraAdapter : BaseObjectAdapter
    {
        private Dictionary<GameObject, Camera> m_cameras = new();

        public List<Camera> GetCameras()
        {
            List<Camera> cameras = new List<Camera>();
            foreach (KeyValuePair<GameObject, Camera> entity in m_cameras)
            {
                cameras.Add(entity.Value);
            }
            return cameras;
        }

        public Camera GetCameraByName(string name)
        {
            foreach(KeyValuePair<GameObject, Camera> entity in m_cameras)
            {
                if(entity.Key.name == name){
                    return entity.Value;
                }
            }
            return Camera.main;
        }

        private void PrepareCamera(Camera camera)
        {
            camera.enabled = false;
            camera.clearFlags = Camera.main.clearFlags;
            camera.backgroundColor = Camera.main.backgroundColor;
            camera.cullingMask = Camera.main.cullingMask;
        }

        public override GameObject Populate(int sceneObjectId, AquaSceneObject sceneObject, MirisStreamController streamController)
        {
            AquaCamera cameraObject = streamController.GetScene().GetCameraObject(sceneObjectId);
            GameObject newGameObject = new GameObject(cameraObject.GetName());
            Camera cameraComponent = newGameObject.AddComponent<Camera>();
            streamController.SetParent(sceneObject, newGameObject);
            cameraComponent.fieldOfView = cameraObject.GetFieldOfView();
            PrepareCamera(cameraComponent);
            m_cameras.Add(newGameObject, cameraComponent);

            Matrix4x4 assetMatrix = sceneObject.GetTransform();
            if (assetMatrix.ValidTRS())
            {
                newGameObject.transform.localPosition = assetMatrix.GetPosition();
                newGameObject.transform.localRotation = assetMatrix.rotation;
                newGameObject.transform.localScale = assetMatrix.lossyScale;
            }

            return newGameObject;
        }

        public override void SetActive(GameObject gameObject, bool activeState, MirisStreamController streamController)
        {
            gameObject.SetActive(activeState);
        }

        public override void SetDirty(GameObject gameObject, AquaSceneObject sceneObject, SceneObjectModifyFlagState flagState)
        {
            base.SetDirty(gameObject, sceneObject, flagState);
        }

        public override IEnumerator RemoveOverTime<T>(GameObject gameObject, MirisStreamController streamController, float? duration = 0.7f, Action<T> callback = null, T value = default(T))
        {
            yield return null;
            callback?.Invoke(value);
        }

        public override void Destroy(GameObject gameObject)
        {
            if(m_cameras.ContainsKey(gameObject)){
                m_cameras.Remove(gameObject);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterAdapter()
        {
            SceneObjectAdapterRegistry.s_instance.RegisterAdapter(SceneObjectType.Camera, () => new CameraAdapter());
        }
    }
}
