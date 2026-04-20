// Copyright © 2026 Miris, Inc. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using UnityEditor.PackageManager;
using UnityEngine;

using Miris.Runtime;

namespace Miris.Editor
{
    /// <summary>
    /// Data structure for deserializing package.json files.
    /// (Unity uses other fields, but we only care about these three.)
    /// </summary>
    [System.Serializable]
    public class PackageJson
    {
        public string version;
        public string unity;
        public string unityRelease;
    }

    public class PackageUtils
    {
        private const string c_packageNameFormat = "com.miris.sdk.{sdkName}";

        public static PackageInfo GetPackageInfo(string name = "core")
        {
            string packageName = GetSdkPackageName(name);

            // Find the package info for the given package name
            PackageInfo packageInfo = PackageInfo.GetAllRegisteredPackages()
                .FirstOrDefault(x => x.name == packageName);

            return packageInfo;
        }

        /// <summary>
        /// Get the version of the Miris SDK package.
        /// </summary>
        /// <param name="name">The name of the SDK package (default is "core").</param>
        /// <returns>The version string of the package, or "Unknown" if not found.</returns>
        public static string GetPackageVersion(string name = "core")
        {
            // Read the package.json file to get the version
            // since PackageInfo.version may not always be reliable
            try
            {
                string packagePath = GetSdkPackagePath(name);
                string packageJsonPath = System.IO.Path.Combine(packagePath, "package.json");
                
                if (System.IO.File.Exists(packageJsonPath))
                {
                    string jsonText = System.IO.File.ReadAllText(packageJsonPath);
                    PackageJson packageJson = JsonUtility.FromJson<PackageJson>(jsonText);
                    return packageJson.version ?? "Unknown";
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to read package.json for {name}: {ex.Message}");
            }
            
            return "Unknown";
        }

        /// <summary>
        /// Gets the URL of the Miris SDK package if it is installed from a Git source.
        /// </summary>
        /// <param name="name">The name of the SDK package (default is "core").</param>
        /// <returns>
        /// The URL string of the package if it is a Git package;
        /// <c>null</c> if the package is not installed from a Git source;
        /// or "Unknown" if the package information could not be determined.
        /// </returns>
        public static string GetPackageURL(string name = "core")
        {
            PackageInfo packageInfo = GetPackageInfo(name);

            if (packageInfo?.source != PackageSource.Git)
            {
                return null;
            }

            return packageInfo?.packageId.Split('@').Last() ?? "Unknown";
        }

        // Get the resolved file path of the Miris SDK package on disk.
        public static string GetSdkPackagePath(string sdkName)
        {
            string sdkPackageName = GetSdkPackageName(sdkName);
            PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + sdkPackageName);
            return package.resolvedPath;
        }

        private static string GetSdkPackageName(string sdkName)
        {
            var replacements = new Dictionary<string, string>
            {
                {"sdkName", sdkName},
            };
            return StringUtils.ExpandVars(c_packageNameFormat, replacements);
        }
    }
}
