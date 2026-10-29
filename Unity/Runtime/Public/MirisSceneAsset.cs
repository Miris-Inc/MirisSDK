// Copyright © 2026 Miris, Inc. All rights reserved.

// Standard library
using System;

// Unity engine
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Miris.Runtime has its own Scene.
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Miris.Runtime
{
    /// <summary>
    /// An asset that belongs to one scene, loaded from a Resources folder so builds can find it.
    /// Only the active scene's assets apply.
    /// </summary>
    public abstract class MirisSceneAsset : ScriptableObject
    {
        /// <summary>
        /// Raised after any scene asset is edited, so a running controller can pick the change up.
        /// </summary>
        public static event Action Changed;

        /// <summary>Raised when the active scene changes, and so which scene assets apply.</summary>
        public static event Action ActiveSceneChanged;

        // The active scene last announced, so each change is raised once.
        private static string s_activeScenePath;

        // Read only in the Editor, but serialized in builds too so the asset's layout matches.
#pragma warning disable CS0169, CS0649
        [SerializeField]
        private string m_sceneGuid;
#pragma warning restore CS0169, CS0649

        // A player can only name its scenes by path, so this follows m_sceneGuid: resolved live in
        // the Editor and refreshed before each build.
        [SerializeField]
        private string m_scenePath = "";

        /// <summary>The asset path of the scene this asset belongs to.</summary>
        public string ScenePath
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(m_sceneGuid))
                {
                    return AssetDatabase.GUIDToAssetPath(m_sceneGuid);
                }
#endif
                return m_scenePath;
            }
        }

        /// <summary>The asset path of the active scene, empty while it is unsaved.</summary>
        public static string ActiveScenePath => SceneManager.GetActiveScene().path;

        public static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        /// <summary>
        /// The <typeparamref name="T"/> in <paramref name="resourcesFolder"/> for the scene at
        /// <paramref name="scenePath"/>, or null when that scene has none.
        /// </summary>
        protected static T Find<T>(string resourcesFolder, string scenePath) where T : MirisSceneAsset
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                return null;
            }

            foreach (T asset in Resources.LoadAll<T>(resourcesFolder))
            {
                if (asset.ScenePath == scenePath)
                {
                    return asset;
                }
            }

            return null;
        }

        // --------------------------------------------------------------------
        // Active scene tracking
        // --------------------------------------------------------------------

        private static void OnActiveSceneChanged()
        {
            string scenePath = ActiveScenePath;
            if (scenePath == s_activeScenePath)
            {
                return;
            }

            s_activeScenePath = scenePath;
            ActiveSceneChanged?.Invoke();
        }

        private static void OnActiveSceneChanged(UnityScene previous, UnityScene active)
        {
            OnActiveSceneChanged();
        }

        // Fresh per play session, including with domain reload disabled, where statics carry over.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            s_activeScenePath = ActiveScenePath;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditorSceneTracking()
        {
            s_activeScenePath = ActiveScenePath;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;

            // Saving an untitled scene gives the active scene a path without changing which it is.
            EditorSceneManager.sceneSaved += scene => OnActiveSceneChanged();

            // Leaving play mode restores the edit-mode scenes without a change event.
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredEditMode)
                {
                    OnActiveSceneChanged();
                }
            };
        }
#endif
    }
}
