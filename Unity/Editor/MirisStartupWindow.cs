using Aqua.Runtime;

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

using System;
using System.Linq;
using System.IO;

namespace Aqua.Editor
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
        private string m_assetViewerKey = "";

        [MenuItem("Tools/Aqua/Show Startup Window", false, -10)]
        static void Init()
        {
            EditorWindow.GetWindow<StartupWindow>("Miris SDK").Show();
        }

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

        void OnGUI()
        {
            // TODO: Show SDK Version
            GUILayout.Label("Welcome", EditorStyles.boldLabel);

            GUIStyle wordWrap = new GUIStyle(EditorStyles.label);
            wordWrap.wordWrap = true;
            wordWrap.richText = true;

            GUILayout.BeginHorizontal();
            GUILayout.Label(_welcome_message, wordWrap);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(_asset_key_message, wordWrap);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            m_assetViewerKey = EditorGUILayout.TextField("", m_assetViewerKey);
            if (GUILayout.Button("Apply"))
            {
                AquaClientConfig config = AquaClientConfig.Load();
                config.asset_viewer_key = m_assetViewerKey;
                AquaClientConfig.Write(config);
            }
            GUILayout.EndHorizontal();

            // TODO: Check for Updates button
            // TODO: Show changelog button

            if (GUILayout.Button("Visit Miris.com"))
            {
                Application.OpenURL("https://miris.com/");
            }

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
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Do not download plugin libraries automatically", wordWrap);
            m_doNotAutoDownloadBinaries = EditorGUILayout.Toggle(m_doNotAutoDownloadBinaries);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Do not show this popup again", wordWrap);
            m_doNotShowAgain = EditorGUILayout.Toggle(m_doNotShowAgain);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Dismiss"))
            {
                EditorPrefs.SetBool(DoNotShowAgainPrefsKey, m_doNotShowAgain);
                EditorPrefs.SetBool(DoNotAutoDownloadPrefsKey, m_doNotAutoDownloadBinaries);
                Close();
            }
        }
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
            if (EditorWindow.HasOpenInstances<StartupWindow>())
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
