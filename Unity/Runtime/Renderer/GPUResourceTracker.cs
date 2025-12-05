// Copyright © 2024 Miris. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;

namespace Miris.Runtime
{
    // GPUResourceTracker tracks which AttributeBuffers we have uploaded to the GPU
    // The track function should be called for each data source semantic, if the attributeBuffer
    // for the source / semantic pair isn't on the GPU an upload command is enqueued and the copy
    // command to form the single render buffer.
    //
    // Once all buffers have been tracked, TriggerUpload to create the compute buffer copy commands.
    public class GPUResourceTracker
    {
        public void Dispose()
        {
              foreach (IGpuBuffer buffer in m_atlasBuffers) {
                buffer?.Dispose();
            }
            m_uploadBuffer?.Dispose();
        }

        public void LogAndResetStats()
        {
            m_stats.Log();
            m_stats.Reset();
        }

        public GPUResourceTracker(UInt64 bufferSize = 64UL * 1024 * 1024 * 10)
        {
            m_bufferSize = bufferSize;
            m_atlasBuffers.Add(new GpuArray((int)m_bufferSize, 0, 0, "atlasBuffer"));
            m_atlasAllocators.Add(new LinearAllocator(m_bufferSize));
            m_stats.SetCacheSize((int)bufferSize); 
        }

        public class AtlasIndexEntry
        {
            public int offset;
            public int size;

            public int lodIndex = -1;
            public Bounds bounds;
            public int atlasBufferIndex = -1;
        }

        private UInt64 m_bufferSize;
        private Dictionary<AquaHash, AtlasIndexEntry> m_resources = new Dictionary<AquaHash, AtlasIndexEntry>();

        private List<LinearAllocator> m_atlasAllocators = new (); // multi buffer support
        private List<IGpuBuffer> m_atlasBuffers = new (); // multi buffer support
        
        private IGpuBuffer m_uploadBuffer = null;                      // buffers to upload
        private List<AttributeBuffer> m_buffersToUpload = new ();      // list of native arrays
        
        private ResourceStats m_stats;

        private struct MergeEntry
        {
            public GaussianSplatDataSource dataSource;
            public AttributeSemantic semantic;
            public IGpuBuffer renderBuffer;
        }
        
        private List<MergeEntry> m_mergeCopy = new List<MergeEntry>();

        public bool Contains(AquaHash hash)
        {
            m_resources.TryGetValue(hash, out var indexEntry);
            return indexEntry == null ? false : (indexEntry.size > 0);
        }

        int GetActiveAtlasBufferIndex()
        {
            int activeAtlasBufferIndex = m_atlasAllocators.Count - 1;
            Debug.Assert(activeAtlasBufferIndex >= 0, $"activeAtlasBufferIndex is invalid");
            return activeAtlasBufferIndex;
        }

        void AllocateAtlasBufferIfNeeded(int allocationSize)
        {
            int activeAtlasBufferIndex = GetActiveAtlasBufferIndex();
            // check whether the entire upload could actually fit in the current atlas buffer
            // if not we need to create a new atlas buffer and allocator 
            if (!m_atlasAllocators[activeAtlasBufferIndex].CanAllocate((UInt64)allocationSize))
            {
                Debug.LogWarning($"[GPUResourceTracker] Creating new atlas buffer ({activeAtlasBufferIndex + 2} total buffers)");
                // create a new allocator and atlas buffer
                m_atlasBuffers.Add(new GpuArray((int)m_bufferSize, 0, 0, "atlasBuffer"));
                m_atlasAllocators.Add(new LinearAllocator(m_bufferSize));
                activeAtlasBufferIndex++;
                // update the stats high watermark reporting information
                m_stats.SetCacheSize((int)m_bufferSize * (activeAtlasBufferIndex + 1));
            }

            Debug.Assert(activeAtlasBufferIndex < m_atlasBuffers.Count);
            Debug.Assert(activeAtlasBufferIndex < m_atlasAllocators.Count);
        }

