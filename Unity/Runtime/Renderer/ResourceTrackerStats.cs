// Copyright © 2024 Miris. All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;

namespace Aqua.Runtime
{
    // Track information on attributes uploaded to the Atlas buffer (via the GPUResourceTracker)
    struct ResourceStats
    {
        public void ResourceStat()
        {
            m_totalNumUploads = 0;
            m_totalBytesUploaded = 0;
            m_cacheHits = 0;
            m_totalCacheHits = 0;
            m_totalCacheSize = 0;
            Reset();
        }

        public void Reset()
        {
            m_cacheHits = 0;
            m_bytesUploaded = 0;
            m_numUploads = 0;
        }

        public void SetCacheSize(int size)
        {
            m_totalCacheSize = size;
        }

        public void AddUpload(int bytes)
        {
            m_bytesUploaded += bytes;
            m_totalBytesUploaded += bytes;

            m_totalNumUploads++;
            m_numUploads++;
        }

        public void AddCacheHit()
        {
            m_cacheHits++;
            m_totalCacheHits++;
        }

        public void Log()
        {
            AquaUnityApi.PlotMetric("Tracker Uploads", m_totalNumUploads);
            AquaUnityApi.PlotMetric("Tracker bytes uploaded", m_bytesUploaded);

            if (m_numUploads > 0)
            {
                Debug.Log(
                    $"[GPUResourceTracker] num uploads: {m_numUploads}, bytes: {m_bytesUploaded}, total num uploads: {m_totalNumUploads}, total bytes uploaded: {m_totalBytesUploaded}");
            }

            Debug.Assert(m_totalCacheSize > 0, $"[GPUResourceTracker] total cache size appears to not have been set.");

            float minCacheSize = Mathf.Max(0.001f, (float)m_totalCacheSize);
            float cacheOccupancyPercentage = 100.0f * (float)m_totalBytesUploaded / minCacheSize;
            float cacheHitPercentage = 100.0f * (float)m_totalCacheHits / (float)(m_totalCacheHits + m_totalNumUploads);
            Debug.Log($"[GPUResourceTracker] cache hits: {m_cacheHits}, misses:{m_numUploads}, total cache hits: {m_totalCacheHits}, efficiency: {cacheHitPercentage}%, occupancy: {cacheOccupancyPercentage}%");
            // issue cache capacity warning approx every second (framerate dependent)
            if (cacheOccupancyPercentage > 98.0f && Time.frameCount % 100 == 0)
            {
                Debug.LogWarning($"[GPUResourceTracker] atlas cache is nearly full! {cacheOccupancyPercentage}%");
            }
        }

        public int m_numUploads;
        public int m_bytesUploaded;

        public int m_totalNumUploads;
        public int m_totalBytesUploaded;

        public int m_cacheHits;
        public int m_totalCacheHits;

        public int m_totalCacheSize;
    }
}