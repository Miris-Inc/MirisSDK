// Copyright © 2024 Miris. All rights reserved.

// Unity engine
using UnityEngine;

// Standard library
using System;
using System.Collections;
using System.Collections.Generic;

namespace Aqua.Runtime
{
    public class AssetRootAdapter : BaseObjectAdapter
    {
        public override GameObject Populate(int sceneObjectId, AquaSceneObject sceneObject, MirisStreamController streamController)
        {
            GameObject newGameObject = new GameObject(sceneObject.GetName());
            streamController.SetParent(sceneObject, newGameObject);
            newGameObject.AddComponent(typeof(AquaAssetRoot));

            streamController.GetAssetMetadata(sceneObject, newGameObject);
            
            TileCreationMode tileMode = streamController.GetTileCreationMode();
            if (tileMode == TileCreationMode.MergeTilesToOneObject)
            {
                newGameObject.AddComponent(typeof(GaussianSplatRenderComponent));
            }

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

        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterAdapter()
        {
            SceneObjectAdapterRegistry.s_instance.RegisterAdapter(SceneObjectType.AssetRootObject, () => new AssetRootAdapter());
        }
    }
}