        public void Track(GaussianSplatDataSource[] dataSources, AttributeSemantic semantic, MosaicTextureToAtlasBufferConverter bufferConverter)
        {
            foreach (GaussianSplatDataSource dataSource in dataSources)
            {
                Track(dataSource.GetBuffer(semantic), dataSource.GetObjectBounds(), dataSource.GetLodIndex(), bufferConverter);
            }
        }

        // Check if the buffer is on the GPU and issue an upload if not.
        private bool Track(AttributeBuffer attributeBuffer, Bounds bounds, int lodIndex, MosaicTextureToAtlasBufferConverter bufferConverter)
        {
            AquaHash hash = attributeBuffer.GetAquaHash();
            
            if (m_resources.TryGetValue(hash, out AtlasIndexEntry _))
            {
                m_stats.AddCacheHit();
                return false;
            }
            
            if (attributeBuffer.IsGPUBuffer())
            {
                int allocationSize = attributeBuffer.GetTotalBytes();
                AllocateAtlasBufferIfNeeded(allocationSize);
                int activeAtlasBufferIndex = GetActiveAtlasBufferIndex();
                Debug.Assert(activeAtlasBufferIndex >= 0, $"activeAtlasBufferIndex is invalid");

                if (m_atlasAllocators[activeAtlasBufferIndex].CanAllocate((UInt64)allocationSize))
                {
                    int allocationOffset = (int)m_atlasAllocators[activeAtlasBufferIndex].Allocate((UInt64)allocationSize);
                    AtlasIndexEntry indexEntry = new AtlasIndexEntry();
                    indexEntry.size = allocationSize;
                    indexEntry.offset = allocationOffset;
                    indexEntry.atlasBufferIndex = activeAtlasBufferIndex;
                    m_resources.Add(hash, indexEntry);

                    Texture2D[] externalTextures = attributeBuffer.GetExternalTextures();
                    for (int i = 0; i < externalTextures.Length; ++i)
                    {
                        GpuTexture sourceBuffer = new("Mosaic frame", externalTextures[i]);
                        MosaicDescriptorInfo mosaicInfo = attributeBuffer.GetMosaicDescriptorInfos()[i];

                        bufferConverter.EnqueueBufferConversion(sourceBuffer, m_atlasBuffers[activeAtlasBufferIndex], attributeBuffer.GetElementCount(), allocationOffset, allocationSize, mosaicInfo);
                    }
                }
                else
                {
                    Debug.LogWarning($"[GPUResourceTracker] will not be able to fit new upload data of {allocationSize} bytes within atlas buffer. Upload aborted.");
                }
            }
            else if (attributeBuffer.IsBlockCompressed())
            {
                int allocationSize = attributeBuffer.GetComponentCount() * sizeof(float) * attributeBuffer.GetSplatCount();
                AllocateAtlasBufferIfNeeded(allocationSize);
                int activeAtlasBufferIndex = GetActiveAtlasBufferIndex();
                Debug.Assert(activeAtlasBufferIndex >= 0, $"activeAtlasBufferIndex is invalid");

                if (m_atlasAllocators[activeAtlasBufferIndex].CanAllocate((UInt64)allocationSize))
                {
                    int allocationOffset = (int)m_atlasAllocators[activeAtlasBufferIndex].Allocate((UInt64)allocationSize);
                    AtlasIndexEntry indexEntry = new AtlasIndexEntry();
                    indexEntry.size = allocationSize;
                    indexEntry.offset = allocationOffset;
                    indexEntry.atlasBufferIndex = activeAtlasBufferIndex;
                    m_resources.Add(hash, indexEntry);

                    GpuTexture sourceBuffer = new("ASTC texture", attributeBuffer.GetTexture());
                    var (minVec, maxVec) = attributeBuffer.GetMinMaxVectors();
                    IGpuBuffer blockBoundsBuffer = attributeBuffer.GetBlockBoundsGpuBuffer();
                    bool hasBlockBoundsBuffer = attributeBuffer.HasBlockBoundsBuffer();
                    bufferConverter.EnqueueBufferConversion(sourceBuffer, m_atlasBuffers[activeAtlasBufferIndex], attributeBuffer.GetSplatCount(), attributeBuffer.GetComponentCount(), allocationOffset, allocationSize, minVec, maxVec, attributeBuffer.GetIsRangeNormalized(), attributeBuffer.GetBlockDim(), blockBoundsBuffer, hasBlockBoundsBuffer, attributeBuffer.IsBlockScanlineOrder());
                }
                else
                {
                    Debug.LogWarning($"[GPUResourceTracker] will not be able to fit new upload data of {allocationSize} bytes within atlas buffer. Upload aborted.");
                }
            }
            else
            {
                m_buffersToUpload.Add(attributeBuffer);
            }

            return true;
        }

