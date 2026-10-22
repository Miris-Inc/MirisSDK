// Copyright © 2026 Miris, Inc. All rights reserved.

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
            // Occupancy comes from the allocators; the stats only see upload traffic.
            UInt64 usedBytes = 0;
            foreach (LinearAllocator allocator in m_atlasAllocators)
            {
                usedBytes += allocator.UsedBytes;
            }

            m_stats.SetUsedSize((long)usedBytes);
            m_stats.Log();
            m_stats.Reset();
        }

        // Granularity is chosen per tracker from its first upload rather than fixed: no one size
        // suits both a sub-megabyte flipbook frame and a 40 MB asset. Growth covers the rest.
        private const UInt64 c_minAtlasBufferBytes = 4UL * 1024 * 1024;
        private const UInt64 c_maxAtlasBufferBytes = 64UL * 1024 * 1024;

        // Headroom over the first request, so a tracker that grows a little does not immediately
        // need a second buffer.
        private const UInt64 c_atlasHeadroomFactor = 4;

        // LinearAllocator satisfies sizes strictly below its capacity, so a buffer cut to fit a
        // single entry needs slack above it.
        private const UInt64 c_atlasBufferSlack = 4096;

        public GPUResourceTracker(UInt64 maxBufferSize = c_maxAtlasBufferBytes)
        {
            // An oversized GraphicsBuffer throws from its constructor, so nothing may exceed what
            // this GPU hands out. A platform that cannot report a limit returns a negative value,
            // which must not be cast into a huge one. int.MaxValue because GpuArray takes an int.
            long deviceMaxBufferSize = SystemInfo.maxGraphicsBufferSize;
            m_deviceMaxBufferSize = deviceMaxBufferSize > 0
                ? Math.Min((UInt64)deviceMaxBufferSize, int.MaxValue)
                : int.MaxValue;

            m_maxBufferSize = Math.Min(maxBufferSize, m_deviceMaxBufferSize);
            if (m_maxBufferSize < maxBufferSize)
            {
                MirisDebug.Log($"GPUResourceTracker: atlas buffer capped at {m_maxBufferSize} "
                               + $"bytes, this device's maximum, from the requested {maxBufferSize}");
            }

        }

        // The request scaled by the headroom factor, clamped to the granularity range - except
        // that an entry too large for the maximum gets a buffer cut to fit it, so granularity
        // stays a tuning number rather than a limit on what an asset may contain.
        private UInt64 ChooseBufferSize(int allocationSize)
        {
            UInt64 dedicated = (UInt64)allocationSize + c_atlasBufferSlack;

            UInt64 size = (UInt64)allocationSize * c_atlasHeadroomFactor;
            size = Math.Max(size, c_minAtlasBufferBytes);
            size = Math.Min(size, m_maxBufferSize);

            return Math.Max(size, dedicated);
        }

        // Appends an atlas buffer and the allocator that hands out offsets into it. The first is
        // created on the first upload, not in the constructor: a renderer exists well before it
        // has anything to draw, and one whose data sources are all empty or frustum-culled never
        // reaches Track at all.
        private void AddAtlasBuffer(UInt64 sizeBytes)
        {
            m_atlasBuffers.Add(new GpuArray((int)sizeBytes, 0, 0, "atlasBuffer"));
            m_atlasAllocators.Add(new LinearAllocator(sizeBytes));

            m_totalAtlasBytes += sizeBytes;
            m_stats.SetCacheSize((long)m_totalAtlasBytes);
        }

        public class AtlasIndexEntry
        {
            public int offset;
            public int size;

            public int lodIndex = -1;
            public Bounds bounds;
            public int atlasBufferIndex = -1;

            public int lastUsedFrame;
        }

        private UInt64 m_maxBufferSize;
        private UInt64 m_deviceMaxBufferSize;
        private UInt64 m_totalAtlasBytes;
        private Dictionary<Hash128, AtlasIndexEntry> m_resources = new Dictionary<Hash128, AtlasIndexEntry>();

        private List<LinearAllocator> m_atlasAllocators = new (); // multi buffer support
        private List<IGpuBuffer> m_atlasBuffers = new (); // multi buffer support
        
        private const int c_minUploadBufferBytes = 1024 * 1024;

        private IGpuBuffer m_uploadBuffer = null;                      // buffers to upload
        private List<AttributeBuffer> m_buffersToUpload = new ();      // list of native arrays
        
        private ResourceStats m_stats;

        // Frames an atlas buffer must go untouched before it can be reclaimed. Covers GPU lag as
        // much as staleness: a command buffer from an earlier frame may still be reading the
        // region, and reusing it under a pending copy corrupts splats rather than crashing.
        private const int c_reclaimIdleFrames = 30;

        private readonly List<Hash128> m_evictionScratch = new();

        private struct MergeEntry
        {
            public AttributeBuffer attributeBuffer;
            public AttributeSemantic semantic;
            public IGpuBuffer renderBuffer;
        }
        
        private List<MergeEntry> m_mergeCopy = new List<MergeEntry>();

        public bool Contains(Hash128 hash)
        {
            m_resources.TryGetValue(hash, out var indexEntry);
            if (indexEntry == null)
            {
                return false;
            }

            indexEntry.lastUsedFrame = Time.frameCount;
            return indexEntry.size > 0;
        }

        // -1 before the first upload, when no atlas buffer exists yet.
        int GetActiveAtlasBufferIndex()
        {
            return m_atlasAllocators.Count - 1;
        }

        // Returns the buffer to allocate allocationSize from, creating or growing as needed, or
        // -1 when no GraphicsBuffer on this device could hold it. Callers must treat -1 as "skip
        // this upload": there is not always an allocator to fall back on.
        int AllocateAtlasBufferIfNeeded(int allocationSize)
        {
            if ((UInt64)allocationSize + c_atlasBufferSlack > m_deviceMaxBufferSize)
            {
                Debug.LogError($"[GPUResourceTracker] upload of {allocationSize} bytes exceeds this "
                               + $"device's maximum GraphicsBuffer of {m_deviceMaxBufferSize} bytes "
                               + $"and never will fit. Not allocating.");
                return -1;
            }

            // First upload asked of this tracker, so there is nothing to grow yet.
            if (m_atlasAllocators.Count == 0)
            {
                AddAtlasBuffer(ChooseBufferSize(allocationSize));
                return GetActiveAtlasBufferIndex();
            }

            // Any buffer with room will do: entries are addressed by the index they record, not
            // by living in the newest buffer, so a reclaimed buffer can fill back up.
            for (int bufferIndex = 0; bufferIndex < m_atlasAllocators.Count; bufferIndex++)
            {
                if (m_atlasAllocators[bufferIndex].CanAllocate((UInt64)allocationSize))
                {
                    return bufferIndex;
                }
            }

            // Reclaiming costs a re-upload if the data comes back; growing costs the buffer for
            // the life of the tracker, so try to reclaim first.
            int reclaimedIndex = TryReclaimAtlasBuffer();
            if (reclaimedIndex >= 0 && m_atlasAllocators[reclaimedIndex].CanAllocate((UInt64)allocationSize))
            {
                return reclaimedIndex;
            }

            AddAtlasBuffer(ChooseBufferSize(allocationSize));
            MirisDebug.Log($"[GPUResourceTracker] Creating new atlas buffer "
                           + $"({m_atlasBuffers.Count} total, {m_totalAtlasBytes} bytes)");
            return GetActiveAtlasBufferIndex();
        }

        // Empties the first atlas buffer whose every entry has gone untouched for
        // c_reclaimIdleFrames and returns its index, or -1 while every buffer holds recent data.
        //
        // Whole buffers, because LinearAllocator can only reset in full. Safe because the atlas
        // is a cache: TriggerCopy copies out of it into the render buffers that drawing reads,
        // so dropping an entry costs an upload rather than a frame of missing geometry.
        private int TryReclaimAtlasBuffer()
        {
            int currentFrame = Time.frameCount;

            for (int bufferIndex = 0; bufferIndex < m_atlasBuffers.Count; bufferIndex++)
            {
                m_evictionScratch.Clear();
                bool inUse = false;

                foreach (KeyValuePair<Hash128, AtlasIndexEntry> resource in m_resources)
                {
                    if (resource.Value.atlasBufferIndex != bufferIndex)
                    {
                        continue;
                    }

                    if (currentFrame - resource.Value.lastUsedFrame < c_reclaimIdleFrames)
                    {
                        inUse = true;
                        break;
                    }

                    m_evictionScratch.Add(resource.Key);
                }

                if (inUse)
                {
                    continue;
                }

                foreach (Hash128 hash in m_evictionScratch)
                {
                    m_resources.Remove(hash);
                }

                m_atlasAllocators[bufferIndex].Reset();
                m_stats.AddEviction();
                MirisDebug.Log($"[GPUResourceTracker] Reclaimed atlas buffer {bufferIndex}, "
                               + $"dropping {m_evictionScratch.Count} entries idle for "
                               + $"{c_reclaimIdleFrames}+ frames");
                return bufferIndex;
            }

            return -1;
        }

        // Folds the -1 from AllocateAtlasBufferIfNeeded into the capacity check callers make.
        private bool CanAllocateIn(int atlasBufferIndex, int size)
        {
            return atlasBufferIndex >= 0
                && m_atlasAllocators[atlasBufferIndex].CanAllocate((UInt64)size);
        }

        public void Track(AttributeBuffer[] attributeBuffers, MosaicTextureToAtlasBufferConverter bufferConverter)
        {
            foreach (AttributeBuffer buffer in attributeBuffers)
            {
                Track(buffer, bufferConverter);
            }
        }

        // Check if the buffer is on the GPU and issue an upload if not.
        private bool Track(AttributeBuffer attributeBuffer, MosaicTextureToAtlasBufferConverter bufferConverter)
        {
            Hash128 hash = attributeBuffer.GetHash();
            
            if (m_resources.TryGetValue(hash, out AtlasIndexEntry cachedEntry))
            {
                cachedEntry.lastUsedFrame = Time.frameCount;
                m_stats.AddCacheHit();
                return false;
            }
            
            if (attributeBuffer.IsGPUBuffer())
            {
                int allocationSize = attributeBuffer.GetTotalBytes();
                int activeAtlasBufferIndex = AllocateAtlasBufferIfNeeded(allocationSize);

                if (CanAllocateIn(activeAtlasBufferIndex, allocationSize))
                {
                    int allocationOffset = (int)m_atlasAllocators[activeAtlasBufferIndex].Allocate((UInt64)allocationSize);
                    AtlasIndexEntry indexEntry = new AtlasIndexEntry();
                    indexEntry.size = allocationSize;
                    indexEntry.offset = allocationOffset;
                    indexEntry.atlasBufferIndex = activeAtlasBufferIndex;
                    indexEntry.lastUsedFrame = Time.frameCount;
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
                int activeAtlasBufferIndex = AllocateAtlasBufferIfNeeded(allocationSize);

                if (CanAllocateIn(activeAtlasBufferIndex, allocationSize))
                {
                    int allocationOffset = (int)m_atlasAllocators[activeAtlasBufferIndex].Allocate((UInt64)allocationSize);
                    AtlasIndexEntry indexEntry = new AtlasIndexEntry();
                    indexEntry.size = allocationSize;
                    indexEntry.offset = allocationOffset;
                    indexEntry.atlasBufferIndex = activeAtlasBufferIndex;
                    indexEntry.lastUsedFrame = Time.frameCount;
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
        public void Merge(AttributeBuffer[] attributeBuffers, AttributeSemantic semantic, IGpuBuffer unifiedBuffer,
            BufferCopier copier)
        {
            for (int index = 0; index < attributeBuffers.Length; ++index)
            {
                MergeEntry mergeEntry;

                mergeEntry.semantic = semantic;
                mergeEntry.attributeBuffer = attributeBuffers[index];
                mergeEntry.renderBuffer = unifiedBuffer;
                
                m_mergeCopy.Add(mergeEntry);
            }
        }

        // Create all the copy commands on the buffer copier (upload -> atlas -> render buffer)
        public void TriggerUpload(BufferCopier copier)
        {
            Dictionary<Hash128, AttributeBuffer> uniqueBuffers = new();

            for (int bufferIndex = 0; bufferIndex < m_buffersToUpload.Count; ++bufferIndex)
            {
                AttributeBuffer buffer = m_buffersToUpload[bufferIndex];
                if (!uniqueBuffers.TryGetValue(buffer.GetHash(), out AttributeBuffer _))
                {
                    uniqueBuffers.Add(buffer.GetHash(), buffer);
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
            // Grow only. Batch sizes alternate between almost nothing and the working size, so
            // any shrink threshold this side of that ratio just oscillates, reallocating a
            // GraphicsBuffer each way while Metal defers the frees.
            int requiredUploadBytes = Math.Max(c_minUploadBufferBytes, totalUploadBytes);
            int currentUploadBytes = m_uploadBuffer?.GetTotalBytes() ?? 0;

            if (m_uploadBuffer == null || currentUploadBytes < requiredUploadBytes)
            {
                MirisDebug.Log($"[GPUResourceTracker] Resizing upload buffer to {requiredUploadBytes} bytes ({requiredUploadBytes / 1024} Kb)");
                m_uploadBuffer?.Dispose();
                m_uploadBuffer = new GpuArray(requiredUploadBytes, 0, 0, "uploadBuffer");
            }

            if (attributeBuffersToUpload.Length > 0)
            {
                Debug.Assert(totalUploadBytes >= 0, $"[GPUResourceTracker] totalUploadBytes = 0 for {attributeBuffersToUpload.Length} attribute buffers");

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
                //
                // Each entry is placed independently rather than reserving the whole batch in
                // one buffer, so granularity need only exceed the largest single attribute
                // buffer. A batch may straddle buffers: TriggerCopy reads each entry back from
                // the buffer its own index names.
                int uploadBufferOffset = 0;
                for (int i = 0; i < attributeBuffersToUpload.Length; ++i)
                {
                    AttributeBuffer attributeBuffer = attributeBuffersToUpload[i];
                    Hash128 hash = attributeBuffer.GetHash();
                    int entrySize = attributeBuffer.GetTotalBytes();

                    int atlasBufferIndex = AllocateAtlasBufferIfNeeded(entrySize);
                    if (CanAllocateIn(atlasBufferIndex, entrySize))
                    {
                        AtlasIndexEntry indexEntry = new AtlasIndexEntry();
                        indexEntry.offset = (int)m_atlasAllocators[atlasBufferIndex].Allocate((UInt64)entrySize);
                        indexEntry.size = entrySize;
                        indexEntry.atlasBufferIndex = atlasBufferIndex;
                        indexEntry.lastUsedFrame = Time.frameCount;

                        copier.EnqueueBufferCopy(m_uploadBuffer, m_atlasBuffers[atlasBufferIndex], uploadBufferOffset, indexEntry.offset, indexEntry.size);
                        m_stats.AddUpload((int)indexEntry.size);
                        m_resources.Add(hash, indexEntry);
                    }
                    else
                    {
                        Debug.LogWarning($"[GPUResourceTracker] will not be able to fit new upload data of {entrySize} bytes within atlas buffer. Upload aborted.");
                    }

                    uploadBufferOffset += attributeBuffer.GetArray().Length;
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

                AttributeBuffer buffer = mergeEntry.attributeBuffer;
                Hash128 hash = buffer.GetHash();

                if (m_resources.TryGetValue(hash, out var indexEntry))
                {
                    indexEntry.lastUsedFrame = Time.frameCount;
                    Debug.Assert(indexEntry.size > 0);
                    Debug.Assert(indexEntry.atlasBufferIndex >= 0);
                    copier.EnqueueBufferCopy(m_atlasBuffers[indexEntry.atlasBufferIndex], mergeEntry.renderBuffer,
                        indexEntry.offset, destOffset, indexEntry.size);
                    destOffset += (int)indexEntry.size;
                }
                else
                {
                    Debug.Assert(false, $"[GPUResourceTracker] Attribute data not found in atlas. hash: {hash.GetHashCode()} semantic: {mergeEntry.semantic}");
                }
            }

            m_mergeCopy.Clear();
            

        }
    }
}