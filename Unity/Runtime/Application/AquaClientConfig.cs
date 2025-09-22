// Copyright © 2024 Miris. All rights reserved.
using System.IO;

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Aqua.Runtime
{
    /// <summary>
    /// Data class for the SDK client configuration
    /// </summary>
    [System.Serializable]
    public class AquaClientConfig
    {
        private static string m_basePath = "Config/aqua-config";

        public string version = "nover";
        public string devlocalhost = "localhost";
        public string devlocalhost_fqdn = "localhost";
        public string asset_viewer_key = "";

        /// <summary>
        /// Loads the client configuration. Prefers the Application's config.
        /// If the Application config is not provided, Load will fallback to 
        /// a package config, if there is one.
        /// </summary>
        /// <returns>Loaded client config</returns>
        static public AquaClientConfig Load()
        {
            AquaClientConfig config = new();
            if (!LoadFromResources(ref config))
            {
                Debug.Log("Failed to load client config from resources, reverting to default");
            }

            Debug.Log($"Aqua Config devlocalhost: {config.devlocalhost} devlocalhost_fqdn: {config.devlocalhost_fqdn}");
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
            string path = Path.Combine(Application.dataPath, "Resources", m_basePath + ".json");
            string parentPath = Path.GetDirectoryName(path);
            Directory.CreateDirectory(parentPath);

            File.WriteAllText(path, JsonUtility.ToJson(config));
            AssetDatabase.Refresh();
#else
            Debug.LogError($"Cannot update {nameof(AquaClientConfig)} outside of the editor.");
#endif
        }

        static private bool LoadFromResources(ref AquaClientConfig config)
        {
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
        }
    }
}
