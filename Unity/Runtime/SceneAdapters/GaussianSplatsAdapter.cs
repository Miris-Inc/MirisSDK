// Copyright © 2024 Miris. All rights reserved.

// Unity engine
using UnityEngine;

// Standard library
using System;
using System.Collections;
using System.Collections.Generic;

namespace Aqua.Runtime
{
    public class GaussianSplatsAdapter : BaseObjectAdapter
    {
        private Dictionary<GameObject, Coroutine> m_fadeCoroutines = new();
        private IEnumerator FadeIn(GameObject currentGameObject, MirisStreamController streamController)
        {
            float fadeDuration = streamController.m_fadeDurationSeconds;
            currentGameObject.SetActive(true);
            streamController.SetUpdateRenderableObjects(true);

            GaussianSplatAquaDataSource dataSource = currentGameObject.GetComponent<GaussianSplatAquaDataSource>();

            if (fadeDuration > 0.0)
            {
                while (dataSource.m_opacity < 1.0)
                {
                    float timeFactor = Time.deltaTime / fadeDuration;
                    dataSource.m_opacity += timeFactor;
                    dataSource.m_opacity = Math.Min(dataSource.m_opacity, 1.0f);
                    yield return null;
                }
            }
            else
            {
                dataSource.m_opacity = 1.0f;
            }
        }

        private IEnumerator FadeOut(GameObject currentGameObject, MirisStreamController streamController)
        {
            float fadeDuration = streamController.m_fadeDurationSeconds;
            currentGameObject.SetActive(true);

            GaussianSplatAquaDataSource dataSource = currentGameObject.GetComponent<GaussianSplatAquaDataSource>();

            if (fadeDuration > 0.0)
            {
                while (dataSource.m_opacity > 0.0)
                {
                    float timeFactor = Time.deltaTime / fadeDuration;
                    dataSource.m_opacity -= timeFactor;
                    dataSource.m_opacity = Math.Max(dataSource.m_opacity, 0.0f);
                    yield return null;
                }
            }
            else
            {
                dataSource.m_opacity = 0.0f;
            }

            currentGameObject.SetActive(false);
            streamController.SetUpdateRenderableObjects(true);
        }


        public override GameObject Populate(int sceneObjectId, AquaSceneObject sceneObject, MirisStreamController streamController)
        {
            GameObject newGameObject = new GameObject(sceneObject.GetName());
            streamController.SetParent(sceneObject, newGameObject);

            TileCreationMode tileMode = streamController.GetTileCreationMode();
            switch (tileMode)
            {
                case TileCreationMode.MergeTilesToOneObject:
                    {
                        GaussianSplatAquaDataSource dataSourceComponent =
                            newGameObject.AddComponent(typeof(GaussianSplatAquaDataSource)) as GaussianSplatAquaDataSource;
                        dataSourceComponent.m_opacity = 0.0f;
                        dataSourceComponent.m_data.m_object = sceneObject;
                        dataSourceComponent.m_data.m_objectIdColor = GameObjectUtils.HashGameObjectToColor(newGameObject);
                        break;
                    }
                case TileCreationMode.OneObjectPerTile:
                    {
                        GaussianSplatAquaDataSource dataSourceComponent =
                            newGameObject.AddComponent(typeof(GaussianSplatAquaDataSource)) as GaussianSplatAquaDataSource;
                        dataSourceComponent.m_opacity = 0.0f;
                        dataSourceComponent.m_data.m_object = sceneObject;
                        dataSourceComponent.m_data.m_objectIdColor = GameObjectUtils.HashGameObjectToColor(newGameObject);
                        break;
                    }

                default:
                    {
                        throw new ArgumentOutOfRangeException($"{nameof(TileCreationMode)}.{tileMode.ToString()} not supported!");
                    }
            }

            Matrix4x4 assetMatrix = sceneObject.GetTransform();
            if (assetMatrix.ValidTRS())
            {
                newGameObject.transform.localPosition = assetMatrix.GetPosition();
                newGameObject.transform.localRotation = assetMatrix.rotation;
                newGameObject.transform.localScale = assetMatrix.lossyScale;
            }

            if (streamController.m_enableStreamVisualization)
            {
                // Create an temporary object that is removed after some time.
                GameObject visualizationObject = new GameObject(sceneObject.GetName() + "_request");
                streamController.SetParent(sceneObject, visualizationObject);
                SceneObjectBoundsRenderComponent bboxComponent =
                    visualizationObject.AddComponent(typeof(SceneObjectBoundsRenderComponent)) as SceneObjectBoundsRenderComponent;
                bboxComponent.m_sceneObject = sceneObject;
                visualizationObject.transform.localPosition = assetMatrix.GetPosition();
                visualizationObject.transform.localRotation = assetMatrix.rotation;
                visualizationObject.transform.localScale = assetMatrix.lossyScale;

                newGameObject.GetComponentInParent<MirisStream>().StartCoroutine(DeleteLater(visualizationObject));
            }

            return newGameObject;

        }

