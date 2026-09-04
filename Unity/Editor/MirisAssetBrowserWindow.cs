// Copyright © 2026 Miris, Inc. All rights reserved.

// Standard library
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Unity engine
using UnityEditor;
using UnityEngine;

// Miris package
using Miris.Runtime;

namespace Miris.Editor
{
    /// <summary>
    /// Standalone Asset Browser, opened from MirisStreamControllerEditor's "Browse Assets…"
    /// button. Same Refresh/Prev/Next pagination as the inline foldout, just in its own window.
    ///
    /// A SearchProvider-based version was tried first and dropped: no per-instance state, and no
    /// way to hook "user scrolled near the bottom." A plain EditorWindow gives us both.
    /// </summary>
    public class MirisAssetBrowserWindow : EditorWindow
    {
        // Tile geometry for the icon grid; m_tileWidth is adjustable via the size slider below.
        private const float TileSpacing = 6f;
        private const float MinTileSize = 32f;
        private const float MaxTileSize = 128f;
        private float m_tileWidth = 72f;

        // Persisted across sessions. EditorPrefs is machine-global, same as the Project
        // window's own grid-size slider.
        private const string TileWidthPrefKey = "Miris.MirisAssetBrowserWindow.TileWidth";
        private const string PageSizePrefKey = "Miris.MirisAssetBrowserWindow.PageSize";

        // Tracked by UUID, not index -- m_assets gets replaced wholesale on every page change.
        private static readonly Color SelectionTint = new Color(0.24f, 0.48f, 0.90f, 0.35f);
        private string m_selectedAssetId;

        // 1-based, tracked locally since cursors are opaque (no real offset to report). Only
        // moves by one on a successful Next/Prev, so it can't drift from what's on screen.
        private int m_pageNumber = 1;

        // Unicode glyphs, not built-in icons -- those internal names shift between Unity versions.
        private static readonly GUIContent RefreshButtonContent = new GUIContent("↻", "Refresh");
        private static readonly GUIContent PrevButtonContent = new GUIContent("◀", "Previous page");
        private static readonly GUIContent NextButtonContent = new GUIContent("▶", "Next page");

        // Cached styles are rebuilt whenever the Editor's Light/Dark skin changes, not just once
        // ever -- otherwise toggling the theme without a domain reload leaves stale colors baked in.
        private static GUIStyle s_iconButtonStyle;
        private static bool s_iconButtonStyleIsProSkin;
        private static GUIStyle IconButtonStyle
        {
            get
            {
                if (s_iconButtonStyle == null || s_iconButtonStyleIsProSkin != EditorGUIUtility.isProSkin)
                {
                    s_iconButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                    s_iconButtonStyleIsProSkin = EditorGUIUtility.isProSkin;
                }
                return s_iconButtonStyle;
            }
        }

        // Icon-view look: centered thumbnail, wrapped caption below.
        private static GUIStyle s_tileThumbnailStyle;
        private static bool s_tileThumbnailStyleIsProSkin;
        private static GUIStyle TileThumbnailStyle
        {
            get
            {
                if (s_tileThumbnailStyle == null || s_tileThumbnailStyleIsProSkin != EditorGUIUtility.isProSkin)
                {
                    s_tileThumbnailStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, imagePosition = ImagePosition.ImageOnly };
                    s_tileThumbnailStyleIsProSkin = EditorGUIUtility.isProSkin;
                }
                return s_tileThumbnailStyle;
            }
        }

