// Copyright © 2024 Miris. All rights reserved.

using System.Collections;
using System.Collections.Generic;

// Unity engine
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

// Unity packages
using Unity.Collections;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Collections.LowLevel.Unsafe;
using System;
using JetBrains.Annotations;

namespace Aqua.Runtime
{
    // Provides abstraction around an underlying GPU resource (e.g. GraphicsBuffer or texture)
    public interface IGpuBuffer : IDisposable
    {
        // Subclass should implement this to upload data to the GPU buffer.
        abstract void SetData(NativeArray<byte> bytesArray);

        // Subclass should implement this to upload data to the GPU buffer with an offset & size.
        abstract void SetData(NativeArray<byte> bytesArray, int bytesOffset, int bytesToUpload);
        
        // Subclass should implement this to bind the gpu buffer to a material.
        abstract void SetBufferOnMaterial(Material material);

        // Subclass should implement this to bind the gpu buffer to a compute shader.
        abstract void SetBufferOnComputeShader(CommandBuffer commandBuffer, ComputeShader computeShader, int kernelIndex);
        
        abstract void SetBufferOnComputeShader(CommandBuffer commandBuffer, ComputeShader computeShader, int kernelIndex, int paramID);

        abstract int GetBlockDim();

        abstract int GetTotalBytes();
    }

    // Wraps a GraphicsBuffer to adhere to IGpuBuffer interface
    public class GpuArray : IGpuBuffer
    {
        private GraphicsBuffer m_graphicsBuffer;

        private Int32 m_blockDim = 8;

        // Unique identifer that mapping to the variable definitions in the shader.
        private int m_bufferShaderId;

        private int m_totalBytes = 0;

        private string m_name = "unknown";
        
        public GpuArray(int totalBytes, int shaderId, int blockDim, string name )
        {
            m_blockDim = blockDim;
            // GraphicsBuffer demands that the stride must be at least 4
            // So we cast our buffer to a uint array before uploading (see SetData)
            m_graphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.None,
                count: totalBytes / sizeof(uint),
                stride: sizeof(uint)
            );
            
            m_name = name;
            m_graphicsBuffer.name = name;    
        
            m_bufferShaderId = shaderId;
            m_totalBytes = totalBytes;
        }

        public void SetData(NativeArray<byte> bytesArray)
        {
            m_graphicsBuffer.SetData(bytesArray.Reinterpret<uint>(1));
        }

        public void SetData(NativeArray<byte> bytesArray, int bytesOffset, int bytesToUpload)
        {
            m_graphicsBuffer.SetData(bytesArray.Reinterpret<uint>(1), 0, bytesOffset / sizeof(uint), bytesToUpload / sizeof(uint));
        }

        public void SetBufferOnMaterial(Material material)
        {
            material.SetBuffer(m_bufferShaderId, m_graphicsBuffer);
        }

        public void SetBufferOnComputeShader(CommandBuffer commandBuffer, ComputeShader computeShader, int kernelIndex)
        {
            commandBuffer.SetComputeBufferParam(computeShader, kernelIndex, m_bufferShaderId, m_graphicsBuffer);
        }
        
        public void SetBufferOnComputeShader(CommandBuffer commandBuffer, ComputeShader computeShader, int kernelIndex, int paramId)
        {
            commandBuffer.SetComputeBufferParam(computeShader, kernelIndex, paramId, m_graphicsBuffer);
        }

        public void Dispose()
        {
            m_graphicsBuffer?.Dispose();
            m_graphicsBuffer = null;
        }

        public int GetBlockDim()
        {
            return m_blockDim;
        }

