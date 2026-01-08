// Copyright © 2025 Miris, Inc. All rights reserved.

using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Miris.Runtime
{
    // Helper class for enqueing a GPU mosaic frames -> conversion to standard splat format on the GPU
    public class MosaicTextureToAtlasBufferConverter
    {
        private List<BufferConvertCommand> m_convertCommands = new();
        private List<BufferAstcConvertCommand> m_convertAstcCommands = new();
        private Texture2D m_eccLut;

        private ComputeShader m_DecodeMosaicTextureToAtlasBufferShader;
        private int m_DecodeMosaicTextureToAtlasBufferKernel;

        private static readonly int ConvertBufferSrc = Shader.PropertyToID("_sourceBuffer");
        private static readonly int LutEccTexture = Shader.PropertyToID("_eccLutTexture");
        
        private static readonly int ConvertBufferDst = Shader.PropertyToID("_destBuffer");

        private static readonly int ConvertNumElements = Shader.PropertyToID("_srcNumSplats");


        private static readonly int ConvertBufferLength = Shader.PropertyToID("_AllocationLength");

        private static readonly int IsAstcTextureFlag = Shader.PropertyToID("_IsAstcTexture");
        private static readonly int SourceComponentCount = Shader.PropertyToID("_SourceComponentCount");



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
        private static readonly int SrcMosaicMinVec = Shader.PropertyToID("_SrcMosaicMinVec");
        private static readonly int SrcMosaicMaxVec = Shader.PropertyToID("_SrcMosaicMaxVec");
        private static readonly int BlockDim = Shader.PropertyToID("_BlockDim");
        private static readonly int isSHColor = Shader.PropertyToID("_SrcIsSHColor");
        private static readonly int isRangeNormalized = Shader.PropertyToID("_IsRangeNormalized");
        private static readonly int EccPart = Shader.PropertyToID("_EccPart");
        private static readonly int BlockBoundsBuffer = Shader.PropertyToID("_blockBoundsBuffer");
        private static readonly int HasBlockBounds = Shader.PropertyToID("_HasBlockBounds");
        private static readonly int HasBlockScanLineOrder = Shader.PropertyToID("_HasBlockScanLineOrder");


        public static readonly ProfilerMarker s_ConvertBufferMarker = new ProfilerMarker("ConvertBuffers");
        public MosaicTextureToAtlasBufferConverter()
        {
            m_DecodeMosaicTextureToAtlasBufferShader = ComputeShader.Instantiate((ComputeShader)Resources.Load("Shaders/DecodeMosaicTextureToAtlasBuffer"));
            m_DecodeMosaicTextureToAtlasBufferKernel = m_DecodeMosaicTextureToAtlasBufferShader.FindKernel("ConvertBufferMain");
            m_eccLut = Miris.Runtime.MirisApi.GetEccLUT();
        }

        public struct BufferAstcConvertCommand
        {
            public GpuTexture sourceTexture;

            public int splatCount;
            public int sourceComponentCount;
            public IGpuBuffer destinationBuffer;
            public int dstAllocationOffset;
            public int dstAllocationLength;
            public Vector4 minVec; // Per-component min values
            public Vector4 maxVec; // Per-component max values
            public bool isRangeNormalized;
            public int blockDim;
            public IGpuBuffer blockBoundsBuffer; // Block bounds data
            public bool hasBlockBounds;
            public bool blockScanLineOrder;
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

        public void EnqueueBufferConversion(GpuTexture sourceBuffer, IGpuBuffer destinationBuffer, int splatCount, int sourceComponentCount, int allocationOffset, int allocationLength, Vector4 minVec, Vector4 maxVec, bool isRangeNormalized, int blockDim, IGpuBuffer blockBoundsBuffer = null, bool hasBlockBoundsBuffer = false, bool blockScanlineOrder = true)
        {

            BufferAstcConvertCommand command;

            command.sourceTexture = sourceBuffer;
            command.destinationBuffer = destinationBuffer;

            Debug.Assert(allocationOffset % 4 == 0, "allocationOffset has to be 4 byte aligned as our buffers are sizeof(int) strided");
            Debug.Assert(allocationLength % 4 == 0, "length has to be 4 byte aligned as our buffers are sizeof(int) strided");

            command.dstAllocationOffset = allocationOffset / 4;
            command.dstAllocationLength = allocationLength / 4;
            command.sourceComponentCount = sourceComponentCount;
            command.splatCount = splatCount;
            command.minVec = minVec;
            command.maxVec = maxVec;
            command.isRangeNormalized = isRangeNormalized;
            command.blockDim = blockDim;
            command.blockBoundsBuffer = blockBoundsBuffer;
            command.hasBlockBounds = hasBlockBoundsBuffer;
            command.blockScanLineOrder = blockScanlineOrder;
            m_convertAstcCommands.Add(command);
        }


        // Add all enqueued copy commands to the commandBuffer
        public void Execute(CommandBuffer commandBuffer)
        {
            if (m_convertCommands.Count == 0 && m_convertAstcCommands.Count == 0)
            {
                return;
            }
            commandBuffer.BeginSample(s_ConvertBufferMarker);
            commandBuffer.SetComputeTextureParam(m_DecodeMosaicTextureToAtlasBufferShader, m_DecodeMosaicTextureToAtlasBufferKernel, LutEccTexture, m_eccLut);
            foreach (BufferConvertCommand command in m_convertCommands)
            {
                Debug.Assert(command.sourceBuffer != null);
                Debug.Assert(command.destinationBuffer != null);
                Debug.Assert(command.dstAllocationLength > 0);
                
                command.sourceBuffer.SetBufferOnComputeShader(commandBuffer, m_DecodeMosaicTextureToAtlasBufferShader, m_DecodeMosaicTextureToAtlasBufferKernel, ConvertBufferSrc);
                command.destinationBuffer.SetBufferOnComputeShader(commandBuffer, m_DecodeMosaicTextureToAtlasBufferShader, m_DecodeMosaicTextureToAtlasBufferKernel, ConvertBufferDst);

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

                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, IsAstcTextureFlag, 0);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, HasBlockScanLineOrder, 0);


                var (threadGroupCountX, _, _) = ComputeKernelUtils.CalculateThreadGroupCount(
                    m_DecodeMosaicTextureToAtlasBufferShader, 
                    m_DecodeMosaicTextureToAtlasBufferKernel, 
                    command.sourceDataSize
                );

                //Dispatch the compute shader
                commandBuffer.DispatchCompute(m_DecodeMosaicTextureToAtlasBufferShader, m_DecodeMosaicTextureToAtlasBufferKernel, threadGroupCountX, 1, 1);
            }

            foreach(BufferAstcConvertCommand command in m_convertAstcCommands)
            {
                Debug.Assert(command.sourceTexture != null);
                Debug.Assert(command.destinationBuffer != null);
                Debug.Assert(command.dstAllocationLength > 0);

                command.sourceTexture.SetBufferOnComputeShader(commandBuffer, m_DecodeMosaicTextureToAtlasBufferShader, m_DecodeMosaicTextureToAtlasBufferKernel, ConvertBufferSrc);
                command.destinationBuffer.SetBufferOnComputeShader(commandBuffer, m_DecodeMosaicTextureToAtlasBufferShader, m_DecodeMosaicTextureToAtlasBufferKernel, ConvertBufferDst);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, ConvertBufferLength, (int)command.dstAllocationLength);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, ConvertBufferDestOffset, (int)command.dstAllocationOffset);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, SourceComponentCount, (int)command.sourceComponentCount);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, IsAstcTextureFlag, 1);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, ConvertNumElements, command.splatCount);
                
                // Set per-component min/max vectors for ASTC textures
                commandBuffer.SetComputeVectorParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicMinVec, command.minVec);
                commandBuffer.SetComputeVectorParam(m_DecodeMosaicTextureToAtlasBufferShader, SrcMosaicMaxVec, command.maxVec);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, isRangeNormalized, command.isRangeNormalized ? 1 : 0);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, BlockDim, command.blockDim);
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, HasBlockScanLineOrder, command.blockScanLineOrder ? 1 : 0);

                // Set block bounds buffer if available
                if (command.blockBoundsBuffer != null)
                {
                    command.blockBoundsBuffer.SetBufferOnComputeShader(commandBuffer, m_DecodeMosaicTextureToAtlasBufferShader, m_DecodeMosaicTextureToAtlasBufferKernel, BlockBoundsBuffer);
                }
                commandBuffer.SetComputeIntParam(m_DecodeMosaicTextureToAtlasBufferShader, HasBlockBounds, command.hasBlockBounds ? 1 : 0);

                var (threadGroupCountX, _, _) = ComputeKernelUtils.CalculateThreadGroupCount(
                    m_DecodeMosaicTextureToAtlasBufferShader,
                    m_DecodeMosaicTextureToAtlasBufferKernel,
                    command.splatCount
                );

                //Dispatch the compute shader
                commandBuffer.DispatchCompute(m_DecodeMosaicTextureToAtlasBufferShader, m_DecodeMosaicTextureToAtlasBufferKernel, threadGroupCountX, 1, 1);
            }
            commandBuffer.EndSample(s_ConvertBufferMarker);
            m_convertCommands.Clear();
            m_convertAstcCommands.Clear();
        }

    }
}