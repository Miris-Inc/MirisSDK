// Copyright © 2024 Miris. All rights reserved.

// Standard library
using System;
using System.Runtime.InteropServices;

// Unity engine
using UnityEngine;
using UnityEngine.Rendering;

// Unity packages
using Unity.Profiling;
using Unity.Profiling.LowLevel;

// GPUSorting package
using GaussianSplatting.Runtime;

namespace Aqua.Runtime
{
    // The available GPU sorting algorithms available.
    public enum GpuSortAlgorithm : int
    {
        DeviceRadixSort,
    };

    // Interface for all GPU Sorting implementations.
    public interface IGpuSort : IDisposable
    {
        public abstract void CreateResources(
            int keyCount,
            ref ComputeBuffer keysToSort,
            ref ComputeBuffer payloadToSort,
            System.Type keyType,
            System.Type payloadType
        );

        // Subclass should implement this to dispatch the sort compute kernels.
        public abstract void Sort(
            CommandBuffer commandBuffer,
            int keyCount,
            ComputeBuffer keysToSort,
            ComputeBuffer payloadToSort,
            ComputeBuffer indirectDrawBuffer,
            System.Type keyType,
            System.Type payloadType
        );
    }

    // Make it more convenient to use b0nes164's GPUSorting.Runtime.DeviceRadixSort.
    public class DeviceRadixGpuSort : IGpuSort
    {
        private ComputeShader m_sortShader;
        private GaussianSplatting.Runtime.DeviceRadixSort m_deviceRadixSort;

        private ComputeBuffer m_tempKeyBuffer;
        private ComputeBuffer m_tempPayloadBuffer;

        private ComputeBuffer m_globalHistBuffer;
        private ComputeBuffer m_passHistBuffer;
        private ComputeBuffer m_indexBuffer;

        private DeviceRadixSort.SupportResources m_supportResources = DeviceRadixSort.SupportResources.Load(1024);

        private ComputeBuffer m_keysToSort;
        private ComputeBuffer m_payloadToSort;

        static readonly ProfilerMarker s_gpuMarker = new ProfilerMarker(
            ProfilerCategory.Render, "DeviceRadix Sort (GPU)", MarkerFlags.SampleGPU
        );

        // Update CreateResources to cache and reuse objects
        public void CreateResources(
            int keyCount,
            ref ComputeBuffer keysToSort,
            ref ComputeBuffer payloadToSort,
            System.Type keyType,
            System.Type payloadType
        )
        {
            // calculate higher key count to avoid relallocating buffers
            int keyCountExtended = keyCount + keyCount / 2;
            // Only load and create once
            if (m_sortShader == null)
            {
                m_sortShader = (ComputeShader)Resources.Load("Shaders/DeviceRadixSort/SplatUtilities");
            }
            if (m_deviceRadixSort == null)
            {
                m_deviceRadixSort = new GaussianSplatting.Runtime.DeviceRadixSort(m_sortShader);
            }

            if (m_supportResources.countLimit < keyCount)
            {
                // If the count limit is exceeded, dispose and recreate the support resources.
                m_supportResources.Dispose();
                m_supportResources = DeviceRadixSort.SupportResources.Load((uint)keyCountExtended);
            }

            // Reallocate key and payload buffers if needed.
            if (m_keysToSort == null || m_keysToSort.count < keyCount)
            {
                m_keysToSort?.Dispose();
                m_keysToSort = new ComputeBuffer(
                    keyCountExtended, Marshal.SizeOf(keyType));
            }

            if (m_payloadToSort == null || m_payloadToSort.count < keyCount)
            {
                m_payloadToSort?.Dispose();
                m_payloadToSort = new ComputeBuffer(
                    keyCountExtended, Marshal.SizeOf(payloadType));
            }

            keysToSort = m_keysToSort;
            payloadToSort = m_payloadToSort;
        }

        public void Sort(
            CommandBuffer commandBuffer,
            int keyCount,
            ComputeBuffer keysToSort,
            ComputeBuffer payloadToSort,
            ComputeBuffer indirectDrawBuffer,
            System.Type keyType,
            System.Type payloadType
        )
        {
            Debug.Assert(keyType == typeof(float));
            Debug.Assert(payloadType == typeof(uint));
            
            DeviceRadixSort.Args args;
            args.inputKeys = keysToSort;
            args.inputValues = payloadToSort;
            args.resources = m_supportResources;
            args.count = (uint) keyCount;
            args.workGroupCount = 0;
            
            commandBuffer.BeginSample(s_gpuMarker);
            
            m_deviceRadixSort.Dispatch(commandBuffer, args, indirectDrawBuffer);
            
            commandBuffer.EndSample(s_gpuMarker);
        }
        public void Dispose()
        {
            m_keysToSort?.Dispose();
            m_payloadToSort?.Dispose();
            m_tempKeyBuffer?.Dispose();
            m_tempPayloadBuffer?.Dispose();
            m_globalHistBuffer?.Dispose();
            m_passHistBuffer?.Dispose();
            m_indexBuffer?.Dispose();
            m_supportResources.Dispose();
            m_sortShader = null;
            m_deviceRadixSort = null;
        }
    }

    // Helper class to create IGpuSort instance(s) based on selected algorithm.
    public class GpuSortFactory
    {
        static public IGpuSort CreateGpuSort(
            GpuSortAlgorithm sortAlgorithm,
            int keyCount,
            ref ComputeBuffer keysToSort,
            ref ComputeBuffer payloadToSort,
            System.Type keyType,
            System.Type payloadType
        )
        {
            IGpuSort sort = null;
            switch (sortAlgorithm)
            {
                case GpuSortAlgorithm.DeviceRadixSort:
                    {
                        sort = new DeviceRadixGpuSort();
                        break;
                    }

                default:
                    {
                        throw new ArgumentOutOfRangeException(
                            $"Un-supported {nameof(GpuSortAlgorithm)}.{sortAlgorithm.ToString()}"
                        );
                    }
            }

            sort.CreateResources(keyCount, ref keysToSort, ref payloadToSort, keyType, payloadType);
            return sort;
        }
    }
}
