// Copyright © 2025 Miris, Inc. All rights reserved.

// Standard library
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.UI;

using TMPro;


namespace Miris.Runtime
{
    [System.Serializable]
    public class SceneAssetInfo
    {
        public string m_assetName;
        public string m_assetId;
    }

    public class SceneSelector : MonoBehaviour
    {
        [SerializeField]
        private GameObject m_selectableAssetPrefab;

        [SerializeField]
        private Material m_thumbnailLoadingMaterial;

        [SerializeField]
        private Material m_thumbnailLoadedMaterial;

        private List<GameObject> m_assetButtonObjects = new List<GameObject>();
        private List<RectTransform> m_assetButtonRectTransforms = new();

        [SerializeField]
        private ScrollRect m_scrollRect;

        private RectTransform m_scrollRectViewport;

        private Coroutine m_periodicPrefetcher;
        private HashSet<string> m_prefetchedAssetUrls = new();

        [SerializeField]
        private MirisStreamController m_streamController;

        public Texture2D m_defaultThumbnail;

        public Texture2D m_loadingThumbnail;

        private float? m_assetsRefreshStart = null;

        private uint m_assetRequestId = 0;

        private AssetIterator m_assetIterator = new AssetIterator();

        [SerializeField]
        private UiUtils m_uiUtils;

        [SerializeField]
        private bool m_enableText = false;

        private static Sprite LoadSprite(Texture2D texture)
        {
            texture.mipMapBias = -1;
            return Sprite.Create(
                texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );
        }

        private void SetLoadingSprite(Image image)
        {
            image.sprite = LoadSprite(m_loadingThumbnail);
            image.material = m_thumbnailLoadingMaterial;
        }

        private void Awake()
        {
            m_assetsRefreshStart = Time.time;
            if(m_thumbnailLoadingMaterial == null)
            {
                m_thumbnailLoadingMaterial = Resources.Load<Material>("Materials/RoundedBoxSwipeLoading");
            }
            if(m_loadingThumbnail == null)
            {
                m_loadingThumbnail = Resources.Load<Texture2D>("Sprites/miris_player_loading_thumbnail");
            }
            LoadInitialPlaceholders(4);
        }

        private void Start()
        {
            if (m_scrollRect != null)
            {
                m_scrollRectViewport = m_scrollRect.viewport;
            }

            Initialize();
        }

