// Copyright © 2025 Miris, Inc. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using UnityEditor.PackageManager;
using UnityEngine;

using Miris.Runtime;

namespace Miris.Editor
{
    public class PackageUtils
    {
        private const string c_packageNameFormat = "com.miris.sdk.{sdkName}";

        public static string GetPackageVersion(string name = "core")
        {
            string packageName = GetSdkPackageName(name);

            // Find the package info for the given package name
            PackageInfo packageInfo = PackageInfo.GetAllRegisteredPackages()
                .FirstOrDefault(x => x.name == packageName);
            
            return packageInfo?.version ?? "Unknown";
        }

        private static string GetSdkPackageName(string sdkName)
        {
            var replacements = new Dictionary<string, string>
            {
                {"sdkName", sdkName},
            };
            return StringUtils.ExpandVars(c_packageNameFormat, replacements);
        }

        // Get the resolved file path of the Miris SDK package on disk.
        public static string GetSdkPackagePath(string sdkName)
        {
            string sdkPackageName = GetSdkPackageName(sdkName);
            PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + sdkPackageName);
            return package.resolvedPath;
        }
    }
}
