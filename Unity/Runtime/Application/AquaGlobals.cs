using System;
using System.Collections;
using UnityEngine;

namespace Aqua.Runtime
{
    [Serializable]
    public class CloudConfig
    {
        public bool @override;
        public EnvironmentInfo overrideEnv;
        public bool performServerTests;
    }

    [Serializable]
    public class EnvironmentInfo
    {
        public string name;
        public string displayName;
        public string baseUrl;
    }

    public class AquaGlobals : MonoBehaviour
    {
        private void Awake()
        {
            // TODO: Decide which specific log level we want to use here.
            int aquaIsDebug = AquaUnityApi.LibAquaIsDebug();
            bool isDebugBuild = aquaIsDebug != 0 && Debug.isDebugBuild;
            AquaUnityApi.SetLogLevel(isDebugBuild ? LogLevel.Info : LogLevel.Error);
            Debug.unityLogger.logEnabled = Debug.isDebugBuild;
        }

        private void Start()
        {
            // We could do other stuff here like present a custom loading / splash screen, preloading of resources, etc. 

            CloudInit();
        }

        private async void CloudInit()
        {
            string response = "";
            try
            {
                response = await CloudConfigHandler.FetchRawJsonAsync("https://devcontents3.miris.com/engtesting/hpa_override");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to fetch cloud config: {ex.Message}");
            }


            if (!string.IsNullOrEmpty(response))
            {
                CloudConfig config = JsonUtility.FromJson<CloudConfig>(response);
                if (config.performServerTests)
                {
                    string deviceId = SystemInfo.deviceUniqueIdentifier;
                    AquaClient.performThroughputTest(response, deviceId);
                }
            }

        }
    }
}
