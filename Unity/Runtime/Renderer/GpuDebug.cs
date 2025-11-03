// Copyright © 2024 Miris. All rights reserved.

// Unity engine
using UnityEngine;
using UnityEngine.Rendering;

// Unity packages
using Unity.Collections;

namespace Aqua.Runtime
{
    // Helper class with methods for for debugging GPU data.
    public class GpuDebug
    {
        static public void DownloadAndPrintBuffer<T>(string bufferName, ComputeBuffer gpuBuffer)
            where T : struct
        {
            AsyncGPUReadback.Request(gpuBuffer, (AsyncGPUReadbackRequest request) =>
            {
                NativeArray<T> array = request.GetData<T>();
                string text = "[";
                for (int i = 0; i < array.Length; ++i)
                {
                    text += array[i] + ((i + 1) < array.Length ? ", " : "]");
                }
                Debug.Log($"{bufferName} (size={array.Length}): " + text);
            });
        }
    }
}