        private void CheckStopCoroutine(GameObject gameObject, MirisStream stream)
        {
            if (m_fadeCoroutines.TryGetValue(gameObject, out Coroutine coroutine))
            {
                // The coroutine may have concluded due to 0 fadeout duration.
                if (coroutine != null)
                {
                    stream.StopCoroutine(coroutine);
                }
            }
        }

        public override void SetActive(GameObject gameObject, bool activeState, MirisStreamController streamController)
        {
            GaussianSplatAquaDataSource dataSource = gameObject.GetComponent<GaussianSplatAquaDataSource>();

            MirisStream stream = gameObject.GetComponentInParent<MirisStream>();
            CheckStopCoroutine(gameObject, stream);

            if (activeState)
            {
                m_fadeCoroutines[gameObject] = stream.StartCoroutine(FadeIn(gameObject, streamController));
            }
            else
            {
                m_fadeCoroutines[gameObject] = stream.StartCoroutine(FadeOut(gameObject, streamController));
            }
        }

        public override void SetDirty(GameObject gameObject, AquaSceneObject sceneObject, SceneObjectModifyFlagState flagState)
        {
            base.SetDirty(gameObject, sceneObject, flagState);
            if(flagState.HasFlag(SceneObjectModifyFlag.ARRAYS)){
                GaussianSplatAquaDataSource dataSourceComponent =
                    gameObject.GetComponent(typeof(GaussianSplatAquaDataSource)) as GaussianSplatAquaDataSource;
                if (dataSourceComponent != null)
                {
                    dataSourceComponent.m_data.m_dirty = true;
                }
            }
        }

        private IEnumerator DeleteLater(GameObject gameObject, float duration = 0.5f)
        {
            while (duration > 0)
            {
                duration -= Time.deltaTime;
                yield return null;
            } 

            GameObject.Destroy(gameObject);
        }

        public override IEnumerator RemoveOverTime<T>(GameObject gameObject, MirisStreamController streamController, float? duration = 0.7f, Action<T> callback = null, T value = default(T))
        {
            if(gameObject.activeSelf){
                float fadeDuration = duration.HasValue ?  duration.Value : 1.0f;

                GaussianSplatAquaDataSource dataSource = gameObject.GetComponent<GaussianSplatAquaDataSource>();

                if (fadeDuration > 0.0)
                {
                    while (dataSource.m_opacity > 0.0)
                    {
                        float timeFactor = Time.deltaTime / fadeDuration;
                        dataSource.m_opacity -= timeFactor;
                        dataSource.m_opacity = Math.Max(dataSource.m_opacity, 0.0f);
                        yield return null;
                    }
                }
                else
                {
                    dataSource.m_opacity = 0.0f;
                }

                gameObject.SetActive(false);
                streamController.SetUpdateRenderableObjects(true);
            }
            callback?.Invoke(value);
        }


        public override void Destroy(GameObject gameObject)
        {

        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterAdapter()
        {
            SceneObjectAdapterRegistry.s_instance.RegisterAdapter(SceneObjectType.GaussianSplats, () => new GaussianSplatsAdapter());
        }
    }
}