        // Generate the copy commands for the atlas -> render buffer copies
        // Not this is done unconditionally as we always need to build the complete render buffer from 
        // all atlas pages.
        public void Merge(GaussianSplatDataSource[] dataSources, AttributeSemantic semantic, IGpuBuffer unifiedBuffer,
            BufferCopier copier)
        {
            for (int sourceIndex = 0; sourceIndex < dataSources.Length; ++sourceIndex)
            {
                GaussianSplatDataSource dataSource = dataSources[sourceIndex];
                MergeEntry mergeEntry;

                mergeEntry.semantic = semantic;
                mergeEntry.dataSource = dataSource;
                mergeEntry.renderBuffer = unifiedBuffer;
                
                m_mergeCopy.Add(mergeEntry);
            }
        }

        // Create all the copy commands on the buffer copier (upload -> atlas -> render buffer)
        public void TriggerUpload(BufferCopier copier)
        {
            Dictionary<AquaHash, AttributeBuffer> uniqueBuffers = new();

            for (int bufferIndex = 0; bufferIndex < m_buffersToUpload.Count; ++bufferIndex)
            {
                AttributeBuffer buffer = m_buffersToUpload[bufferIndex];
                if (!uniqueBuffers.TryGetValue(buffer.GetAquaHash(), out AttributeBuffer _))
                {
                    uniqueBuffers.Add(buffer.GetAquaHash(), buffer);
                }
            }

            m_buffersToUpload.Clear();

            // determine storage requirements for new buffers
            var attributeBuffersToUpload = uniqueBuffers.Values.ToArray();

            // determine the size of any upload
            int totalUploadBytes = 0;
            for (int i = 0; i < attributeBuffersToUpload.Length; ++i)
            {
                AttributeBuffer attributeBuffer = attributeBuffersToUpload[i];
                totalUploadBytes += attributeBuffer.GetTotalBytes();
            }

            // tidy up the upload buffer to prevent retaining excess memory 
            // beyond the frame we performed the actual data upload (previous frame)

            // this ensures we allocate what we need for the upload or it 
            // resets the upload buffer to consume only 5Mb 
            int minUploadBufferSize = 5 * 1024 * 1024;
            minUploadBufferSize = Math.Max(minUploadBufferSize, totalUploadBytes);

            if (m_uploadBuffer == null || m_uploadBuffer.GetTotalBytes() != minUploadBufferSize)
            {
                Debug.LogWarning($"[GPUResourceTracker] Resizing upload buffer to {minUploadBufferSize} bytes ({minUploadBufferSize / 1024} Kb)");
                m_uploadBuffer?.Dispose();
                m_uploadBuffer = new GpuArray(minUploadBufferSize, 0, 0, "uploadBuffer");
            }

            if (attributeBuffersToUpload.Length > 0)
            {
                Debug.Assert(totalUploadBytes >= 0, $"[GPUResourceTracker] totalUploadBytes = 0 for {attributeBuffersToUpload.Length} attribute buffers");

                AllocateAtlasBufferIfNeeded(totalUploadBytes);
                int activeAtlasBufferIndex = GetActiveAtlasBufferIndex();
                Debug.Assert(activeAtlasBufferIndex >= 0, $"activeAtlasBufferIndex is invalid");

                // double-check that the entire upload could actually fit in the target atlas buffer
                // if not we should skip the transfer but we have to ensure we update the
                // total splat number accordingly
                if (m_atlasAllocators[activeAtlasBufferIndex].CanAllocate((UInt64)totalUploadBytes))
                {
                    using NativeArray<byte> cpuUploadData = new NativeArray<byte>(totalUploadBytes, Allocator.Temp);
                    int uploadBufferOffset_ = 0;
                    for (int i = 0; i < attributeBuffersToUpload.Length; ++i)
                    {
                        AttributeBuffer attributeBuffer = attributeBuffersToUpload[i];

                        NativeArray<byte> srcArray = attributeBuffer.GetArray();
                        srcArray.CopyTo(cpuUploadData.GetSubArray(uploadBufferOffset_, srcArray.Length));
                        uploadBufferOffset_ += srcArray.Length;
                    }

                    Debug.Assert(uploadBufferOffset_ == totalUploadBytes, $"expected: {totalUploadBytes}, actual: {uploadBufferOffset_}");

                    m_uploadBuffer.SetData(cpuUploadData);

                    // do the allocation and generate atlas index entries to track offsets
                    // then queue all uploads on the command buffer

                    int uploadBufferOffset = 0;
                    for (int i = 0; i < attributeBuffersToUpload.Length; ++i)
                    {
                        AttributeBuffer attributeBuffer = attributeBuffersToUpload[i];
                        AquaHash hash = attributeBuffer.GetAquaHash();

                        AtlasIndexEntry indexEntry = new AtlasIndexEntry();
                        int offset = (int)m_atlasAllocators[activeAtlasBufferIndex].Allocate((UInt64)attributeBuffer.GetTotalBytes());

                        if (offset >= 0)
                        {
                            indexEntry.offset = offset;
                            indexEntry.size = attributeBuffer.GetTotalBytes();
                            indexEntry.atlasBufferIndex = activeAtlasBufferIndex;

                            copier.EnqueueBufferCopy(m_uploadBuffer, m_atlasBuffers[activeAtlasBufferIndex], uploadBufferOffset, indexEntry.offset, indexEntry.size);
                            m_stats.AddUpload((int)indexEntry.size);
                            m_resources.Add(hash, indexEntry);
                        }
                        else
                        {
                            Debug.Assert(false);
                        }
                        uploadBufferOffset += attributeBuffer.GetArray().Length;
                    }
                }
                else
                {
                    Debug.LogWarning($"[GPUResourceTracker] will not be able to fit new upload data of {totalUploadBytes} bytes within atlas buffer. Upload aborted.");
                }
            }
        }

