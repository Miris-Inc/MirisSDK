// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Reflection;

#if UNITY_XR_SIMULATION
using UnityEditor.XR.Simulation;
#endif

namespace Miris.Editor.Tools
{
    /// <summary>
    /// Miris Unity Editor menu items for developer tools and utilities
    /// </summary>
    public static class MenuItems
    {
        private const string XR_SIM_MENU_PATH = "Tools/Miris/Toggle XR Simulation";
        
        /// <summary>
        /// Auto-configure XR Simulation based on CI/batch mode detection
        /// Called when Unity Editor loads - ensures CI tests have XR simulation enabled
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AutoConfigureXRSimulation()
        {
            if (IsCIMode())
            {
                // CI tests need XR simulation for proper VR/mobile testing
                SetXRSimulation(true);
                Debug.Log("[Miris] Auto-enabled XR Simulation for CI/batch mode testing");
            }
            // Interactive development - leave current state, developers can toggle manually
        }
        
        /// <summary>
        /// Toggle XR Simulation on/off for desktop vs VR development
        /// Useful for switching between desktop dev mode and VR testing mode
        /// </summary>
        [MenuItem(XR_SIM_MENU_PATH)]
        public static void ToggleXRSimulation()
        {
            bool currentState = IsXRSimulationEnabled();
            SetXRSimulation(!currentState);
            
            string status = !currentState ? "ENABLED" : "DISABLED";
            string context = !currentState ? "VR testing mode" : "Desktop development mode";
            
            Debug.Log($"[Miris] XR Simulation {status} - Now in {context}");
            
            // Force repaint of Scene view to update simulation visuals
            SceneView.RepaintAll();
        }
        
        /// <summary>
        /// Validation function for XR Simulation toggle menu item
        /// Shows checkmark when XR simulation is currently enabled
        /// </summary>
        [MenuItem(XR_SIM_MENU_PATH, true)]
        public static bool ValidateToggleXRSimulation()
        {
            // Show checkmark when XR simulation is enabled
            Menu.SetChecked(XR_SIM_MENU_PATH, IsXRSimulationEnabled());
            return true;
        }
        
