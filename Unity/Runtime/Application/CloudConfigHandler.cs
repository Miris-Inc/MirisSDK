// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Miris.Runtime
{
    public static class CloudConfigHandler
    {
        public static async Task<T> FetchJsonAsync<T>(string url) where T : class
        {
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Accept", "application/json");
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Request failed: {request.error}");
                throw new Exception($"Request failed: {request.error}");
            }

            string jsonResponse = request.downloadHandler.text;
            T response = JsonUtility.FromJson<T>(jsonResponse);

            if (response == null)
            {
                Debug.LogError("Failed to parse JSON response");
                throw new Exception("Failed to parse JSON response");
            }

            return response;
        }
        public static async Task<string> FetchRawJsonAsync(string url)
        {
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Accept", "application/json");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Request failed: {request.error}");
                throw new Exception($"Request failed: {request.error}");
            }

            return request.downloadHandler.text;
        }
    }
}