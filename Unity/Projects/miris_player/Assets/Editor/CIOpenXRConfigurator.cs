#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Configures OpenXR based on XR_INPUT_MODE env var.
// CI/test environments use hand tracking only (no controllers) to prevent runtime install blocking on Android.
[InitializeOnLoad]
public class CIOpenXRConfigurator
{
    private const string OPENXR_SETTINGS_PATH = "Assets/XR/Settings/OpenXRPackageSettings.asset";

    static CIOpenXRConfigurator() => EditorApplication.delayCall += ConfigureOpenXRForEnvironment;

    [MenuItem("Tools/Configure OpenXR for CI")]
    public static void ConfigureOpenXRForEnvironment()
    {
        string xrInputMode = Environment.GetEnvironmentVariable("XR_INPUT_MODE");

        if (string.IsNullOrEmpty(xrInputMode))
        {
            Debug.Log("[CIOpenXRConfigurator] XR_INPUT_MODE not set, skipping OpenXR configuration");
            return;
        }

        bool isHandTracking = xrInputMode.Equals("HAND_TRACKING", StringComparison.OrdinalIgnoreCase);
        Debug.Log($"[CIOpenXRConfigurator] Detected XR_INPUT_MODE={xrInputMode}");

        if (!File.Exists(OPENXR_SETTINGS_PATH))
        {
            Debug.LogWarning($"[CIOpenXRConfigurator] OpenXR settings file not found at: {OPENXR_SETTINGS_PATH}");
            return;
        }

        try
        {
            string content = File.ReadAllText(OPENXR_SETTINGS_PATH);
            bool modified = false;

            if (isHandTracking)
            {
                // Prevents runtime install/run blocking on Android in CI
                content = SetOpenXRFeatureEnabled(content, "OculusTouchControllerProfile Android", false, ref modified);
                content = SetOpenXRFeatureEnabled(content, "HandInteractionProfile Android", true, ref modified);

                Debug.Log("[CIOpenXRConfigurator] ✅ Configured for HAND_TRACKING mode:");
                Debug.Log("  - Disabled: Oculus Touch Controller Profile (prevents CI blocking)");
                Debug.Log("  - Enabled: Hand Interaction Profile");
            }
            else if (xrInputMode.Equals("CONTROLLERS", StringComparison.OrdinalIgnoreCase))
            {
                content = SetOpenXRFeatureEnabled(content, "OculusTouchControllerProfile Android", true, ref modified);
                // Keep hand profile enabled for seamless switching
                content = SetOpenXRFeatureEnabled(content, "HandInteractionProfile Android", true, ref modified);

                Debug.Log("[CIOpenXRConfigurator] ✅ Configured for CONTROLLERS mode:");
                Debug.Log("  - Enabled: Oculus Touch Controller Profile");
                Debug.Log("  - Enabled: Hand Interaction Profile (for seamless switching)");
            }

            if (modified)
            {
                File.WriteAllText(OPENXR_SETTINGS_PATH, content);
                AssetDatabase.Refresh();
                Debug.Log($"[CIOpenXRConfigurator] ✅ OpenXR configuration updated successfully");
            }
            else
                Debug.Log("[CIOpenXRConfigurator] No configuration changes needed");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CIOpenXRConfigurator] ❌ Failed to configure OpenXR: {e.Message}\n{e.StackTrace}");
        }
    }

    private static string SetOpenXRFeatureEnabled(string content, string featureName, bool enabled, ref bool modified)
    {
        string searchPattern = $"m_Name: {featureName}";
        int featureIndex = content.IndexOf(searchPattern);

        if (featureIndex == -1)
        {
            Debug.LogWarning($"[CIOpenXRConfigurator] Feature '{featureName}' not found in OpenXR settings");
            return content;
        }

        // Look within the same MonoBehaviour block (before the next --- delimiter)
        int nextSeparator = content.IndexOf("---", featureIndex + 1);
        int enabledIndex = content.IndexOf("m_enabled:", featureIndex);

        if (enabledIndex == -1 || (nextSeparator != -1 && enabledIndex > nextSeparator))
        {
            Debug.LogWarning($"[CIOpenXRConfigurator] Could not find m_enabled for feature '{featureName}'");
            return content;
        }

        int lineEnd = content.IndexOf('\n', enabledIndex);
        if (lineEnd == -1) lineEnd = content.Length;

        string enabledLine = content.Substring(enabledIndex, lineEnd - enabledIndex);
        string currentValue = enabledLine.Split(':')[1].Trim();

        string targetValue = enabled ? "1" : "0";
        if (currentValue == targetValue)
            return content;

        string newEnabledLine = $"  m_enabled: {targetValue}";
        content = content.Substring(0, enabledIndex) + newEnabledLine + content.Substring(lineEnd);
        modified = true;

        Debug.Log($"[CIOpenXRConfigurator]   {featureName}: {currentValue} → {targetValue}");
        return content;
    }
}
#endif