        /// <summary>
        /// Check if XR Simulation is currently enabled
        /// </summary>
        private static bool IsXRSimulationEnabled()
        {
            try
            {
                // First, try to check actual XR settings
                var xrGeneralSettingsType = System.Type.GetType("UnityEngine.XR.Management.XRGeneralSettings, Unity.XR.Management");
                if (xrGeneralSettingsType != null)
                {
                    var instanceProperty = xrGeneralSettingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    var generalSettingsInstance = instanceProperty?.GetValue(null);
                    
                    if (generalSettingsInstance != null)
                    {
                        var managerProperty = xrGeneralSettingsType.GetProperty("Manager", BindingFlags.Public | BindingFlags.Instance);
                        var managerInstance = managerProperty?.GetValue(generalSettingsInstance);
                        
                        if (managerInstance != null)
                        {
                            // Check if XR Simulation loader is active
                            var activeLoadersProperty = managerInstance.GetType().GetProperty("activeLoaders", BindingFlags.Public | BindingFlags.Instance);
                            if (activeLoadersProperty != null)
                            {
                                var activeLoaders = activeLoadersProperty.GetValue(managerInstance) as System.Collections.IList;
                                if (activeLoaders != null)
                                {
                                    bool foundSimulation = false;
                                    foreach (var loader in activeLoaders)
                                    {
                                        if (loader.GetType().Name.Contains("Simulation"))
                                        {
                                            foundSimulation = true;
                                            break;
                                        }
                                    }
                                    
                                    Debug.Log($"[Miris] IsXRSimulationEnabled() - Found simulation loader: {foundSimulation}");
                                    
                                    // Sync EditorPrefs with actual state
                                    EditorPrefs.SetBool("Miris.XRSimulation.Enabled", foundSimulation);
                                    return foundSimulation;
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Miris] Error checking XR Simulation state: {e.Message}");
            }
            
            // Fallback to EditorPrefs for XR Simulation state
            bool editorPrefsValue = EditorPrefs.GetBool("Miris.XRSimulation.Enabled", false);
            Debug.Log($"[Miris] IsXRSimulationEnabled() - Fallback to EditorPrefs: {editorPrefsValue}");
            return editorPrefsValue;
        }
        
        /// <summary>
        /// Check if running in CI/automated testing mode
        /// </summary>
        private static bool IsCIMode()
        {
            // Check if Unity is running in batch mode (CI tests use -batchmode)
            if (Application.isBatchMode)
            {
                Debug.Log("[Miris] Detected batch mode - CI environment");
                return true;
            }
                
            // Check GitHub Actions environment variable
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
            {
                Debug.Log("[Miris] Detected GITHUB_ACTIONS environment variable - CI environment");
                return true;
            }
                
            // Check generic CI environment variable
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
            {
                Debug.Log("[Miris] Detected CI environment variable - CI environment");
                return true;
            }
            
            // Check for aqua_cmd.py --ci flag (sets this env var)
            string aquaCIFlag = Environment.GetEnvironmentVariable("AQUA_CI_MODE");
            if (!string.IsNullOrEmpty(aquaCIFlag) && aquaCIFlag.ToLower() == "true")
            {
                Debug.Log("[Miris] Detected AQUA_CI_MODE environment variable - CI environment");
                return true;
            }
                
            return false;
        }
        
        /// <summary>
        /// Enable or disable XR Simulation by directly modifying XR settings
        /// </summary>
        private static void SetXRSimulation(bool enabled)
        {
            try
            {
                // Use reflection to access Unity's internal XR settings
                var xrGeneralSettingsType = System.Type.GetType("UnityEngine.XR.Management.XRGeneralSettings, Unity.XR.Management");
                var xrManagerDataType = System.Type.GetType("UnityEngine.XR.Management.XRManagerSettings, Unity.XR.Management");
                
                if (xrGeneralSettingsType != null && xrManagerDataType != null)
                {
                    // Get the XRGeneralSettings instance
                    var instanceProperty = xrGeneralSettingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    var generalSettingsInstance = instanceProperty?.GetValue(null);
                    
                    if (generalSettingsInstance != null)
                    {
                        // Get the Manager property
                        var managerProperty = xrGeneralSettingsType.GetProperty("Manager", BindingFlags.Public | BindingFlags.Instance);
                        var managerInstance = managerProperty?.GetValue(generalSettingsInstance);
                        
                        if (managerInstance != null)
                        {
                            // Try to find XR Simulation loader with different possible type names
                            var xrSimulationLoaderType = System.Type.GetType("UnityEngine.XR.Simulation.XRSimulationLoader, Unity.XR.Simulation") ??
                                                        System.Type.GetType("Unity.XR.Simulation.XRSimulationLoader, Unity.XR.Simulation") ??
                                                        System.Type.GetType("XRSimulationLoader") ??
                                                        GetXRSimulationLoaderType();
                            
                            if (xrSimulationLoaderType != null)
                            {
                                // Check if XR Simulation is already active
                                var activeLoadersProperty = xrManagerDataType.GetProperty("activeLoaders", BindingFlags.Public | BindingFlags.Instance);
                                var activeLoaders = activeLoadersProperty?.GetValue(managerInstance) as System.Collections.IList;
                                bool isCurrentlyActive = false;
                                
                                if (activeLoaders != null)
                                {
                                    foreach (var loader in activeLoaders)
                                    {
                                        if (loader.GetType() == xrSimulationLoaderType)
                                        {
                                            isCurrentlyActive = true;
                                            break;
                                        }
                                    }
                                }
                                
                                Debug.Log($"[Miris] XR Simulation currently active: {isCurrentlyActive}, trying to set to: {enabled}");
                                
                                if (enabled && !isCurrentlyActive)
                                {
                                    // Try to add XR Simulation loader with multiple method signatures
                                    var allMethods = xrManagerDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                        .Where(m => m.Name == "TryAddLoader" || m.Name == "AddLoader").ToArray();
                                    
                                    bool success = false;
                                    var simulationLoader = ScriptableObject.CreateInstance(xrSimulationLoaderType);
                                    
                                    foreach (var method in allMethods)
                                    {
                                        var parameters = method.GetParameters();
                                        try
                                        {
                                            if (parameters.Length == 1)
                                            {
                                                if (parameters[0].ParameterType.IsAssignableFrom(simulationLoader.GetType()))
                                                {
                                                    Debug.Log($"[Miris] Trying method {method.Name} with loader instance parameter");
                                                    var result = method.Invoke(managerInstance, new object[] { simulationLoader });
                                                    success = result is bool ? (bool)result : true;
                                                    break;
                                                }
                                                else if (parameters[0].ParameterType == typeof(System.Type))
                                                {
                                                    Debug.Log($"[Miris] Trying method {method.Name} with Type parameter");
                                                    var result = method.Invoke(managerInstance, new object[] { xrSimulationLoaderType });
                                                    success = result is bool ? (bool)result : true;
                                                    break;
                                                }
                                            }
                                        }
                                        catch (System.Exception ex)
                                        {
                                            Debug.LogWarning($"[Miris] Method {method.Name} failed: {ex.Message}");
                                        }
                                    }
                                    
                                    if (success)
                                    {
                                        Debug.Log("[Miris] XR Simulation enabled in XR Plug-in Management");
                                        EditorPrefs.SetBool("Miris.XRSimulation.Enabled", true);
                                    }
                                    else
                                    {
                                        Debug.LogWarning("[Miris] Failed to add XR Simulation loader with all available methods");
                                        // Manual addition as fallback
                                        activeLoaders.Add(simulationLoader);
                                        Debug.Log("[Miris] XR Simulation manually added to active loaders");
                                        EditorPrefs.SetBool("Miris.XRSimulation.Enabled", true);
                                    }
                                }
                                else if (enabled && isCurrentlyActive)
                                {
                                    Debug.Log("[Miris] XR Simulation already enabled");
                                    EditorPrefs.SetBool("Miris.XRSimulation.Enabled", true);
                                }
                                else
                                {
                                    // Try to remove XR Simulation loader - find the correct method signature
                                    var tryRemoveLoaderMethod = xrManagerDataType.GetMethod("TryRemoveLoader", BindingFlags.Public | BindingFlags.Instance);
                                    var removeLoaderMethod = xrManagerDataType.GetMethod("RemoveLoader", BindingFlags.Public | BindingFlags.Instance);
                                    
                                    bool success = false;
                                    
                                    if (tryRemoveLoaderMethod != null)
                                    {
                                        try
                                        {
                                            // Try with Type parameter first
                                            success = (bool)tryRemoveLoaderMethod.Invoke(managerInstance, new object[] { xrSimulationLoaderType });
                                        }
                                        catch
                                        {
                                            try
                                            {
                                                // Try with no parameters (remove by type)
                                                var methods = xrManagerDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                    .Where(m => m.Name == "TryRemoveLoader" || m.Name == "RemoveLoader").ToArray();
                                                
                                                foreach (var method in methods)
                                                {
                                                    var parameters = method.GetParameters();
                                                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(System.Type))
                                                    {
                                                        success = (bool)method.Invoke(managerInstance, new object[] { xrSimulationLoaderType });
                                                        break;
                                                    }
                                                }
                                            }
                                            catch (System.Exception ex)
                                            {
                                                Debug.LogWarning($"[Miris] Remove loader failed: {ex.Message}");
                                            }
                                        }
                                    }
                                    
                                    if (success)
                                    {
                                        Debug.Log("[Miris] XR Simulation disabled in XR Plug-in Management");
                                        EditorPrefs.SetBool("Miris.XRSimulation.Enabled", false);
                                    }
                                    else
                                    {
                                        Debug.LogWarning("[Miris] Failed to remove XR Simulation loader - trying manual removal");
                                        
                                        // Manual removal: get active loaders and remove XR Simulation
                                        if (activeLoaders != null)
                                        {
                                            int removedCount = 0;
                                            for (int i = activeLoaders.Count - 1; i >= 0; i--)
                                            {
                                                if (activeLoaders[i].GetType() == xrSimulationLoaderType)
                                                {
                                                    Debug.Log($"[Miris] Manually removing XR Simulation loader at index {i}");
                                                    activeLoaders.RemoveAt(i);
                                                    removedCount++;
                                                }
                                            }
                                            
                                            if (removedCount > 0)
                                            {
                                                Debug.Log($"[Miris] XR Simulation manually removed from active loaders ({removedCount} instances)");
                                                EditorPrefs.SetBool("Miris.XRSimulation.Enabled", false);
                                                success = true;
                                            }
                                            else
                                            {
                                                Debug.LogWarning("[Miris] No XR Simulation loaders found in active loaders for manual removal");
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogWarning("[Miris] XR Simulation loader not found - make sure XR Interaction Toolkit is installed");
                                // Fallback to EditorPrefs tracking
                                EditorPrefs.SetBool("Miris.XRSimulation.Enabled", enabled);
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[Miris] XR Management types not found - using preference tracking only");
                    EditorPrefs.SetBool("Miris.XRSimulation.Enabled", enabled);
                }
                
                // Save project to persist XR settings changes
                AssetDatabase.SaveAssets();
                
                // Refresh to update UI
                EditorApplication.delayCall += () =>
                {
                    EditorApplication.RepaintProjectWindow();
                    SceneView.RepaintAll();
                };
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Miris] Failed to toggle XR Simulation: {e.Message}");
                // Fallback to EditorPrefs tracking
                EditorPrefs.SetBool("Miris.XRSimulation.Enabled", enabled);
            }
        }
        
        /// <summary>
        /// Helper method to find XR Simulation loader type by searching all loaded assemblies
        /// </summary>
        private static System.Type GetXRSimulationLoaderType()
        {
            try
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    if (assembly.FullName.Contains("XR.Simulation") || assembly.FullName.Contains("Simulation"))
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name.Contains("Simulation") && type.Name.Contains("Loader"))
                            {
                                Debug.Log($"[Miris] Found potential XR Simulation loader: {type.FullName}");
                                return type;
                            }
                        }
                    }
                }
                
                // Also search all assemblies for any type containing "Simulation" and "Loader"
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name.ToLower().Contains("simulation") && 
                                (type.Name.ToLower().Contains("loader") || type.BaseType?.Name.ToLower().Contains("loader") == true))
                            {
                                Debug.Log($"[Miris] Found potential XR Simulation loader: {type.FullName}");
                                return type;
                            }
                        }
                    }
                    catch
                    {
                        // Skip assemblies that can't be reflected over
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Miris] Error searching for XR Simulation loader: {e.Message}");
            }
            
            return null;
        }
    }
}
