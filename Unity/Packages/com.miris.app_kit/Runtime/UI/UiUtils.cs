// Copyright © 2025 Miris, Inc. All rights reserved.

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

namespace Miris.Runtime
{
    public class UiUtils : MonoBehaviour
    {
        [SerializeField]
        MirisStreamController m_streamController;

        static public void InitializeEnumDropdown(TMP_Dropdown dropdown, Type enumType, UnityEngine.Events.UnityAction<int> listenerFunc)
        {
            foreach (string value in Enum.GetNames(enumType))
            {
                dropdown.options.Add(new TMP_Dropdown.OptionData(value));
            }

            dropdown.onValueChanged.AddListener(listenerFunc);
            dropdown.RefreshShownValue();
        }

        public async Task<Texture2D> FetchTextureAsync(string url)
        {
            try
            {
                using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url))
                {
                    var operation = webRequest.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (webRequest.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"Error fetching texture from {url}: {webRequest.error}");
                        return null;
                    }

                    return DownloadHandlerTexture.GetContent(webRequest);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception while fetching texture: {ex.Message}");
                return null;
            }
        }
    }
}
