// Copyright © 2026 Miris, Inc. All rights reserved.

using UnityEngine;

namespace Miris.Runtime
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

        public void SetCacheSize(long size)
        {
            m_totalCacheSize = size;
        }

        public void SetUsedSize(long size)
        {
            m_usedCacheSize = size;
        }

        public void AddUpload(int bytes)
        {
            m_bytesUploaded += bytes;
            m_totalBytesUploaded += bytes;

            m_totalNumUploads++;
            m_numUploads++;
        }

        public void AddEviction()
        {
            m_totalEvictions++;
        }

        public void AddCacheHit()
        {
            m_cacheHits++;
            m_totalCacheHits++;
        }

        public void Log()
        {
            MirisApi.PlotMetric("Tracker Uploads", m_totalNumUploads);
            MirisApi.PlotMetric("Tracker bytes uploaded", m_bytesUploaded);
            // Climbing in step with uploads means atlas thrash: entries are being re-uploaded
            // as fast as they are reclaimed.
            MirisApi.PlotMetric("Tracker evictions", m_totalEvictions);

            if (m_numUploads > 0)
            {
                MirisDebug.Log(
                    $"[GPUResourceTracker] num uploads: {m_numUploads}, bytes: {m_bytesUploaded}, total num uploads: {m_totalNumUploads}, total bytes uploaded: {m_totalBytesUploaded}");
            }

            if (m_totalCacheSize == 0 && m_totalNumUploads == 0)
            {
                return;
            }

            Debug.Assert(m_totalCacheSize > 0, $"[GPUResourceTracker] total cache size appears to not have been set.");

            // Live bytes against capacity. Not m_totalBytesUploaded, which is lifetime traffic:
            // it is never reset, so it exceeds capacity as soon as a tracker has re-uploaded
            // enough, and reclamation makes traffic and residency diverge permanently.
            float minCacheSize = Mathf.Max(0.001f, (float)m_totalCacheSize);
            float cacheOccupancyPercentage = 100.0f * (float)m_usedCacheSize / minCacheSize;
            float cacheHitPercentage = 100.0f * (float)m_totalCacheHits / (float)(m_totalCacheHits + m_totalNumUploads);
            // MirisDebug.Log($"[GPUResourceTracker] cache hits: {m_cacheHits}, misses:{m_numUploads}, total cache hits: {m_totalCacheHits}, efficiency: {cacheHitPercentage}%, occupancy: {cacheOccupancyPercentage}%");
            // issue cache capacity warning approx every second (framerate dependent)
            if (cacheOccupancyPercentage > 98.0f && Time.frameCount % 100 == 0)
            {
                Debug.LogWarning($"[GPUResourceTracker] atlas cache is nearly full! "
                                 + $"{cacheOccupancyPercentage:F1}% of {m_totalCacheSize} bytes, "
                                 + $"{m_totalEvictions} reclaims so far");
            }
        }

        public int m_numUploads;
        public int m_bytesUploaded;

        public int m_totalNumUploads;
        public int m_totalBytesUploaded;

        public int m_cacheHits;
        public int m_totalCacheHits;

        public long m_usedCacheSize;

        public int m_totalEvictions;

        public long m_totalCacheSize;
    }
}