using Aqua.Runtime;

using UnityEditor;
using UnityEngine;

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
        public static string PrefsKey = "MirisStartup_DoNotShowAgain";
        private static string _welcome_message =
            "The Miris Unity SDK is currently in a pre-alpha state, but feel free to look around.";
        private static string _asset_key_message =
            "If you have an Asset Viewer Key for use with this project (or need to replace your old one), please paste it below, and click \"Apply\"";

        private bool m_doNotShowAgain = false;
        private string m_assetViewerKey = "";

        [MenuItem("Tools/Aqua/Show Startup Window", false, -10)]
        static void Init()
        {
            EditorWindow.GetWindow<StartupWindow>("Miris SDK").Show();
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

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            m_doNotShowAgain = EditorGUILayout.Toggle("Do not show again", m_doNotShowAgain);

            if (GUILayout.Button("Dismiss"))
            {
                EditorPrefs.SetBool(PrefsKey, m_doNotShowAgain);
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

            // Do not show the window if the user has selected "Do Not Show Again"
            string windowKey = StartupWindow.PrefsKey;
            if (EditorPrefs.HasKey(windowKey) && EditorPrefs.GetBool(windowKey))
            {
                return;
            }

            EditorWindow.GetWindow<StartupWindow>("Miris SDK").Show();
        }
    }
}
