using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.Rendering;

// Aqua
using Aqua.Runtime;

namespace Aqua.Tests
{
    public class GpuReduceScanTests {
        
        static int[] s_keyCounts = { 10, 100, 1000, 10000 };
        
        private void InitializeData(int keyCount, ComputeBuffer gpuKeys, ComputeBuffer gpuPayload, ComputeBuffer cullMaskSplat, out ComputeBuffer indirectDrawBuffer, out ComputeBuffer indirectDispatchBuffer) {
            
            // initialize data and send to GPU
            float[] cpuKeys = new float[keyCount];
            for (int index = 0; index < keyCount; ++index)
            {
                cpuKeys[index] = UnityEngine.Random.Range(0.0f, 1.0f);
            }
            
            uint[] cpuPayload = new uint[keyCount];
            for (int index = 0; index < keyCount; ++index) {
                cpuPayload[index] = (uint)index;
            }
            
            uint[] culledData = new uint[keyCount];
            for (int index = 0; index < keyCount; ++index) {
                culledData[index] = (uint)UnityEngine.Random.Range(0,2); // Random values: 0 or 1. 0 means culled.
            }
            
            gpuKeys.SetData(cpuKeys);
            gpuPayload.SetData(cpuPayload);
            cullMaskSplat.SetData(culledData);
            
            // Allocate the indirect draw buffer.
            indirectDrawBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
            uint[] cpuIndirectDrawBuffer = new uint[] {
                0,
                (uint)keyCount, // This is the number of instances to draw, and also limits the number of elements to sort
                0,
                0,
                0
            };
            indirectDrawBuffer.SetData(cpuIndirectDrawBuffer);
            
            indirectDispatchBuffer = new ComputeBuffer(1, sizeof(uint)*4, ComputeBufferType.IndirectArguments);
        }
        [UnityTest, ConditionalIgnore("IgnoreInWindowsCI", "Ignored in window CI.")]
        public IEnumerator TestReduceScan([ValueSource(nameof(s_keyCounts))] int keyCount) {

            // // Initialize
            ComputeBuffer gpuKeysReduced = null;
            ComputeBuffer gpuPayloadReduced = null;
            ComputeBuffer cullMaskSplat = null;
            
            ComputeBuffer gpuKeys = new ComputeBuffer(keyCount, sizeof(float));
            ComputeBuffer gpuPayload = new ComputeBuffer(keyCount, sizeof(uint));
            
            GPUReduceAndScan gpuReduceAndScan = new GPUReduceAndScan
            (
                keyCount,
                ref cullMaskSplat,
                ref gpuKeysReduced, 
                ref gpuPayloadReduced
            );
            
            InitializeData(
                keyCount, 
                gpuKeys, 
                gpuPayload, 
                cullMaskSplat, 
                out ComputeBuffer indirectDrawBuffer, 
                out ComputeBuffer indirectDispatchBuffer
                );
            
            // // Execute
            CommandBuffer commandBuffer = new();
            gpuReduceAndScan.RunReduceScan
                (
                commandBuffer,
                cullMaskSplat,
                gpuKeys, 
                gpuKeysReduced, 
                gpuPayload, 
                gpuPayloadReduced, 
                indirectDrawBuffer,
                indirectDispatchBuffer
                );
            
            Graphics.ExecuteCommandBuffer(commandBuffer);
            
            // Compute expected output
            float[] cpuKeys = new float[keyCount];
            uint[] cpuPayload = new uint[keyCount];
            uint[] visibleSplatData = new uint[keyCount];
            
            gpuPayload.GetData(cpuPayload);
            gpuKeys.GetData(cpuKeys);
            cullMaskSplat.GetData(visibleSplatData);
            
            var (expectedCpuPayload, expectedCpuKeys)= ReduceScanCPUSim(visibleSplatData, cpuPayload, cpuKeys);
            
            // grab gpu data
            float[] computedGpuKeys = new float[keyCount];
            uint[] computedGpuPayload = new uint[keyCount];
            
            gpuPayloadReduced.GetData(computedGpuPayload);
            gpuKeysReduced.GetData(computedGpuKeys);
            
            uint[] reducedGpuCount =new uint[5];
            indirectDrawBuffer.GetData(reducedGpuCount);
            
            // validate results
            string failureMsg = "";
            int failureCount = 0;
            
            if (expectedCpuPayload.Count != reducedGpuCount[1]) {
                failureMsg += $"Expected a reduced count of {expectedCpuPayload.Count}, got {reducedGpuCount[1]}\n";
                failureCount++;
            }
            
            for (int i = 0; i < expectedCpuPayload.Count; ++i) {
                
                if (expectedCpuPayload[i] != computedGpuPayload[i]) {
                    failureMsg += $"Payload[{i}] expected {expectedCpuPayload[i]}, got {computedGpuPayload[i]}\n";
                    failureCount++;
                }
                
                if (expectedCpuKeys[i] != computedGpuKeys[i]) {
                    failureMsg += $"Keys[{i}] expected {expectedCpuKeys[i]}, got {computedGpuKeys[i]}\n";
                    failureCount++;
                }
                
            }
            
            
            if (failureCount > 0)
            {
                Assert.Fail($"{failureCount} failures!\n" + failureMsg);
            }
            
            //clean up
            gpuKeysReduced.Dispose();
            gpuPayloadReduced.Dispose();
            cullMaskSplat.Dispose();
            gpuKeys.Dispose();
            gpuPayload.Dispose();
            
            yield return null;
        }

        private (List<uint> payload, List<float> keys) ReduceScanCPUSim(uint[] visibleArray,
            uint[] payload, float[] keys) {
            
            List<uint> outPayload = new List<uint>();
            List<float> outKeys = new List<float>();

            for (int i = 0; i < visibleArray.Length; i++) {
                if (visibleArray[i] == 1) {
                    
                    outPayload.Add(payload[i]);
                    outKeys.Add(keys[i]);
                }
            }
            
            return (outPayload, outKeys);
            
        } 
    }
    
    
}