        public void TriggerCopy(BufferCopier copier)
        {
            // copy/merge data into single unified render buffers
            int destOffset = 0;

            for (int i = 0; i < m_mergeCopy.Count; ++i)
            {
                MergeEntry mergeEntry = m_mergeCopy[i];

                if (i > 0 && m_mergeCopy[i - 1].semantic != m_mergeCopy[i].semantic)
                {
                    destOffset = 0;
                }

                AttributeBuffer buffer = mergeEntry.dataSource.GetBuffer(mergeEntry.semantic);
                AquaHash hash = buffer.GetAquaHash();

                if (m_resources.TryGetValue(hash, out var indexEntry))
                {
                    Debug.Assert(indexEntry.size > 0);
                    Debug.Assert(indexEntry.atlasBufferIndex >= 0);
                    copier.EnqueueBufferCopy(m_atlasBuffers[indexEntry.atlasBufferIndex], mergeEntry.renderBuffer,
                        indexEntry.offset, destOffset, indexEntry.size);
                    destOffset += (int)indexEntry.size;
                }
                else
                {
                    Debug.Assert(false, $"[GPUResourceTracker] Attribute data not found in atlas buffer {indexEntry.atlasBufferIndex} hash: {hash.GetHashCode()} semantic: {mergeEntry.semantic.ToString()}");
                }
            }

            m_mergeCopy.Clear();
            

        }
    }
}