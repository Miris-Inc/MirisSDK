using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using UnityEngine;

using Newtonsoft.Json;
using System.Linq;

namespace Miris.Runtime
{
    public class Preferences
    {
        #region Data model

        public class StreamingPreferences
        {
            public string environment = "Prod";
            public string viewerKey = "";
            public string[] tags = new string[] { };
        }
        
        public StreamingPreferences streaming = new();
        
        // Fallback values when the input fields are empty
        private StreamingPreferences streamingFallback = new StreamingPreferences {
            environment = "Prod",
            // 'Miris Demo' account
            viewerKey = "l2LWVHS39ZhmXDOtefPoOmvPdAp55OxTPJfwVmNo7rY",
            tags = new string[]{ "miris_player", "approved" }
        };

        public class PlayerControllerPreferences
        {
            public bool enableTeleporter = true;
        }

        public PlayerControllerPreferences playerController = new();

        public class ScenePreferences
        {
            public LodRefinementParameters m_lodRefinementParameters;
            public bool m_fadeLargeSplats;
        }

        // Root level dictionary of scene path -> per scene preferences
        public Dictionary<string, ScenePreferences> scenes = new();

        #endregion

        #region Static accessors

        public static Preferences instance = new();

        public static Task completedLoading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            Application.quitting += OnQuitting;

            completedLoading = Load();
        }

        private static async void OnQuitting()
        {
            await completedLoading;
            await Save();
        }

        private static string persistedFilePath => Path.Combine(Application.persistentDataPath, "miris", "preferences.json");

        private static async Task Load()
        {
            string filePath = Preferences.persistedFilePath;
            try
            {
                string jsonString = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
                instance = JsonConvert.DeserializeObject<Preferences>(jsonString);

            } catch (Exception e)
            {
                Debug.LogError($"Failed to read miris player preferences from {filePath}: {e}");
                instance = new();
            }
        }

        public static async Task<bool> Save()
        {
            string filePath = Preferences.persistedFilePath;
            try
            {
                string jsonString = JsonConvert.SerializeObject(instance, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, jsonString);
                return true;

            }
            catch (Exception e)
            {
                Debug.LogError($"Encountered exception when saving miris player preferences to {filePath}: {e}");
            }

            return false;
        }

        public string ResolveViewerKey(string inputViewerKey)
        {
            if (inputViewerKey.Length == 0)
            {
                return streamingFallback.viewerKey;
            }
            else
            {
                return inputViewerKey;
            }
        }

        public string[] ResolveTags(string[] inputTags)
        {
            return inputTags.Concat(streamingFallback.tags).ToArray();
        }

        #endregion
    }
}
