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

            WarmUpRenderer();
            CloudInit();
        }

        private void WarmUpRenderer()
        {
            // Warm up the shaders by instantiating a renderer and deleting it after a second.
            MirisStream stream = FindFirstObjectByType<MirisStream>();

            // Temporary work around for only allowing a single AquaScene root: Only warm up if scene path is not set to anything
            if (stream != null && stream.m_url == "")
            {
                // TODO: Need a more robust way to get access to this single splat data that doesn't involve downloading from the internet?
                stream.m_url = "https://devcontents3.miris.com/test/single-chunk/single/0_0_0-0.ply";
                StartCoroutine(ClearUrlLater(stream, 0.1f));
            }
        }

        private IEnumerator ClearUrlLater(MirisStream stream, float secondsLater)
        {
            yield return new WaitForSeconds(secondsLater);
            stream.m_url = "";
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
