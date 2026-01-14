// Copyright © 2025 Miris, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Miris.Runtime
{
    /// <summary>
    /// Data class for the SDK client configuration. Note that this is a
    /// ScriptableObject in external deployments, and plain JSON internally.
    /// </summary>
    [Serializable]
    public class ClientConfig
#if !MIRIS_INTERNAL
    : ScriptableObject
#endif
    {
        public string version = "nover";

        #if !MIRIS_INTERNAL
        private static string m_basePath = "Config/MirisSDK";

        public string asset_viewer_key = "";
        #else
        private static string m_basePath = "Config/aqua-config";

        public Dictionary<string, string> asset_viewer_keys = new();
        #endif

        public string GetAssetViewerKey()
        {
            #if !MIRIS_INTERNAL
            return asset_viewer_key;
            #else
            if (asset_viewer_keys.ContainsKey("Prod"))
            {
                return asset_viewer_keys["Prod"];
            }

            return asset_viewer_keys.Count > 0 ? asset_viewer_keys.First().Value : "";
            #endif
        }

        /// <summary>
        /// Loads the client configuration. Prefers the Application's config.
        /// If the Application config is not provided, Load will fallback to 
        /// a package config, if there is one.
        /// </summary>
        /// <returns>Loaded client config</returns>
        static public ClientConfig Load()
        {
            ClientConfig config = 
            #if !MIRIS_INTERNAL
                null;
            #else
                new();
            #endif

            if (!LoadFromResources(ref config))
            {
                MirisDebug.Log("Existing config not found, creating a new config with default values.");

                #if !MIRIS_INTERNAL
                config = ScriptableObject.CreateInstance<ClientConfig>();
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
                Debug.LogError("ClientConfig not found in Resources, and cannot create one outside of the editor.");
                #endif
                #endif
            }
            return config;
        }

        /// <summary>
        /// Writes a new config to Application's Resources folder. Only 
        /// functions in the editor. Once this is done, future Load calls 
        /// will use the Application config, rather than any config provided 
        /// in a package.
        /// </summary>
        /// <param name="config">The config to write</param>
        static public void Write(ClientConfig config)
        {
#if UNITY_EDITOR
    #if !MIRIS_INTERNAL
            // ScriptableObject assets should be saved in place, so mark it dirty
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
    #else
            // For simplicity, do not persist changes for miris internal since we have better tools for this
    #endif
#else
            Debug.LogError($"Cannot update {nameof(ClientConfig)} outside of the editor.");
#endif
        }

#if MIRIS_INTERNAL
        [Serializable]
        private class ClientConfigJson
        {
            public string version = "";
            public string[] asset_viewer_envs;
            public string[] asset_viewer_keys;
        }
#endif

        static private bool LoadFromResources(ref ClientConfig config)
        {
            #if !MIRIS_INTERNAL
            config = Resources.Load<ClientConfig>(m_basePath);
            return config != null;
            #else
            // Load JSON from the Resources folder
            TextAsset jsonText = Resources.Load<TextAsset>(m_basePath);
            if (jsonText != null)
            {
                MirisDebug.Log(jsonText.text);
                // Parse the JSON into a C# object
                var configJson = JsonUtility.FromJson<ClientConfigJson>(jsonText.text);

                config = new();
                config.version = configJson.version;

                // Unity's JSONUtility does not support C# dictionaries. Unity recommends using
                // Newtonsoft.Json for complex serialization, but I did not want to add that
                // as a dependency to our Core package, so instead we're serializing an array
                // of keys and its corresponding array of values
                if (configJson.asset_viewer_envs?.Length > 0
                    && configJson.asset_viewer_envs?.Length == configJson.asset_viewer_envs?.Length)
                {
                    config.asset_viewer_keys = new();
                    for (int i = 0; i < configJson.asset_viewer_envs.Length; i++)
                    {
                        config.asset_viewer_keys.Add(configJson.asset_viewer_envs[i], configJson.asset_viewer_keys[i]);
                    }
                }

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
