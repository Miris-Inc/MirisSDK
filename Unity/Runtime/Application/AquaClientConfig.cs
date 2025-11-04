// Copyright © 2024 Miris. All rights reserved.
using System.IO;

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Aqua.Runtime
{
    /// <summary>
    /// Data class for the SDK client configuration. Note that this is a
    /// ScriptableObject in external deployments, and plain JSON internally.
    /// </summary>
    [System.Serializable]
    public class AquaClientConfig
#if !MIRIS_INTERNAL
    : ScriptableObject
#endif
    {
        #if !MIRIS_INTERNAL
        private static string m_basePath = "Config/MirisSDK";
        #else
        private static string m_basePath = "Config/aqua-config";

        public string version = "nover";
        public string devlocalhost = "localhost";
        public string devlocalhost_fqdn = "localhost";
        #endif
        public string asset_viewer_key = "";
        
        /// <summary>
        /// Loads the client configuration. Prefers the Application's config.
        /// If the Application config is not provided, Load will fallback to 
        /// a package config, if there is one.
        /// </summary>
        /// <returns>Loaded client config</returns>
        static public AquaClientConfig Load()
        {
            AquaClientConfig config = 
            #if !MIRIS_INTERNAL
                null;
            #else
                new();
            #endif

            if (!LoadFromResources(ref config))
            {
                Debug.Log("Existing config not found, creating a new config with default values.");

                #if !MIRIS_INTERNAL
                config = ScriptableObject.CreateInstance<AquaClientConfig>();
                #if UNITY_EDITOR
                // In editor, write the asset to the Resources folder
                string path = Path.Combine("Assets", "Resources", m_basePath + ".asset");

                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                #else
                Debug.LogError("AquaClientConfig not found in Resources, and cannot create one outside of the editor.");
                #endif
                #endif
            }

            #if MIRIS_INTERNAL
            Debug.Log($"Aqua Config devlocalhost: {config.devlocalhost} devlocalhost_fqdn: {config.devlocalhost_fqdn}");
            #endif
            return config;
        }

        /// <summary>
        /// Writes a new config to Application's Resources folder. Only 
        /// functions in the editor. Once this is done, future Load calls 
        /// will use the Application config, rather than any config provided 
        /// in a package.
        /// </summary>
        /// <param name="config">The config to write</param>
        static public void Write(AquaClientConfig config)
        {
#if UNITY_EDITOR
    #if !MIRIS_INTERNAL
            // ScriptableObject assets should be saved in place, so mark it dirty
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
    #else
            string path = Path.Combine(Application.dataPath, "Resources", m_basePath + ".json");
            string parentPath = Path.GetDirectoryName(path);
            Directory.CreateDirectory(parentPath);

            File.WriteAllText(path, JsonUtility.ToJson(config));
            AssetDatabase.Refresh();
    #endif
#else
            Debug.LogError($"Cannot update {nameof(AquaClientConfig)} outside of the editor.");
#endif
        }

        static private bool LoadFromResources(ref AquaClientConfig config)
        {
            #if !MIRIS_INTERNAL
            config = Resources.Load<AquaClientConfig>(m_basePath);
            return config != null;
            #else
            // Load JSON from the Resources folder
            TextAsset jsonText = Resources.Load<TextAsset>(m_basePath);
            if (jsonText != null)
            {
                Debug.Log(jsonText.text);
                // Parse the JSON into a C# object
                config = JsonUtility.FromJson<AquaClientConfig>(jsonText.text);

                return true;
            }
            else
            {
                return false;
            }
            #endif
        }
    }
}
