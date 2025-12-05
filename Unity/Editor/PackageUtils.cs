using System.Collections.Generic;
using Miris.Runtime;
using UnityEngine;

using UnityEditor.PackageManager;


namespace Miris.Editor
{
    public class PackageUtils
    {
        private const string c_packageNameFormat = "com.miris.sdk.{sdkName}";

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
