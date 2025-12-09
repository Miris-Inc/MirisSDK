// Copyright © 2025 Miris, Inc. All rights reserved.

// C#
using System;
using System.IO;

// Unity Engine
using UnityEngine;

// Unity Editor
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.PackageManager;

using Miris.Runtime;

namespace Miris.Editor
{
    /// <summary>
    /// This script writes a VersionInfo.json file under Resources/Config
    /// which encodes the Android bundle version number so we can read it reliably at runtime to show in our dev menu.
    /// 
    /// See https://discussions.unity.com/t/how-can-i-get-bundle-version-and-bundle-version-code-through-script/429221/29
    /// </summary>
    public class VersionInfoWriter : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            WriteVersionInfo();
        }

        [DidReloadScripts]
        public static void WriteVersionInfo()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.Log("Android is not supported in this Editor instance, skipping VersionInfo write.");
                return;
            }

            VersionInfo vi = new VersionInfo
            {
                m_androidBundleVersion = PlayerSettings.Android.bundleVersionCode.ToString()
            };

            string versionInfoPath = Path.Combine(Application.dataPath, "Resources", "Config", "VersionInfo.json");
            string currentText = "";
            string newText = JsonUtility.ToJson(vi);
            try
            {
                currentText = File.ReadAllText(versionInfoPath);
            }
            catch (FileNotFoundException ex)
            {
                Debug.Log("VersionInfo.json not found, will create new one. " + ex.Message);
            }
            catch (DirectoryNotFoundException ex)
            {
                Debug.Log("Directories containing VersionInfo.json were not found, will create new one. " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to read VersionInfo.json: " + ex.Message);
            }

            if (currentText != newText)
            {
                Directory.CreateDirectory(Directory.GetParent(versionInfoPath).FullName);
                File.WriteAllText(versionInfoPath, newText);
                AssetDatabase.Refresh();
            }
        }

    }
}