        private async Task FetchAndUpdateImage(Image image, TMP_Text text, string url, Material loadedMaterial, Texture2D defaultThumbnail)
        {
            if (string.IsNullOrEmpty(url))
            {
                text.enabled = true;
                image.material = loadedMaterial;
                return;
            }

            try
            {
                Texture2D texture = await m_uiUtils.FetchTextureAsync(url);
                if (texture != null)
                {
                    texture.filterMode = FilterMode.Bilinear;
                    image.sprite = LoadSprite(texture);
                    image.material = loadedMaterial;
                } else {
                    image.sprite = LoadSprite(defaultThumbnail);
                }

                text.enabled = texture == null || m_enableText;
                image.material = loadedMaterial;

            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private async Task RevealButtonWhenReady(int timeoutMs, GameObject buttonObj, Image image, TMP_Text text, string url, Material loadedMaterial, Texture2D defaultThumbnail)
        {
            await Task.WhenAny(
                FetchAndUpdateImage(image, text, url, loadedMaterial, defaultThumbnail),
                Task.Delay(timeoutMs)
            );

            // Visually pleasing trick: reveal thumbnail finally
            buttonObj.SetActive(true);
        }

        private void LoadInitialPlaceholders(int placeholderCount) {
            for (int i = 0; i < placeholderCount; ++i)
            {
                GameObject prefabRoot = Instantiate(m_selectableAssetPrefab, transform);
                m_assetButtonObjects.Add(prefabRoot);
                GameObject sceneAssetButton = prefabRoot.GetComponentInChildren<RoundedBoxUIProperties>().gameObject;
                m_assetButtonRectTransforms.Add(sceneAssetButton.GetComponent<RectTransform>());
                Image image = sceneAssetButton.GetComponent<Image>();
                SetLoadingSprite(image);
                TMP_Text text = prefabRoot.GetComponentInChildren<TMP_Text>();
                text.enabled = false;
            }
        }

        private void RefreshAssetButtons(AssetInfo[] assets)
        {
            // Destroy all current items
            foreach (var buttonObj in m_assetButtonObjects)
            {
                Destroy(buttonObj);
            }
            m_assetButtonObjects.Clear();
            m_assetButtonRectTransforms.Clear();

            // Create new buttons again
            foreach (var info in assets)
            {
                GameObject prefabRoot = Instantiate(m_selectableAssetPrefab, transform);
                m_assetButtonObjects.Add(prefabRoot);

                SceneAssetItem asset = prefabRoot.GetComponentInChildren<SceneAssetItem>();
                GameObject sceneAssetButton = asset.gameObject;

                // Store the rect transform for use in visibility checks
                m_assetButtonRectTransforms.Add(sceneAssetButton.GetComponent<RectTransform>());

                // set asset attributes
                asset.SetAssetInfo(new SceneAssetInfo { m_assetName = info.m_name, m_assetId = info.m_uuid });

                // Initialize asset name text
                var text = prefabRoot.GetComponentInChildren<TMP_Text>();
                text.SetText(info.m_name);
                text.enabled = m_enableText;

                // Initialize thumbnail to a default image
                Image image = sceneAssetButton.GetComponentInChildren<Image>();
                SetLoadingSprite(image);

                // Asynchronously update to the actual thumbnail image
                _ = RevealButtonWhenReady(1000, prefabRoot, image, text, info.m_thumbnailUrl, m_thumbnailLoadedMaterial, m_defaultThumbnail);
            }
        }

        async Task ForceAssetRefresh()
        {
            if (m_assetsRefreshStart.HasValue)
            {
                uint currentAssetRequestId = ++m_assetRequestId;

                var assets = await m_assetIterator.LoadAssets(m_streamController.GetAssetManager());

                // Only apply changes from the LAST request made.
                if (m_assetRequestId == currentAssetRequestId)
                {
                    RefreshAssetButtons(assets);
                }

                m_assetsRefreshStart = null;
            }
        }

        public async Task AssetSourceChanged()
        {
            m_prefetchedAssetUrls.Clear();
            m_assetsRefreshStart = Time.time;
            await ForceAssetRefresh();
        }

        IEnumerator PeriodicallyPrefetchVisibleAssets(float seconds)
        {
            var wait = new WaitForSecondsRealtime(seconds);

            List<SceneAssetInfo> visibleAssets = new();
            while (true)
            {
                visibleAssets.Clear();

                // Determine all assets that are visible in the selector
                for (int i = 0; i < m_assetButtonRectTransforms.Count; i++)
                {
                    Vector3[] corners = new Vector3[4];
                    m_assetButtonRectTransforms[i].GetWorldCorners(corners);

                    foreach (Vector3 corner in corners)
                    {
                        if (RectTransformUtility.RectangleContainsScreenPoint(m_scrollRectViewport, new Vector2(corner.x, corner.y)))
                        {
                            var assetInfo = m_assetButtonObjects[i].gameObject.GetComponentInChildren<SceneAssetItem>().GetAssetInfo();
                            if (assetInfo != null && !m_prefetchedAssetUrls.Contains(assetInfo.m_assetId))
                            {
                                visibleAssets.Add(assetInfo);
                            }
                            break;
                        }
                    }
                }

                // Issue prefetch requests for all the presently visible assets
                foreach (var assetInfo in visibleAssets)
                {
                    // m_streamController.GetClient().PrefetchContent(m_streamController.GetFormattedUrl(assetInfo.m_assetId));
                    // Need a different prefetch for asset IDs
                }

                // We don't want to spam prefetch requests for resources that have already attempted to be prefetched
                m_prefetchedAssetUrls.UnionWith(visibleAssets.Select(x => x.m_assetId));

                yield return wait;
            }
        }

        void OnEnable()
        {
            if (m_streamController.isActiveAndEnabled)
            {
                Initialize();
            }
        }

        private void OnDisable()
        {
            Teardown();
        }

        public string PreviousAsset()
        {
            return m_assetIterator.PreviousAsset();
        }

        public string NextAsset()
        {
            return m_assetIterator.NextAsset();
        }

        public string GetAsset(int index = 0)
        {
            return m_assetIterator.GetAsset(index);
        }

        /// --------------------------------------------------------------------------
        /// Setup / teardown
        /// --------------------------------------------------------------------------

        private async void Initialize()
        {
            await ForceAssetRefresh();

            if (m_scrollRect != null)
            {
                // FIXME: Temporarily disabled until Prefetch works for asset IDs
                // m_periodicPrefetcher = StartCoroutine(PeriodicallyPrefetchVisibleAssets(0.25f));
            }
            else
            {
                Debug.LogWarning("Containing ScrollRect was not linked");
            }
        }

        private void Teardown()
        {
            if (m_periodicPrefetcher != null)
            {
                StopCoroutine(m_periodicPrefetcher);
            }
        }
    }
}
