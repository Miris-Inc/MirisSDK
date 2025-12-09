// Copyright © 2025 Miris, Inc. All rights reserved.

using System.IO;
using UnityEngine;

namespace Miris.Runtime
{
    public class VersionInfo
    {
        public static string BasePath = "Config/VersionInfo";

        public string m_androidBundleVersion = "";
    }

    public static class VersionInfoReader
    {
        public static VersionInfo Info => GetVersionInfo();

        private static VersionInfo _versionInfo;

        public static VersionInfo GetVersionInfo()
        {
            if (_versionInfo == null)
            {
                LoadVersionInfo();
            }
            return _versionInfo;
        }

        private static void LoadVersionInfo()
        {
            TextAsset jsonText = Resources.Load<TextAsset>(VersionInfo.BasePath);
            if (jsonText != null)
            {
                // Parse the JSON into a C# object
                _versionInfo = JsonUtility.FromJson<VersionInfo>(jsonText.text);
            }
            else
            {
                Debug.LogError($"Failed to load VersionInfo.json from {VersionInfo.BasePath}.");
                _versionInfo = new VersionInfo();
            }
        }
    }
}