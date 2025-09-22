// Copyright © 2024 Miris. All rights reserved.

// Unity engine
using UnityEngine;

// Standard library
using System;
using System.Collections;
using System.Collections.Generic;

namespace Aqua.Runtime
{
    public abstract class BaseObjectAdapter
    {
        public abstract GameObject Populate(int sceneObjectId, AquaSceneObject sceneObject, MirisStreamController streamController);

        public abstract void SetActive(GameObject gameObject, bool activeState, MirisStreamController streamController);

        public virtual void SetDirty(GameObject gameObject, AquaSceneObject sceneObject, SceneObjectModifyFlagState flagState)
        {
            if(flagState.HasFlag(SceneObjectModifyFlag.TRANSFORM)){
                Matrix4x4 assetMatrix = sceneObject.GetTransform();

                gameObject.transform.localPosition = assetMatrix.GetPosition();
                gameObject.transform.localRotation = assetMatrix.rotation;
                gameObject.transform.localScale = assetMatrix.lossyScale;
            }
        }

        public abstract IEnumerator RemoveOverTime<T>(GameObject gameObject, MirisStreamController streamController, float? duration = 0.7f, Action<T> callback = null, T value = default(T));

        public abstract void Destroy(GameObject gameObject);
    }
}
