// Copyright © 2026 Miris, Inc. All rights reserved.

// C# Standard Library
using System.Linq;

// Unity Engine
using UnityEngine;

// Unity packages
using Unity.Profiling;

namespace Miris.Runtime
{
    // Per-ModelRoot state a MirisStream keeps: the asset's stream-relative transform, which Shark
    // reads, and the bounds of its splat data sources.
    internal class MirisAssetRenderComponent
    {
        // Reference to source 3DGS asset data.
        public MirisAssetDataSource[] m_dataSources;

        public Matrix4x4 m_assetMatrix = Matrix4x4.identity;

        // Stream-relative bounds of every data source, re-aggregated only when they change.
        private Bounds m_dataSourceBounds = new();
        private Bounds m_worldBounds = new();

        // The data source array m_dataSourceBounds was last aggregated from.
        private MirisAssetDataSource[] m_prevBoundsDataSources;

        static readonly ProfilerMarker s_updateBoundsMarker = new ProfilerMarker(
            "[MirisAssetRenderComponent] Update bounds"
        );

        public Bounds GetObjectBounds()
        {
            return m_dataSourceBounds;
        }

        public Bounds GetWorldBounds()
        {
            return m_worldBounds;
        }

        // Is our data source in a valid state?
        public bool IsAssetValid()
        {
            if (m_dataSources == null)
            {
                return false;
            }

            if (m_dataSources.Length == 0)
            {
                return false;
            }

            foreach (var dataSource in m_dataSources)
            {
                if (dataSource.IsValid())
                {
                    return true;
                }
            }

            return false;
        }

        public void Update(Transform transform)
        {
            // The component may be asked to update when the asset is in an invalid state. If this
            // happens just skip any further updates
            if (!IsAssetValid())
            {
                return;
            }

            // Re-aggregate when the array is swapped, or when an existing data source's
            // ancestor-relative bounds went stale from a transform change.
            bool dataSourcesChanged = !ReferenceEquals(m_prevBoundsDataSources, m_dataSources);
            if (dataSourcesChanged || m_dataSources.Any(dataSource => dataSource.m_dirty))
            {
                m_prevBoundsDataSources = m_dataSources;
                UpdateDataSourceBounds();
            }

            // Every frame: the stream can move without any data source changing.
            UpdateWorldBounds(transform.localToWorldMatrix);
        }

        // Aggregate bounds over the data sources by taking the min/max of their bounds, which
        // appears to be faster than repeatedly calling Bounds.Encapsulate(Bounds).
        private void UpdateDataSourceBounds()
        {
            using (s_updateBoundsMarker.Auto())
            {
                Vector3 minBound = Vector3.positiveInfinity;
                Vector3 maxBound = Vector3.negativeInfinity;
                foreach (var dataSource in m_dataSources)
                {
                    Bounds dataSourceBounds = dataSource.GetObjectBounds();
                    minBound = Vector3.Min(minBound, dataSourceBounds.min);
                    maxBound = Vector3.Max(maxBound, dataSourceBounds.max);

                    // After GetObjectBounds, which refreshes its cached bounds only while dirty.
                    dataSource.m_dirty = false;
                }
                m_dataSourceBounds.SetMinMax(minBound, maxBound);
            }
        }

        private void UpdateWorldBounds(Matrix4x4 streamToWorld)
        {
            Vector3[] corners = BoundsUtils.BoundsGetCorners(m_dataSourceBounds);
            m_worldBounds = new Bounds(streamToWorld.MultiplyPoint3x4(corners[0]), Vector3.zero);
            for (int cornerIndex = 1; cornerIndex < corners.Length; cornerIndex++)
            {
                m_worldBounds.Encapsulate(streamToWorld.MultiplyPoint3x4(corners[cornerIndex]));
            }
        }
    }
}
