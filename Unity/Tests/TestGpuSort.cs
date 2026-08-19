// Copyright © 2026 Miris, Inc. All rights reserved.

// Standard lib
using System.Collections;
using System;
using System.Linq;

// NUnit
using NUnit.Framework;

// Unity engine
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;

using Miris.Runtime;

namespace Miris.Tests
{
    public class GpuSortTests
    {
        static GpuSortAlgorithm[] s_sortAlgorithms = Enum.GetValues(typeof(GpuSortAlgorithm)).Cast<GpuSortAlgorithm>().ToArray();
        static int[] s_keyCounts = {
            10,
            128,
            254, // Due to our sort culling customization, this is a important size to test.  See MirisCullSort in DeviceRadixSort.hlsl
            256,
            512,
            1000,
            10000
        };

        private void InitializeData(int keyCount, ComputeBuffer gpuKeys, ComputeBuffer gpuPayload, out ComputeBuffer indirectDrawBuffer)
        {
            // Create randomized keys & payload.  Keys and payload contain the exactly same data (just encoded as different types)
            float[] cpuKeys = new float[keyCount];
            for (int index = 0; index < keyCount; ++index)
            {
                cpuKeys[index] = index;
            }

            ShuffleArray(cpuKeys);

            uint[] cpuPayload = new uint[keyCount];
            for (int index = 0; index < keyCount; ++index)
            {
                cpuPayload[index] = (uint)cpuKeys[index];
            }

            // Upload to GPU
            gpuKeys.SetData(cpuKeys);
            gpuPayload.SetData(cpuPayload);

            // Allocate the indirect draw buffer.  Th
            indirectDrawBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
            uint[] cpuIndirectDrawBuffer = new uint[] {
                0,
                (uint)keyCount, // This is the number of instances to draw, and also limits the number of elements to sort
                0,
                0,
                0
            };
            indirectDrawBuffer.SetData(cpuIndirectDrawBuffer);
        }

        static void ShuffleArray<T>(T[] array)
        {
            System.Random random = new System.Random();
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "GPU tests require interactive window station on Windows CI")]
        public IEnumerator TestGpuSort([ValueSource(nameof(s_sortAlgorithms))] GpuSortAlgorithm sortAlgorithm, [ValueSource(nameof(s_keyCounts))] int keyCount)
        {
            // Initialize GPU data
            ComputeBuffer gpuKeys = null;
            ComputeBuffer gpuPayload = null;
            IGpuSort sort = GpuSortFactory.CreateGpuSort(
                sortAlgorithm,
                keyCount,
                ref gpuKeys,
                ref gpuPayload,
                keyType: typeof(float),
                payloadType: typeof(uint)
            );
            InitializeData(keyCount, gpuKeys, gpuPayload, out ComputeBuffer indirectDrawBuffer);


            // Execute sort
            CommandBuffer commandBuffer = new();
            sort.Sort(
                commandBuffer,
                keyCount,
                gpuKeys,
                gpuPayload,
                indirectDrawBuffer,
                keyType: typeof(float),
                payloadType: typeof(uint)
            );
            Graphics.ExecuteCommandBuffer(commandBuffer);


            // Download results
            float[] cpuKeys = new float[keyCount];
            gpuKeys.GetData(cpuKeys);
            uint[] cpuPayload = new uint[keyCount];
            gpuPayload.GetData(cpuPayload);

            string failureMsg = "";
            int failureCount = 0;
            for (int index = 0; index < keyCount; ++index)
            {
                int expectedValue = keyCount - index - 1;

                if ((int)cpuKeys[index] != expectedValue) {
                    failureMsg += $"Keys[{index}] expected {expectedValue}, got {cpuKeys[index]}\n";
                    failureCount++;
                }

                if ((int)cpuPayload[index] != expectedValue) {
                    failureMsg += $"Payload[{index}] expected {expectedValue}, got {cpuPayload[index]}\n";
                    failureCount++;
                }
            }

            if (failureCount > 0)
            {
                Assert.Fail($"{failureCount} failures!\n" + failureMsg);
            }

            gpuKeys.Dispose();
            gpuPayload.Dispose();

            yield return null;
        }
    }
}