        public int GetTotalBytes()
        {
            return m_totalBytes;
        }
    }

    // Wraps a Texture to adhere to IGpuBuffer interface
    public class GpuTexture : IGpuBuffer
    {
        private Texture2D m_texture;

        private Int32 m_blockDim = 8;

        // Unique identifer(s) that mapping to the variable definitions in the shader.
        private int m_textureShaderId;
        private int m_textureWidthShaderId;

        public GpuTexture(
            string textureName,
            int textureWidth,
            int textureHeight,
            GraphicsFormat graphicsFormat,
            int textureShaderId,
            int textureWidthShaderId,
            int blockDim
        )
        {
            m_blockDim = blockDim;
            m_texture = new Texture2D(
                textureWidth,
                textureHeight,
                graphicsFormat,
                TextureCreationFlags.DontInitializePixels | TextureCreationFlags.DontUploadUponCreate
            )
            { name = textureName };

            m_textureShaderId = textureShaderId;
            m_textureWidthShaderId = textureWidthShaderId;
        }

        public GpuTexture(
            string textureName,
            Texture2D externalGPUTexture
        )
        {
            m_texture = externalGPUTexture;
            m_texture.name = textureName;
        }

        public void SetData(NativeArray<byte> bytesArray)
        {
            // TODO: We need to check if there is a copy from cpuBuffer -> Texture's CPU buffer here.
            m_texture.SetPixelData(bytesArray, mipLevel: 0);

            // Upload to GPU
            m_texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        }

        public void SetData(NativeArray<byte> bytesArray, int bytesOffset, int bytesToUpload)
        {
            throw new NotSupportedException("GPU Textures do not support upload of data with offsets");
        }
        
        public void SetBufferOnMaterial(Material material)
        {
            material.SetTexture(m_textureShaderId, m_texture);
            material.SetInt(m_textureWidthShaderId, m_texture.width);
        }

        public void SetBufferOnComputeShader(CommandBuffer commandBuffer, ComputeShader computeShader, int kernelIndex)
        {
            commandBuffer.SetComputeTextureParam(computeShader, kernelIndex, m_textureShaderId, m_texture);
            commandBuffer.SetComputeIntParam(computeShader, m_textureWidthShaderId, m_texture.width);
        }
        
        public void SetBufferOnComputeShader(CommandBuffer commandBuffer, ComputeShader computeShader, int kernelIndex, int paramId)
        {
            commandBuffer.SetComputeTextureParam(computeShader, kernelIndex, paramId, m_texture);
            commandBuffer.SetComputeIntParam(computeShader, m_textureWidthShaderId, m_texture.width);
        }

        public void Dispose()
        {
            GameObject.DestroyImmediate(m_texture);
            m_texture = null;
        }

        public int GetBlockDim()
        {
            return m_blockDim;
        }

        public int GetTotalBytes()
        {
            throw new NotSupportedException("GPU Textures do not support querying of total bytes");
        }
    }

    // Helper class to create a GpuBuffer from a (cpu) AttributeBuffer.
    public class GpuBufferFactory
    {
        static string s_profilerPrefix = "[GpuBufferFactory] ";

        // CPU Markers
        static readonly ProfilerMarker s_createCombinedGpuBuffer = new ProfilerMarker(
            s_profilerPrefix + "Create combined GPU buffer"
        );

        static readonly ProfilerMarker s_allocateCpuBuffer = new ProfilerMarker(
            s_profilerPrefix + "Allocating CPU buffer"
        );

        static readonly ProfilerMarker s_copyToCpuBuffer = new ProfilerMarker(
            s_profilerPrefix + "Copy to CPU buffer"
        );

        static readonly ProfilerMarker s_allocateGpuBuffer = new ProfilerMarker(
            s_profilerPrefix + "Allocating GPU buffer"
        );

        static readonly ProfilerMarker s_uploadToGpuBuffer = new ProfilerMarker(
            s_profilerPrefix + "Upload to GPU buffer"
        );


        // Create a single or combined GPU buffer from an array of one or more CPU buffers.
        static public void CreateGpuBuffer(
            AttributeBuffer[] cpuBuffers,
            int bufferShaderId, 
            int textureWidthShaderId,
            ref IGpuBuffer gpuBuffer,
            string name
        )
        {
            Debug.Assert(cpuBuffers.Length > 0);

            if (cpuBuffers.Length == 1)
            {
                CreateSingleGpuBuffer(cpuBuffers[0], bufferShaderId, textureWidthShaderId, ref gpuBuffer, name);
            }
            else
            {
                using (s_createCombinedGpuBuffer.Auto())
                {
                    CreateCombinedGpuBuffer(cpuBuffers, bufferShaderId, ref gpuBuffer, name);
                }
            }
        }

        static private IGpuBuffer CreateSingleGpuBuffer(AttributeBuffer cpuBuffer, int bufferShaderId, int textureWidthShaderId, ref IGpuBuffer gpuBuffer, [CanBeNull] string name)
        {
            gpuBuffer?.Dispose();

            int blockDim = cpuBuffer.GetBlockDim();

            if (cpuBuffer.IsTexture() && !cpuBuffer.IsGPUBuffer())
            {
                (int textureWidth, int textureHeight) = cpuBuffer.GetTextureSize();

                GraphicsFormat graphicsFormat = cpuBuffer.GetEncoding().ToGraphicsFormat();

                gpuBuffer = new GpuTexture(
                    cpuBuffer.GetSemantic().ToString(),
                    textureWidth,
                    textureHeight,
                    graphicsFormat,
                    bufferShaderId,
                    textureWidthShaderId,
                    blockDim
                );

                gpuBuffer.SetData(cpuBuffer.GetArray());
            }
            else
            {
                gpuBuffer = new GpuArray(cpuBuffer.GetTotalBytes(), bufferShaderId, blockDim, name);

                if(!cpuBuffer.IsGPUBuffer())
                    gpuBuffer.SetData(cpuBuffer.GetArray());
            }

            return gpuBuffer;
        }

        static private IGpuBuffer CreateCombinedGpuBuffer(
            AttributeBuffer[] cpuBuffers, 
            int bufferShaderId, 
            ref IGpuBuffer gpuBuffer,
            string name = null
        )
        {
            Debug.Assert(cpuBuffers.Length > 0);

            // Validate & extract total number of bytes to allocate.
            int totalBytes = 0;

            AttributeEncoding firstEncoding = cpuBuffers[0].GetEncoding();
            int blockDim = 0;

            for (int bufferIndex = 0; bufferIndex < cpuBuffers.Length; ++bufferIndex)
            {
                var cpuBuffer = cpuBuffers[bufferIndex];
                if (cpuBuffer.IsTexture())
                {
                    throw new NotSupportedException("Aggregate GPU buffers not supported for textures");
                }
                else if (firstEncoding != cpuBuffer.GetEncoding())
                {
                    throw new NotSupportedException(
                        $"All buffers must have the same encoding to be aggregated. " +
                        $"Expected {firstEncoding}, but cpuBuffer[{bufferIndex}] encoding is {cpuBuffer.GetEncoding()}"
                    );
                }
                blockDim = cpuBuffer.GetBlockDim();
                totalBytes += cpuBuffer.GetTotalBytes();
            }
            
            // Create new buffer if required.
            s_allocateGpuBuffer.Begin();
            if (gpuBuffer == null || totalBytes > gpuBuffer.GetTotalBytes())
            {
                gpuBuffer?.Dispose();
                gpuBuffer = new GpuArray(totalBytes, bufferShaderId, blockDim, name);
            }
            s_allocateGpuBuffer.End();
            
            return gpuBuffer;
        }
    }
}
