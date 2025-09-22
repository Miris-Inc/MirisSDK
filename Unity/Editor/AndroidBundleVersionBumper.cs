// Copyright © 2024 Miris. All rights reserved.
using System;

using UnityEngine;
using UnityEditor;

namespace Aqua.Editor
{
    /// A utility Menu Item which can bump the Android bundle version code.
    /// Output format: YYYYMMDDN
    public class AndroidBundleVersionBumper
    {
        private const string s_toolTitle = "Bump Android Bundle Version";

        [MenuItem("Tools/Aqua/" + s_toolTitle)]
        public static void RunBumper()
        {
            // get current UTC time
            DateTime dt = DateTime.UtcNow;
            string version = dt.ToString("yyyyMMdd");

            // convert to int
            bool parsed = int.TryParse(version, out int result);
            if (parsed)
            {
                // hack: use Android bundle version as source of truth
                int currentVersionCode = PlayerSettings.Android.bundleVersionCode;
                result *= 10;
                int newVersionCode;
                
                if (result > currentVersionCode)
                {
                    // indicates first build of the day, should look like YYYYMMDD0
                    newVersionCode = result;
                }
                else
                {
                    // indicates additional build today, just increment build number
                    // TODO: catch overflow (unlikely)
                    currentVersionCode++;
                    newVersionCode = currentVersionCode;
                }
                PlayerSettings.Android.bundleVersionCode = newVersionCode;

                // Bump runtime-accessible VersionInfo.cs to match new bundle version.
                VersionInfoWriter.WriteVersionInfo();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Bumped version to: " + PlayerSettings.Android.bundleVersionCode);
            }
            else
            {
                Debug.LogError("Failed to parse version number: " + version);
            }
        }
    }
}