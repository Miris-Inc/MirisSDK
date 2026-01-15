// Copyright © 2025 Miris, Inc. All rights reserved.

using System;
using System.IO;
using System.Linq;

using Miris.Runtime;

using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

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

        private static readonly string WelcomeMessage =
            "The Miris Unity SDK is currently in an alpha state, but feel free to look around.";
        private static readonly string AssetKeyMessage =
            "If you have an Asset Viewer Key for use with this project (or need to replace your old one), please paste it below, and click \"Apply\"";

        private bool m_doNotShowAgain;
        private bool m_doNotAutoDownloadBinaries;

        private enum UpdateCheckState
        {
            NotStarted,
            Checking,
            UpToDate,
            UpdateAvailable,
            NotGitPackage,
            Error
        }

        private UpdateCheckState m_updateCheckState = UpdateCheckState.NotStarted;
        private string m_currentVersion;
        private string m_newVersion;
        private AddRequest m_addRequest;

        private ClientConfig m_clientConfig;
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
                return new GraphicsDeviceType[] { GraphicsDeviceType.Metal, GraphicsDeviceType.Vulkan };
            }

            return new GraphicsDeviceType[] { GraphicsDeviceType.Vulkan };
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

        #region Update Checking
        private void CheckForUpdatesRun()
        {
            m_updateCheckState = UpdateCheckState.Checking;

            if (PackageUtils.GetPackageURL() is string packageURL)
            {
                string gitURL = packageURL;
                string rawUrl = "";
                try
                {
                    // Parse the URL manually
                    string url = gitURL;

                    // Extract branch/tag (after #)
                    // Preserving the branch is necessary because different branches may have different latest versions
                    // (e.g., "pre-release" vs "latest")
                    string branch = "main";
                    int hashIndex = url.IndexOf('#');
                    if (hashIndex >= 0)
                    {
                        branch = url.Substring(hashIndex + 1);
                        url = url.Substring(0, hashIndex);
                    }

                    // Extract path parameter (after ?path=)
                    string packagePath = "";
                    int queryIndex = url.IndexOf("?path=");
                    if (queryIndex >= 0)
                    {
                        packagePath = url.Substring(queryIndex + 6);
                        url = url.Substring(0, queryIndex);
                    }

                    // Extract repository path (remove .git)
                    if (url.EndsWith(".git"))
                    {
                        url = url.Substring(0, url.Length - 4);
                    }

                    // Replace github.com with raw.githubusercontent.com and construct path
                    string repoPath = url.Replace("https://github.com/", "");
                    rawUrl = $"https://raw.githubusercontent.com/{repoPath}/refs/heads/{branch}/{packagePath}/package.json";
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Miris] Failed to convert Git URL to raw package.json URL: {ex}");
                    m_updateCheckState = UpdateCheckState.Error;
                    return;
                }

                UnityWebRequest www = UnityWebRequest.Get(rawUrl);
                var operation = www.SendWebRequest();
                operation.completed += (asyncOp) =>
                {
                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[Miris] Failed to check for updates: {www.error}");
                        m_updateCheckState = UpdateCheckState.Error;
                    }
                    else
                    {
                        try
                        {
                            string jsonText = www.downloadHandler.text;
                            var packageJson = JsonUtility.FromJson<PackageJson>(jsonText);
                            string latestVersion = packageJson.version;

                            try
                            {
                                Version latest = new Version(latestVersion);
                                Version current = new Version(m_currentVersion);
                                if (latest.CompareTo(current) > 0)
                                {
                                    m_updateCheckState = UpdateCheckState.UpdateAvailable;
                                    m_newVersion = latestVersion;
                                }
                                else
                                {
                                    m_updateCheckState = UpdateCheckState.UpToDate;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"[Miris] Failed to parse version strings for update check: {ex}");
                                m_updateCheckState = UpdateCheckState.Error;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[Miris] Failed to parse package.json for update check: {ex}");
                            m_updateCheckState = UpdateCheckState.Error;
                        }
                    }

                    Repaint();
                    www.Dispose();
                };
            }
            else
            {
                m_updateCheckState = UpdateCheckState.NotGitPackage;
            }
        }

        private void CheckForUpdatesUIBlock()
        {
            switch (m_updateCheckState)
            {
                case UpdateCheckState.Checking:
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Checking for updates...", EditorStyles.label);
                    GUILayout.EndHorizontal();
                    break;

                case UpdateCheckState.NotGitPackage:
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Miris SDK is not installed via Git. Update checks are unavailable.", EditorStyles.label);
                    GUILayout.EndHorizontal();
                    break;

                case UpdateCheckState.UpToDate:
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Your Miris SDK is up to date.", EditorStyles.label);
                    GUILayout.EndHorizontal();
                    break;

                case UpdateCheckState.UpdateAvailable:
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"A new version of the Miris SDK is available: {m_newVersion}", EditorStyles.label);
                    if (GUILayout.Button("Update"))
                    {
                        m_addRequest = UnityEditor.PackageManager.Client.Add(PackageUtils.GetPackageURL());
                    }
                    GUILayout.EndHorizontal();
                    break;

                case UpdateCheckState.Error:
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Error checking for updates.", EditorStyles.label);
                    GUILayout.EndHorizontal();
                    break;

                case UpdateCheckState.NotStarted:
                default:
                    // No UI to display
                    break;
            }
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            CheckForUpdatesRun();
        }

        private void Update()
        {
            if (m_addRequest != null)
            {
                switch (m_addRequest.Status)
                {
                    case StatusCode.InProgress:
                        // TODO: Query Package Manager for progress?
                        EditorUtility.DisplayProgressBar("Miris SDK Update", "Updating package...", 0.1f);
                        break;

                    case StatusCode.Failure:
                        EditorUtility.ClearProgressBar();
                        Debug.LogError($"[Miris] Failed to update Miris SDK: {m_addRequest.Error?.message}");
                        m_addRequest = null;
                        break;

                    case StatusCode.Success:
                        EditorUtility.ClearProgressBar();
                        Debug.Log($"[Miris] Miris SDK updated successfully to version {m_addRequest.Result.version}. Please restart the Editor.");
                        m_addRequest = null;
                        break;
                }
            }
        }

        private void OnEnable()
        {
            m_doNotShowAgain = EditorPrefs.GetBool(DoNotShowAgainPrefsKey, false);
            m_doNotAutoDownloadBinaries = EditorPrefs.GetBool(DoNotAutoDownloadPrefsKey, false);
            m_clientConfig = ClientConfig.Load();
            m_currentVersion = PackageUtils.GetPackageVersion();
        }

        private void OnGUI()
        {
            #region Welcome Section
            GUILayout.Label("Welcome", EditorStyles.boldLabel);

            GUIStyle wordWrap = new GUIStyle(EditorStyles.label);
            wordWrap.wordWrap = true;
            wordWrap.richText = true;
            wordWrap.alignment = TextAnchor.MiddleLeft;

            GUILayout.BeginHorizontal();
            GUILayout.Label(WelcomeMessage, wordWrap);
            GUILayout.EndHorizontal();

            #if !MIRIS_INTERNAL
            GUILayout.BeginHorizontal();
            GUILayout.Label(AssetKeyMessage, wordWrap);
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
            #else
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            ClientConfig config = m_clientConfig;
            if (config.asset_viewer_keys != null && config.asset_viewer_keys.Count > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Configured Asset Viewer Environments:", wordWrap);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                foreach (var kvp in config.asset_viewer_keys)
                {
                    GUILayout.Label($" - {kvp.Key}", wordWrap);
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"No Asset Viewer Environments/Keys configured.", EditorStyles.boldLabel);
                GUILayout.EndHorizontal();
                EditorGUILayout.HelpBox($"Define `MIRIS_VIEWER_KEY` as a variable in your `.aquaenv` file.\nThen run the following.\nWhen it finishes, restart Unity.", MessageType.Warning);
                EditorGUILayout.TextField("python aqua_cmd.py config-client");
            }
            #endif

            #region Versioning
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            GUILayout.Label("Version", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Miris SDK Version: {m_currentVersion ?? "Unknown"}", wordWrap);
            GUILayout.EndHorizontal();
            CheckForUpdatesUIBlock();
            #endregion

            if (m_updateCheckState != UpdateCheckState.Checking && m_updateCheckState != UpdateCheckState.NotGitPackage)
            {
                if (GUILayout.Button("Check for Updates"))
                {
                    CheckForUpdatesRun();
                }
            }

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
            // Do not show the window if running in CI environment
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CI")))
            {
                Debug.Log("[Miris] CI environment detected; skipping Startup Window.");
                return;
            }

            // Do not show the window if the editor is
            // - running in batch mode
            // - already showing the startup window
            if (Application.isBatchMode || EditorWindow.HasOpenInstances<StartupWindow>())
            {
                return;
            }

            // Otherwise, show if the configured graphics API is not ideal
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