        private static GUIStyle s_tileLabelStyle;
        private static bool s_tileLabelStyleIsProSkin;
        private static GUIStyle TileLabelStyle
        {
            get
            {
                if (s_tileLabelStyle == null || s_tileLabelStyleIsProSkin != EditorGUIUtility.isProSkin)
                {
                    s_tileLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, wordWrap = true };
                    s_tileLabelStyleIsProSkin = EditorGUIUtility.isProSkin;
                }
                return s_tileLabelStyle;
            }
        }

        [SerializeField]
        private MirisStreamController m_controller;

        // The controller is only this window's source of truth for the viewer key -- page size
        // is a purely local browsing preference.
        private int m_pageSize = 20;

        // Debounces the Page Size slider so a drag fires one request, not one per tick.
        private EditorDebouncer m_pageSizeDebouncer;
        private EditorDebouncer PageSizeDebouncer =>
            m_pageSizeDebouncer ??= new EditorDebouncer(0.1, RefreshAssets);

        // Current page's assets and per-asset thumbnail cache, kept alive across redraws.
        private AssetInfo[] m_assets = new AssetInfo[0];
        private readonly Dictionary<string, Texture2D> m_thumbnailCache = new();
        private readonly HashSet<string> m_thumbnailsInFlight = new();
        private bool IsAnyThumbnailLoading => m_thumbnailsInFlight.Count > 0;

        // Pagination state from the last fetched page, used to drive the Next/Prev buttons.
        private bool m_isLoading;
        private string m_lastError;
        private string m_nextCursor = "";
        private string m_prevCursor = "";
        private bool HasNextPage => !string.IsNullOrEmpty(m_nextCursor);
        private bool HasPrevPage => !string.IsNullOrEmpty(m_prevCursor);
        private int m_requestId;

        private Vector2 m_scrollPosition;

        /// <summary>Opens the window, scoped to the given controller.</summary>
        // ProjectBrowser is internal to UnityEditor, so it's resolved by name instead of
        // typeof(). Falls back to null (no dock preference) if a future Unity version renames it.
        private static readonly Type s_projectBrowserType = Type.GetType("UnityEditor.ProjectBrowser,UnityEditor");

        public static void Open(MirisStreamController controller)
        {
            // Dock preference only applies the first time this window is created.
            MirisAssetBrowserWindow window = s_projectBrowserType != null
                ? GetWindow<MirisAssetBrowserWindow>("Miris Assets", s_projectBrowserType)
                : GetWindow<MirisAssetBrowserWindow>("Miris Assets");
            window.minSize = new Vector2(280, 360);
            window.SetController(controller);
            window.Show();
        }

        private void OnEnable()
        {
            m_tileWidth = EditorPrefs.GetFloat(TileWidthPrefKey, m_tileWidth);
            m_pageSize = EditorPrefs.GetInt(PageSizePrefKey, m_pageSize);

            // Also runs after a domain reload. m_controller (now [SerializeField]) survives, but
            // m_assets doesn't (AssetInfo isn't serializable), so ResetAndLoad() refetches -- and
            // falls back to a scene search if m_controller didn't survive either.
            ResetAndLoad();
        }

        private void SetController(MirisStreamController controller)
        {
            if (m_controller != null)
            {
                m_controller.ViewerKeyChanged -= OnViewerKeyChanged;
            }

            m_controller = controller;

            if (m_controller != null)
            {
                m_controller.ViewerKeyChanged += OnViewerKeyChanged;
            }

            ResetAndLoad();
        }

        // Without this, the event subscription outlives the window and points at a stale controller.
        private void OnDestroy()
        {
            if (m_controller != null)
            {
                m_controller.ViewerKeyChanged -= OnViewerKeyChanged;
            }
            m_pageSizeDebouncer?.Cancel();
            ClearThumbnailCache();
            DisposeAssets(m_assets);
        }

        /// <summary>
        /// Destroys each cached thumbnail before dropping it -- these are runtime-downloaded
        /// textures, so just clearing the dictionary would leak native memory until reload.
        /// </summary>
        private void ClearThumbnailCache()
        {
            foreach (Texture2D texture in m_thumbnailCache.Values)
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }
            m_thumbnailCache.Clear();
        }

        /// <summary>AssetInfo is a native SWIG wrapper too -- dispose before m_assets is replaced.</summary>
        private static void DisposeAssets(AssetInfo[] assets)
        {
            foreach (AssetInfo asset in assets)
            {
                asset?.Dispose();
            }
        }

        /// <summary>Viewer key changed on the controller -- refresh instead of waiting for Refresh.</summary>
        private void OnViewerKeyChanged(string newViewerKey)
        {
            if (this == null)
            {
                return;
            }
            // A different viewer key means a different catalog -- ResetAndLoad() clears the
            // thumbnail cache and selection too, not just the asset list RefreshAssets() alone
            // would refetch.
            ResetAndLoad();
            Repaint();
        }

        private void ResetAndLoad()
        {
            DisposeAssets(m_assets);
            m_assets = new AssetInfo[0];
            ClearThumbnailCache();
            m_thumbnailsInFlight.Clear();
            m_nextCursor = "";
            m_prevCursor = "";
            m_isLoading = false;
            m_lastError = null;
            m_scrollPosition = Vector2.zero;
            ++m_requestId; // drop any in-flight request from the previous controller
            m_pageSizeDebouncer?.Cancel();
            m_selectedAssetId = null;
            m_pageNumber = 1;

            titleContent = new GUIContent("Miris Assets", EditorGUIUtility.IconContent("Folder Icon").image);

            RefreshAssets();
        }

        /// <summary>
        /// Re-fetches the first page of assets.
        /// </summary>
        private void RefreshAssets()
        {
            _ = LoadPageAsync("", PageDirection.None, pendingPageNumber: 1);
        }

        /// <summary>
        /// Fetches the next page of assets, using the cursor from the last fetched page.
        /// </summary>
        private void LoadNextPage()
        {
            if (!HasNextPage)
            {
                return;
            }
            _ = LoadPageAsync(m_nextCursor, PageDirection.Next, m_pageNumber + 1);
        }

        /// <summary>
        /// Fetches the previous page of assets, using the cursor from the last fetched page.
        /// </summary>
        private void LoadPrevPage()
        {
            if (!HasPrevPage)
            {
                return;
            }
            _ = LoadPageAsync(m_prevCursor, PageDirection.Prev, m_pageNumber - 1);
        }

        private async Task LoadPageAsync(string cursor, PageDirection direction, int pendingPageNumber)
        {
            if (m_controller == null)
            {
                // Falls back to whatever's in the scene (e.g. after a domain reload wipes the
                // reference) instead of just failing. SetController() resets and refetches on
                // its own, so hand off to it and let this call be superseded rather than also
                // fetching here.
                MirisStreamController found = UnityEngine.Object.FindFirstObjectByType<MirisStreamController>();
                if (found != null)
                {
                    SetController(found);
                    return;
                }
            }

            if (m_controller == null)
            {
                m_lastError = "No MirisStreamController selected.";
                return;
            }

            // Fetched fresh every call rather than cached: the controller disposes and recreates
            // its AssetManager/Client on every Teardown/Initialize (e.g. a disable/enable cycle),
            // so a cached reference here would silently go stale and call into a disposed native
            // handle.
            AssetManager assetManager = m_controller.isActiveAndEnabled ? m_controller.GetAssetManager() : null;
            if (assetManager == null)
            {
                m_lastError = $"{nameof(MirisStreamController)} on the browsed GameObject must be enabled before assets can be listed.";
                return;
            }

            int requestId = ++m_requestId;
            m_isLoading = true;
            m_lastError = null;
            if (this != null)
            {
                Repaint();
            }

            try
            {
                assetManager.SetViewerKey(m_controller.ViewerKey);
                AssetsResultPage page = await assetManager.GetAssets(m_pageSize, cursor, direction);

                // Ignore this result if a newer request has since superseded it.
                if (requestId != m_requestId)
                {
                    return;
                }

                DisposeAssets(m_assets);
                m_assets = page.Assets;
                m_nextCursor = page.NextCursor ?? "";
                m_prevCursor = page.PrevCursor ?? "";
                m_pageNumber = pendingPageNumber;
                m_scrollPosition = Vector2.zero; // new page's content, don't leave scrolled mid-way
            }
            catch (Exception ex)
            {
                if (requestId == m_requestId)
                {
                    m_lastError = ex.Message;
                    Debug.LogException(ex);
                }
            }
            finally
            {
                if (requestId == m_requestId)
                {
                    m_isLoading = false;
                    if (this != null)
                    {
                        Repaint();
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (m_controller == null)
            {
                EditorGUILayout.HelpBox("No MirisStreamController selected. Reopen this window from its Inspector's \"Browse Assets…\" button, or retry to search the scene again.", MessageType.Warning);
                if (GUILayout.Button("Retry", GUILayout.Height(20)))
                {
                    ResetAndLoad();
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(RefreshButtonContent, IconButtonStyle, GUILayout.Width(28), GUILayout.Height(20)))
                {
                    RefreshAssets();
                }

                using (new EditorGUI.DisabledScope(!HasPrevPage))
                {
                    if (GUILayout.Button(PrevButtonContent, IconButtonStyle, GUILayout.Width(28), GUILayout.Height(20)))
                    {
                        LoadPrevPage();
                    }
                }

                using (new EditorGUI.DisabledScope(!HasNextPage))
                {
                    if (GUILayout.Button(NextButtonContent, IconButtonStyle, GUILayout.Width(28), GUILayout.Height(20)))
                    {
                        LoadNextPage();
                    }
                }

                // No total count available from opaque cursors, so just report what's loaded.
                EditorGUILayout.LabelField($"Page {m_pageNumber} · {m_assets.Length} shown", EditorStyles.miniLabel, GUILayout.Width(140));

                if (m_isLoading)
                {
                    EditorGUILayout.LabelField("Loading...");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                // MaxWidth alone starves the slider track first; shrink the label instead.
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 70f;

                EditorGUI.BeginChangeCheck();
                m_pageSize = EditorGUILayout.IntSlider("Page Size", m_pageSize, 1, 100, GUILayout.Width(260));
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetInt(PageSizePrefKey, m_pageSize);
                    PageSizeDebouncer.Ping();
                }

                EditorGUIUtility.labelWidth = previousLabelWidth;
                GUILayout.FlexibleSpace();
            }

            if (!string.IsNullOrEmpty(m_lastError))
            {
                EditorGUILayout.HelpBox(m_lastError, MessageType.Warning);
            }

            EditorGUILayout.Space();

            // ExpandHeight pins the size-slider strip to the bottom instead of trailing a short list.
            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition, GUILayout.ExpandHeight(true));

            if (!m_isLoading && m_assets.Length == 0 && string.IsNullOrEmpty(m_lastError))
            {
                EditorGUILayout.HelpBox("No assets found. Check the viewer key, or increase the max asset count.", MessageType.Info);
            }
            else if (m_assets.Length > 0)
            {
                EditorGUILayout.LabelField("Double-click, or drag an asset into the Hierarchy or Scene View, to spawn it.", EditorStyles.miniLabel);
                DrawAssetGrid();
            }

            EditorGUILayout.EndScrollView();

            DrawTileSizeSlider();

            // Keep redrawing while loading, so results appear as soon as they arrive.
            if (m_isLoading || IsAnyThumbnailLoading)
            {
                Repaint();
            }
        }

        /// <summary>Bottom-right tile-size slider, like the Project window's grid-size control.
        /// Purely visual -- no re-fetch, just reflows the grid.</summary>
        private void DrawTileSizeSlider()
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18)))
            {
                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                m_tileWidth = GUILayout.HorizontalSlider(m_tileWidth, MinTileSize, MaxTileSize, GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetFloat(TileWidthPrefKey, m_tileWidth);
                    Repaint();
                }
            }
        }

        /// <summary>Wrapping icon grid: as many tiles per row as fit the current width.</summary>
        private void DrawAssetGrid()
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - TileSpacing) / (m_tileWidth + TileSpacing)));

            for (int i = 0; i < m_assets.Length; i += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns; column++)
                    {
                        int index = i + column;
                        if (index >= m_assets.Length)
                        {
                            break;
                        }
                        DrawAssetTile(m_assets[index]);
                        GUILayout.Space(TileSpacing);
                    }
                    GUILayout.FlexibleSpace();
                }
                GUILayout.Space(TileSpacing);
            }
        }

        private void DrawAssetTile(AssetInfo info)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(m_tileWidth));

            Texture2D thumbnail = GetThumbnail(info);
            GUILayout.Label(new GUIContent(thumbnail != null ? thumbnail : Texture2D.grayTexture, GetTileTooltip(info)),
                TileThumbnailStyle, GUILayout.Width(m_tileWidth), GUILayout.Height(m_tileWidth));
            GUILayout.Label(info.m_name, TileLabelStyle, GUILayout.Width(m_tileWidth));

            EditorGUILayout.EndVertical();

            // BeginVertical's own Rect isn't reliable until Repaint; GetLastRect() after
            // EndVertical() is.
            Rect tileRect = GUILayoutUtility.GetLastRect();

            // Drawn after the content, not before -- BeginArea would let us tint underneath, but
            // it doesn't nest inside a HorizontalScope (that's what broke the grid earlier).
            // It's semi-transparent, so drawing it on top looks the same either way.
            if (info.m_uuid == m_selectedAssetId && Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(tileRect, SelectionTint);
            }

            HandleTileInteraction(tileRect, info);
        }

        // Includes name and uuid, not just tags -- identical tooltip text between adjacent tiles
        // stops Unity's tooltip from moving when you mouse between them. uuid guarantees uniqueness.
        private static string GetTileTooltip(AssetInfo info)
        {
            string tags = info.m_tags == null || info.m_tags.Count == 0
                ? "No tags"
                : "Tags: " + string.Join(", ", info.m_tags);
            return $"{info.m_uuid}\n{info.m_name}\n{tags}\n";
        }

        /// <summary>Click selects, double-click spawns, drag uses the same DragAndDrop path as
        /// the inline browser.</summary>
        private void HandleTileInteraction(Rect tileRect, AssetInfo info)
        {
            Event evt = Event.current;
            if (!tileRect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type == EventType.MouseDown)
            {
                if (evt.clickCount == 2)
                {
                    MirisStreamAssetBrowserDropHandler.SpawnMirisStreamInstance(MakePayload(info), parent: null, worldPosition: null);
                }
                else
                {
                    m_selectedAssetId = info.m_uuid;
                }
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.MouseDrag)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(MirisStreamDragPayload.GenericDataKey, MakePayload(info));
                DragAndDrop.StartDrag(string.IsNullOrEmpty(info.m_name) ? "Miris Asset" : info.m_name);
                evt.Use();
            }
        }

        private MirisStreamDragPayload MakePayload(AssetInfo info)
        {
            return new MirisStreamDragPayload
            {
                AssetId = info.m_uuid,
                AssetName = info.m_name,
                Controller = m_controller,
            };
        }

        /// <summary>Cached thumbnail for the asset, kicking off a fetch if needed. Null while pending.</summary>
        private Texture2D GetThumbnail(AssetInfo info)
        {
            if (string.IsNullOrEmpty(info.m_uuid))
            {
                return null;
            }

            if (m_thumbnailCache.TryGetValue(info.m_uuid, out Texture2D cached))
            {
                return cached;
            }

            if (!m_thumbnailsInFlight.Contains(info.m_uuid) && !string.IsNullOrEmpty(info.m_thumbnailUrl))
            {
                _ = FetchThumbnail(info.m_uuid, info.m_thumbnailUrl);
            }

            return null;
        }

        private async Task FetchThumbnail(string uuid, string url)
        {
            m_thumbnailsInFlight.Add(uuid);
            try
            {
                Texture2D texture = await UiUtils.FetchTextureAsync(url);
                if (this == null)
                {
                    // Window closed mid-fetch -- destroy it now, nothing else will.
                    if (texture != null)
                    {
                        DestroyImmediate(texture);
                    }
                    return;
                }
                m_thumbnailCache[uuid] = texture; // cache a miss too, so we don't retry every frame
                Repaint();
            }
            finally
            {
                m_thumbnailsInFlight.Remove(uuid);
            }
        }

        /// <summary>
        /// Debounces rapid calls (e.g. a slider drag) into one delayed call, `delaySeconds` after
        /// the last Ping() -- used for the Page Size slider, so a drag fires one request, not one
        /// per tick.
        ///
        /// Editor-only: uses EditorApplication.update to catch the timeout, since there's no
        /// per-frame Update() outside a MonoBehaviour to hook into here.
        /// </summary>
        private sealed class EditorDebouncer
        {
            private readonly double m_delaySeconds;
            private readonly Action m_action;
            private double m_dueTime;
            private bool m_pending;

            public EditorDebouncer(double delaySeconds, Action action)
            {
                m_delaySeconds = delaySeconds;
                m_action = action;
            }

            /// <summary>Restarts the delay window. Call on every change.</summary>
            public void Ping()
            {
                m_dueTime = EditorApplication.timeSinceStartup + m_delaySeconds;
                if (!m_pending)
                {
                    m_pending = true;
                    EditorApplication.update += OnUpdate;
                }
            }

            /// <summary>Cancels a pending call, if any. Call from OnDisable/OnDestroy.</summary>
            public void Cancel()
            {
                if (m_pending)
                {
                    EditorApplication.update -= OnUpdate;
                    m_pending = false;
                }
            }

            private void OnUpdate()
            {
                if (EditorApplication.timeSinceStartup < m_dueTime)
                {
                    return;
                }

                EditorApplication.update -= OnUpdate;
                m_pending = false;
                m_action();
            }
        }
    }
}
