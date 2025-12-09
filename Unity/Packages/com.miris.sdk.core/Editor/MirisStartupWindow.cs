// Copyright © 2025 Miris, Inc. All rights reserved.

using Miris.Runtime;

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

using System;
using System.Linq;
using System.IO;

namespace Miris.Editor
{
    /// <summary>
    /// The startup window for the Miris SDK. This window is opened by 
    /// <see cref="StartupWindowOpener"/>, or by <see cref="StartupWindow.Init"/>.
    /// 
    /// This window is meant to introduce new users to the Miris SDK.
    /// </summary>
    public class StartupWindow : EditorWindow
    {
        public static string DoNotShowAgainPrefsKey = "MirisStartup_DoNotShowAgain";
        public static string DoNotAutoDownloadPrefsKey = "MirisStartup_DoNotAutoDownloadBinaries";
        
        private static string _welcome_message =
            "The Miris Unity SDK is currently in a pre-alpha state, but feel free to look around.";
        private static string _asset_key_message =
            "If you have an Asset Viewer Key for use with this project (or need to replace your old one), please paste it below, and click \"Apply\"";

        private bool m_doNotShowAgain = false;
        private bool m_doNotAutoDownloadBinaries = false;

        private string m_packageVersion = "Unknown";
        private string m_assetViewerKey = "";

        [MenuItem("Tools/Miris/Show Startup Window", false, -20)]
        static void Init()
        {
            EditorWindow.GetWindow<StartupWindow>("Miris SDK").Show();
        }

        #region Graphics API Configuration
        private static GraphicsDeviceType[] GetIdealGraphicsAPIs()
        {
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
            {
                return new GraphicsDeviceType[] { GraphicsDeviceType.Metal, GraphicsDeviceType.Vulkan};
            }

            return new GraphicsDeviceType[] { GraphicsDeviceType.Vulkan};
        }

        private static GraphicsDeviceType[] GetCurrentGraphicsAPIs()
        {
            return PlayerSettings.GetGraphicsAPIs(EditorUserBuildSettings.activeBuildTarget);
        }

        public static bool ConfiguredWithIdealGraphicsAPI()
        {
            var playerGfxAPI = GetCurrentGraphicsAPIs()[0];
            var idealGfxAPIs = GetIdealGraphicsAPIs();

            // Checks whether the current build target's renderer is using an ideal/supported graphics API
            return idealGfxAPIs.Contains(playerGfxAPI);
        }

        private static bool SwitchToGraphicsAPI(GraphicsDeviceType api)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;

            try
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, false);
                PlayerSettings.SetGraphicsAPIs(buildTarget, new[] { api });
                Debug.Log($"[Miris] Graphics API set to {api} for {buildTarget}.");

                return EditorUtility.DisplayDialog(
                    "Graphics API Set",
                    $"Graphics API has been set to {api} for the current build target.\nPlease restart the Editor for full effect.",
                    "Restart Editor", "Continue");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Miris] Failed to set Graphics API to {api}: {ex}");
            }

            return false;
        }
        #endregion

        #region Unity Lifecycle
        void OnEnable()
        {
            m_doNotShowAgain = EditorPrefs.GetBool(DoNotShowAgainPrefsKey, false);
            m_doNotAutoDownloadBinaries = EditorPrefs.GetBool(DoNotAutoDownloadPrefsKey, false);
            m_packageVersion = PackageUtils.GetPackageVersion();
        }
        
        void OnGUI()
        {
            // TODO: Show SDK Version
            #region Welcome Section
            GUILayout.Label("Welcome", EditorStyles.boldLabel);

            GUIStyle wordWrap = new GUIStyle(EditorStyles.label);
            wordWrap.wordWrap = true;
            wordWrap.richText = true;
            wordWrap.alignment = TextAnchor.MiddleLeft;

            GUILayout.BeginHorizontal();
            GUILayout.Label(_welcome_message, wordWrap);
            GUILayout.EndHorizontal();
            
            #if !MIRIS_INTERNAL
            GUILayout.BeginHorizontal();
            GUILayout.Label(_asset_key_message, wordWrap);
            GUILayout.EndHorizontal();
            #endif

            #if !MIRIS_INTERNAL
            GUILayout.BeginHorizontal();
            m_assetViewerKey = EditorGUILayout.TextField("", m_assetViewerKey);
            if (GUILayout.Button("Apply"))
            {
                ClientConfig config = ClientConfig.Load();
                config.asset_viewer_key = m_assetViewerKey;
                ClientConfig.Write(config);
            }
            GUILayout.EndHorizontal();
            #endif

            #region Versioning
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            GUILayout.Label("Version", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Miris SDK Version: {m_packageVersion}", wordWrap);
            GUILayout.EndHorizontal();
            #endregion

            // TODO: Check for Updates button
            // TODO: Show changelog button

            if (GUILayout.Button("Visit Miris.com"))
            {
                Application.OpenURL("https://miris.com/");
            }
            #endregion
            
            #region Graphics Validation
            if (!ConfiguredWithIdealGraphicsAPI())
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                var playerGfxAPI = GetCurrentGraphicsAPIs()[0];
                var idealGfxApi = GetIdealGraphicsAPIs()[0];

                EditorGUILayout.HelpBox($"Graphics API is currently set to {playerGfxAPI}. Miris works best with {idealGfxApi}.", MessageType.Warning);

                if (GUILayout.Button($"Switch to {idealGfxApi}", GUILayout.Height(30)))
                {
                    if (SwitchToGraphicsAPI(idealGfxApi))
                    {
                        EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                    }
                }
            }
            #endregion

            #region Settings and Dismissal
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            GUILayout.Label("Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            m_doNotAutoDownloadBinaries = EditorGUILayout.Toggle(m_doNotAutoDownloadBinaries, GUILayout.Width(20));
            EditorGUILayout.LabelField("Do not download plugin libraries automatically", wordWrap);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            m_doNotShowAgain = EditorGUILayout.Toggle(m_doNotShowAgain, GUILayout.Width(20));
            EditorGUILayout.LabelField("Do not show this popup again", wordWrap);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (GUILayout.Button("Dismiss"))
            {
                EditorPrefs.SetBool(DoNotShowAgainPrefsKey, m_doNotShowAgain);
                EditorPrefs.SetBool(DoNotAutoDownloadPrefsKey, m_doNotAutoDownloadBinaries);
                Close();
            }
            #endregion
        }
        #endregion
    }

    /// <summary>
    /// Opens the <see cref="StartupWindow"/>
    /// </summary>
    [InitializeOnLoad]
    public class StartupWindowOpener
    {
        static StartupWindowOpener()
        {
            // Do not show the window if it's already rendering
            // Or if running in batch mode
            if (Application.isBatchMode || EditorWindow.HasOpenInstances<StartupWindow>())
            {
                return;
            }

            // Always show if the configured graphics API is not ideal
            if (StartupWindow.ConfiguredWithIdealGraphicsAPI())
            {
                // Do not show the window if the user has selected "Do Not Show Again"
                string windowKey = StartupWindow.DoNotShowAgainPrefsKey;
                if (EditorPrefs.HasKey(windowKey) && EditorPrefs.GetBool(windowKey))
                {
                    return;
                }
            }

            EditorWindow.GetWindow<StartupWindow>("Miris SDK").Show();
        }
    }
}
