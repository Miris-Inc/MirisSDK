// Copyright © 2025 Miris. All rights reserved.

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Miris.Runtime
{
    public class GPUReduceAndScan: IDisposable {
        
        private static class ShaderIds
        {
            public static readonly int MaskedInputData = Shader.PropertyToID("maskedInputData");
            public static readonly int PrefixSums = Shader.PropertyToID("prefixSums");
            public static readonly int BlockSums = Shader.PropertyToID("blockSums");
            public static readonly int AdjustedBlockSums = Shader.PropertyToID("adjustedBlockSums");
            public static readonly int ScanSumsOutput = Shader.PropertyToID("scanSumsOutput");
            
            public static readonly int IndexInputData = Shader.PropertyToID("indexInputData");
            public static readonly int DepthInputData = Shader.PropertyToID("depthInputData");
            public static readonly int IndexOutputData = Shader.PropertyToID("indexOutputData");
            public static readonly int DepthOutputData = Shader.PropertyToID("depthOutputData");
            public static readonly int AdjustedBlockSumsLength = Shader.PropertyToID("adjustedBlockSumsLength");
            public static readonly int IndirectDrawBuffer = Shader.PropertyToID("indirectDrawBuffer");
            public static readonly int IndirectDispatchBuffer = Shader.PropertyToID("indirectDispatchBuffer");
            public static readonly int KeyCount = Shader.PropertyToID("_KeyCount");
            public static readonly int BlockCount = Shader.PropertyToID("_BlockCount");
        }
        
        
        private ComputeShader m_reduceScanKernel;
        
        private ComputeBuffer m_prefixSumBuffer;
        private ComputeBuffer m_blockSumBuffer;
        private ComputeBuffer m_adjustedBlockSumsBuffer;
        private ComputeBuffer m_scanSumsDataBuffer;

        private int m_localPrefixSumKernelID;
        private int m_blockSumPrefixSumKernelID;
        private int m_adjustAndCompactKernelID;
        private int m_streamCompactionKernelID;
        private int m_extractSizeKernelID; 
        private int m_clearDataKernelID;
        
        private const int BLOCK_SIZE = 1024;

        private int m_keyCount = 0;

        public GPUReduceAndScan(int keyCount, ref ComputeBuffer maskBuffer, ref ComputeBuffer outputDepth, ref ComputeBuffer outputIndex) {
            
            m_keyCount = keyCount;

            maskBuffer = new ComputeBuffer(m_keyCount, sizeof(uint));
            outputDepth= new ComputeBuffer(m_keyCount, sizeof(float));
            outputIndex = new ComputeBuffer(m_keyCount, sizeof(uint));
            
            InitializeBuffers();
            GetKernelIds();

        }

        private void InitializeBuffers() {
            
            m_prefixSumBuffer = new ComputeBuffer(m_keyCount, sizeof(uint));
            m_blockSumBuffer = new ComputeBuffer((m_keyCount+BLOCK_SIZE-1)/BLOCK_SIZE, sizeof(uint)); 
            m_adjustedBlockSumsBuffer = new ComputeBuffer(m_blockSumBuffer.count, sizeof(uint));
            m_scanSumsDataBuffer = new ComputeBuffer(m_keyCount, sizeof(uint));
            
        }

        private void GetKernelIds() {
            
            m_reduceScanKernel=(ComputeShader)Resources.Load("Shaders/GPUReduceScan");
            m_localPrefixSumKernelID=m_reduceScanKernel.FindKernel("LocalPrefixSum");
            m_blockSumPrefixSumKernelID=m_reduceScanKernel.FindKernel("BlockSumPrefixSum");
            m_adjustAndCompactKernelID=m_reduceScanKernel.FindKernel("AdjustAndCompact");
            m_streamCompactionKernelID=m_reduceScanKernel.FindKernel("StreamCompaction");
            m_extractSizeKernelID=m_reduceScanKernel.FindKernel("ExtractSize");
            m_clearDataKernelID=m_reduceScanKernel.FindKernel("ClearData");
        }

        public void RunReduceScan(
            CommandBuffer commandBuffer, 
            ComputeBuffer maskedInputDataBuffer, 
            ComputeBuffer depthInputBuffer, 
            ComputeBuffer depthOutputBuffer,
            ComputeBuffer indexInputBuffer, 
            ComputeBuffer indexOutputBuffer, 
            ComputeBuffer indirectDrawBuffer, 
            ComputeBuffer indirectDispatchBuffer
            ) {
            
            //int chunkSize = (totalElements+BLOCK_SIZE-1)/BLOCK_SIZE;
            int numGroups=(m_keyCount+BLOCK_SIZE-1) / BLOCK_SIZE;

            commandBuffer.SetComputeIntParam(m_reduceScanKernel, ShaderIds.KeyCount, m_keyCount);
            commandBuffer.SetComputeIntParam(m_reduceScanKernel, ShaderIds.BlockCount, m_blockSumBuffer.count);
            
            //Pre steps: Clear buffers
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_clearDataKernelID, ShaderIds.DepthOutputData, depthOutputBuffer);
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_clearDataKernelID, ShaderIds.IndexOutputData, indexOutputBuffer);
            commandBuffer.DispatchCompute(m_reduceScanKernel, m_clearDataKernelID, numGroups, 1, 1);
            
            //1. Dispatch localPrefixSum kernel
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_localPrefixSumKernelID, ShaderIds.MaskedInputData, maskedInputDataBuffer);
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_localPrefixSumKernelID, ShaderIds.BlockSums, m_blockSumBuffer);
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_localPrefixSumKernelID, ShaderIds.PrefixSums, m_prefixSumBuffer);
            
            commandBuffer.DispatchCompute(m_reduceScanKernel, m_localPrefixSumKernelID, numGroups, 1, 1);
            
            //2. Dispatch BlockSumPrefixSum Kernel
             commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_blockSumPrefixSumKernelID, ShaderIds.BlockSums, m_blockSumBuffer);
             commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_blockSumPrefixSumKernelID, ShaderIds.AdjustedBlockSums, m_adjustedBlockSumsBuffer);
            
             commandBuffer.DispatchCompute(m_reduceScanKernel, m_blockSumPrefixSumKernelID, (m_blockSumBuffer.count+BLOCK_SIZE-1)/BLOCK_SIZE, 1, 1);
            
            //3. Dispatch AdjustAndCompact kernel
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_adjustAndCompactKernelID, ShaderIds.PrefixSums, m_prefixSumBuffer);
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_adjustAndCompactKernelID, ShaderIds.AdjustedBlockSums, m_adjustedBlockSumsBuffer);
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_adjustAndCompactKernelID, ShaderIds.ScanSumsOutput, m_scanSumsDataBuffer);
            
            commandBuffer.DispatchCompute(m_reduceScanKernel, m_adjustAndCompactKernelID, numGroups, 1, 1);
            
            //4. Dispatch ExtractSize kernel
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_extractSizeKernelID, ShaderIds.AdjustedBlockSums, m_adjustedBlockSumsBuffer);
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_extractSizeKernelID, ShaderIds.IndirectDrawBuffer, indirectDrawBuffer);
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_extractSizeKernelID, ShaderIds.IndirectDispatchBuffer, indirectDispatchBuffer);
            commandBuffer.SetComputeIntParam(m_reduceScanKernel,ShaderIds.AdjustedBlockSumsLength, m_blockSumBuffer.count);
            
            commandBuffer.DispatchCompute(m_reduceScanKernel, m_extractSizeKernelID, 1, 1, 1);

            //5. Dispatch CompactDepth kernel
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_streamCompactionKernelID, ShaderIds.MaskedInputData, maskedInputDataBuffer);
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_streamCompactionKernelID, ShaderIds.DepthInputData, depthInputBuffer);
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_streamCompactionKernelID, ShaderIds.DepthOutputData, depthOutputBuffer);
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_streamCompactionKernelID, ShaderIds.IndexInputData, indexInputBuffer);
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_streamCompactionKernelID, ShaderIds.IndexOutputData, indexOutputBuffer);
            commandBuffer.SetComputeBufferParam(m_reduceScanKernel, m_streamCompactionKernelID, ShaderIds.ScanSumsOutput, m_scanSumsDataBuffer);
            
            commandBuffer.DispatchCompute(m_reduceScanKernel, m_streamCompactionKernelID, numGroups, 1, 1);
            
        }
        

        public void Dispose() {
            
            m_prefixSumBuffer.Release();
            m_blockSumBuffer.Release();
            m_adjustedBlockSumsBuffer.Release();
            m_scanSumsDataBuffer.Release();
            
        }



    }
}
