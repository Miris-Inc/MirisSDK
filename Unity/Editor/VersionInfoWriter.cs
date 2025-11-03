// C#
using System.IO;

// Unity Engine
using UnityEngine;

// Unity Editor
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.PackageManager;


namespace Aqua.Editor
{
    // This script writes a VersionInfo.cs file under $AQUA_ROOT/modules/unity_packages/miris_sdk_core/Runtime/Application/
    // which encodes the Android bundle version number so we can read it reliably at runtime to show in our dev menu.
    // 
    // See https://discussions.unity.com/t/how-can-i-get-bundle-version-and-bundle-version-code-through-script/429221/29
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
            string packagePath = PackageUtils.GetSdkPackagePath("core");
            Debug.Assert(packagePath != null, "Failed to find package path");
            string versionInfoPath = Path.Combine(packagePath, "Runtime", "Application", "VersionInfo.cs");
            string versionInfoCode = $"namespace Aqua.Runtime\r\n" +
                "{\r\n" +
                "public static class VersionInfo\r\n" +
                "{\r\n" +
                FormatVariable("m_androidBundleVersion", PlayerSettings.Android.bundleVersionCode.ToString()) +
                "}\r\n" +
                "}";

            string currentText = File.ReadAllText(versionInfoPath);
            if (currentText != versionInfoCode)
            {
                File.WriteAllText(versionInfoPath, versionInfoCode);
                AssetDatabase.Refresh();
            }
        }

        private static string FormatVariable(string varName, string varValue)
        {
            return $"    public const string {varName} = \"{varValue}\";\r\n";
        }
    }
}
