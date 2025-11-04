// Copyright © 2024 Miris. All rights reserved.

using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aqua.Runtime
{
    // Helper class for enqueing a GPU mosaic frames -> conversion to standard splat format on the GPU
    public class MosaicTextureToAtlasBufferConverter
    {
        private List<BufferConvertCommand> m_convertCommands = new();
        private Texture2D m_eccLut;

        private ComputeShader m_DecodeMosaicTextureToAtlasBufferShader;
        private int m_DecodeMosaicTextureToAtlasBufferKernel;

        private static readonly int ConvertBufferSrc = Shader.PropertyToID("_sourceBuffer");
        private static readonly int LutEccTexture = Shader.PropertyToID("_eccLutTexture");
        
        private static readonly int ConvertBufferDst = Shader.PropertyToID("_destBuffer");

        private static readonly int ConvertNumElements = Shader.PropertyToID("_srcNumSplats");


        private static readonly int ConvertBufferLength = Shader.PropertyToID("_AllocationLength");
        private static readonly int ConvertBufferDestOffset = Shader.PropertyToID("_AllocationOffset");

        private static readonly int SrcMosaicStride = Shader.PropertyToID("_SrcMosaicStride");
        private static readonly int SrcMosaicOffset = Shader.PropertyToID("_SrcMosaicOffset");

        private static readonly int SrcMosaicTileWidth = Shader.PropertyToID("_SrcMosaicTileWidth");
        private static readonly int SrcMosaicTileHeight = Shader.PropertyToID("_SrcMosaicTileHeight");
        private static readonly int SrcMosaicTileX = Shader.PropertyToID("_SrcMosaicTileX");
        private static readonly int SrcMosaicTileY = Shader.PropertyToID("_SrcMosaicTileY");
        private static readonly int SrcMosaicInterleaveType = Shader.PropertyToID("_SrcMosaicInterleaveType");
        private static readonly int SrcMosaicTextureWidth = Shader.PropertyToID("_SrcMosaicTextureWidth");
        private static readonly int SrcMosaicTextureHeight = Shader.PropertyToID("_SrcMosaicTextureHeight");
        private static readonly int SrcMosaicMin = Shader.PropertyToID("_SrcMosaicMin");
        private static readonly int SrcMosaicMax = Shader.PropertyToID("_SrcMosaicMax");
        private static readonly int isSHColor = Shader.PropertyToID("_SrcIsSHColor");
        private static readonly int isRangeNormalized = Shader.PropertyToID("_IsRangeNormalized");
        private static readonly int EccPart = Shader.PropertyToID("_EccPart");


        public static readonly ProfilerMarker s_ConvertBufferMarker = new ProfilerMarker("ConvertBuffers");
        public MosaicTextureToAtlasBufferConverter()
        {
            m_DecodeMosaicTextureToAtlasBufferShader = ComputeShader.Instantiate((ComputeShader)Resources.Load("Shaders/DecodeMosaicTextureToAtlasBuffer"));
            m_DecodeMosaicTextureToAtlasBufferKernel = m_DecodeMosaicTextureToAtlasBufferShader.FindKernel("ConvertBufferMain");
            m_eccLut = Aqua.Runtime.AquaUnityApi.GetEccLUT();
        }

        public struct BufferConvertCommand
        {
            public GpuTexture sourceBuffer;
            public int sourceDataSize;
            public IGpuBuffer destinationBuffer;
            public int dstAllocationOffset;
            public int dstAllocationLength;
            public MosaicDescriptorInfo srcMosaic;
        }


        // Add a new buffer copy to be triggered during rendering
        public void EnqueueBufferConversion(GpuTexture sourceBuffer, IGpuBuffer destinationBuffer, int sourceDataSize, int allocationOffset, int allocationLength, MosaicDescriptorInfo mosaicSource)
        {
            BufferConvertCommand command;

            command.sourceBuffer = sourceBuffer;
            command.destinationBuffer = destinationBuffer;
            
            Debug.Assert( allocationOffset % 4 == 0, "allocationOffset has to be 4 byte aligned as our buffers are sizeof(int) strided");
            Debug.Assert( allocationLength % 4 == 0, "length has to be 4 byte aligned as our buffers are sizeof(int) strided");

            command.sourceDataSize = sourceDataSize;
            command.dstAllocationOffset = allocationOffset / 4;
            command.dstAllocationLength = allocationLength / 4;
            command.srcMosaic = mosaicSource;
            m_convertCommands.Add(command);
        }

        // Add all enqueued copy commands to the commandBuffer
        public void Execute(CommandBuffer commandBuffer)
        {
            if (m_convertCommands.Count == 0)
            {
                return;
            }
            commandBuffer.BeginSample(s_ConvertBufferMarker);
            foreach (BufferConvertCommand command in m_convertCommands)
            {
                Debug.Assert(command.sourceBuffer != null);
                Debug.Assert(command.destinationBuffer != null);
                Debug.Assert(command.dstAllocationLength > 0);
                
                command.sourceBuffer.SetBufferOnComputeShader(commandBuffer, m_DecodeMosaicTextureToAtlasBufferShader, m_DecodeMosaicTextureToAtlasBufferKernel, ConvertBufferSrc);
                command.destinationBuffer.SetBufferOnComputeShader(commandBuffer, m_DecodeMosaicTextureToAtlasBufferShader, m_DecodeMosaicTextureToAtlasBufferKernel, ConvertBufferDst);

                commandBuffer.SetComputeTextureParam(m_DecodeMosaicTextureToAtlasBufferShader, m_DecodeMosaicTextureToAtlasBufferKernel, LutEccTexture, m_eccLut);

                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, ConvertBufferLength, (int)command.dstAllocationLength);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, ConvertBufferDestOffset, (int)command.dstAllocationOffset);

                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, ConvertNumElements, command.sourceDataSize);

                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicStride, command.srcMosaic.m_stride);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicOffset, command.srcMosaic.m_offset);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicTileWidth, command.srcMosaic.m_mosaicTileWidth);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicTileHeight, command.srcMosaic.m_mosaicTileHeight);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicTileX, command.srcMosaic.m_mosaicTileX);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicTileY, command.srcMosaic.m_mosaicTileY);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicInterleaveType, command.srcMosaic.m_interleaveType);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicTextureWidth, command.srcMosaic.m_textureWidth);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicTextureHeight, command.srcMosaic.m_textureHeight);
                commandBuffer.SetComputeFloatParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicMin, command.srcMosaic.m_min);
                commandBuffer.SetComputeFloatParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicMax, command.srcMosaic.m_max);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, isSHColor, command.srcMosaic.m_isShColor);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, isRangeNormalized, command.srcMosaic.m_isRangeNormalized);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, EccPart, command.srcMosaic.m_eccPart);



                var (threadGroupCountX, _, _) = ComputeKernelUtils.CalculateThreadGroupCount(
                    m_DecodeMosaicTextureToAtlasBufferShader, 
                    m_DecodeMosaicTextureToAtlasBufferKernel, 
                    command.sourceDataSize
                );

                //Dispatch the compute shader
                commandBuffer.DispatchCompute(m_DecodeMosaicTextureToAtlasBufferShader, m_DecodeMosaicTextureToAtlasBufferKernel, threadGroupCountX, 1, 1);
            }
            commandBuffer.EndSample(s_ConvertBufferMarker);
            m_convertCommands.Clear();
        }

    }
}