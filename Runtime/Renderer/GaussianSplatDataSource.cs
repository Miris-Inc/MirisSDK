// Copyright © 2024 Miris. All rights reserved.

// Unity engine
using UnityEngine;
using Unity.Mathematics;

// Unity packages
using System.Collections.Generic;

namespace Aqua.Runtime
{
    /// <summary>
    /// GaussianSplatDataSource is an abstract component that provides
    /// data to a GaussianSplatRenderer.
    /// </summary>
    public abstract class GaussianSplatDataSource : MonoBehaviour
    {
        // Per data source opacity multiplier
        public float m_opacity = 1.0f;

        private bool m_dirty = true;

        public bool dirty
        {
            get => m_dirty;
            set
            {
                m_dirty = value;
            }
        }

        // All the supported semantics for representing 3DGS data.
        static private AttributeSemantic[] m_semantics = {
            AttributeSemantic.Position,
            AttributeSemantic.BlockBounds,
            AttributeSemantic.Scale,
            AttributeSemantic.Orientation,
            AttributeSemantic.Color,
            AttributeSemantic.SHCoefficients
        };

        // Cached object ID color for drawing bounding boxes, and other visual features (maybe picking in the future?)
        private float4 m_objectIdColor;

        // --------------------------------------------------------------------
        // Unity overrides
        // --------------------------------------------------------------------

        private void Start()
        {
            m_objectIdColor = GameObjectUtils.HashGameObjectToColor(gameObject);
        }

        // --------------------------------------------------------------------
        // Abstract methods
        // --------------------------------------------------------------------

        public abstract int GetSplatCount();
        public abstract bool HasBuffer(AttributeSemantic semantic);
        public abstract AttributeBuffer GetBuffer(AttributeSemantic semantic);
        public abstract bool IsValid();
        public abstract Bounds GetObjectBounds();
        public abstract int GetLodIndex();

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        public virtual AttributeSemantic[] GetSupportedSemantics()
        {
            return m_semantics;
        }

        // Get all the buffers from this data source
        public IEnumerable<AttributeBuffer> GetBuffers()
        {
            // Semantics associated with a Gaussian Splat data source.
            foreach (AttributeSemantic semantic in GetSupportedSemantics())
            {
                if (HasBuffer(semantic))
                {
                    yield return GetBuffer(semantic);
                }
            }
        }

        public void DebugPrint()
        {
            foreach (AttributeBuffer AttributeBuffer in GetBuffers())
            {
                Debug.Log(
                    AttributeBuffer.GetSemantic().ToString() +
                    " encoding: " + AttributeBuffer.GetEncoding().ToString() +
                    " element count: " + AttributeBuffer.GetElementCount() +
                    " total bytes: " + AttributeBuffer.GetTotalBytes()
                );
            }
        }

        public float4 GetObjectIdColor()
        {
            return m_objectIdColor;
        }
    }
}
