// Copyright © 2024 Miris. All rights reserved.

// Standard library
using System;
using System.Collections.Generic;

// Unity packages
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

namespace Aqua.Runtime
{
    /// <summary>
    /// GaussianSplatDataSource provides data to a GaussianSplatRenderer.
    /// </summary>
    public class GaussianSplatDataSource
    {
        public AquaSceneObject m_object = null;
        private Bounds m_bounds = new();

        // Warning: we are caching AttributeBuffer(s) as to not allocate new objects on every query. 
        // This is assuming the nature of the underlying buffer(s) remain static which holds
        // true today, but is subject to change.  
        private Dictionary<AttributeSemantic, AttributeBuffer> m_semanticToBuffer = new();

        // Per data source opacity multiplier
        public float m_opacity = 1.0f;

        public bool m_active = false;
        public bool m_dirty = true;

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
        public float4 m_objectIdColor;

        public int GetSplatCount()
        {
            return GetBuffer(AttributeSemantic.Position).GetElementCount();
        }

        public bool HasBuffer(AttributeSemantic semantic)
        {
            string bufferName = GetBufferName(semantic);
            return m_object.HasAttribute(bufferName);
        }

        unsafe public AttributeBuffer GetBuffer(AttributeSemantic semantic)
        {
            // Is it cached?
            if (!m_dirty && m_semanticToBuffer.TryGetValue(semantic, out AttributeBuffer cachedAttributeBuffer))
            {
                AquaHash attrHash = cachedAttributeBuffer.GetAquaHash();
                Tuple<uint, uint, uint, uint> hashTuple = attrHash.ToTuple();
                AquaUnityApi.MarkAttributeArrayAccessed(hashTuple.Item1, hashTuple.Item2, hashTuple.Item3, hashTuple.Item4);
                return cachedAttributeBuffer;
            }

            // Nope, create it
            string bufferName = GetBufferName(semantic);

            AttributeInfo attributeInfo = m_object.GetAttribute(bufferName);
            int totalBytes = attributeInfo.m_bytesPerElement * attributeInfo.m_elementCount;
            IntPtr bufferPtr = new IntPtr(attributeInfo.m_dataPtr);

            NativeArray<byte> nativeArray = DataFormatUtils.WrapVoidPtrWithNativeArray(bufferPtr, totalBytes);
            AttributeEncoding encoding = GetEncoding(attributeInfo.m_compressionType, semantic.GetDefaultEncoding());
            int width = AttributeBuffer.s_invalidTextureWidth;
            int blockDim = attributeInfo.m_blockDim;
            AquaHash hash = new AquaHash(attributeInfo.m_hash0, attributeInfo.m_hash1, attributeInfo.m_hash2,
                attributeInfo.m_hash3);
            if (AttributeEncodingExtensions.IsTextureEncoding(encoding))
            {
                width = attributeInfo.m_textureWidth;
            }

            if (attributeInfo.m_mosaicDescriptorCount > 0)
            {
                int descriptorCount = attributeInfo.m_mosaicDescriptorCount;

                MosaicDescriptorInfo[] mosaicDescriptors = new MosaicDescriptorInfo[descriptorCount];
                AquaUnityApi.GetMosaicDescriptors((long)attributeInfo.m_mosaicDescriptors, mosaicDescriptors);

                AttributeBuffer ab = new AttributeBuffer(semantic, encoding, mosaicDescriptors, hash, width, blockDim, attributeInfo.m_elementCount);
                m_semanticToBuffer[semantic] = ab;
                return ab;
            }

            AttributeBuffer attributeBuffer = new AttributeBuffer(semantic, encoding,  nativeArray, hash, width, blockDim);
            m_semanticToBuffer[semantic] = attributeBuffer;
            return attributeBuffer;
        }

        public Bounds GetObjectBounds()
        {
            if (m_dirty)
            {
                m_bounds = m_object.GetBoundingBox();
            }
            return m_bounds;
        }

        public bool IsValid()
        {
            // TODO: Need more robust way of checking whether or not a data source is valid.
            return m_object != null && m_object.GetAttributeCount() > 0;
        }

        public int GetLodIndex()
        {
            return m_object.GetLodIndex();
        }

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

        static private string GetBufferName(AttributeSemantic semantic)
        {
            switch (semantic)
            {
                 case AttributeSemantic.Position:
                    {
                        return "position";
                    }
                case AttributeSemantic.Scale:
                    {
                        return "scale";
                    }
                case AttributeSemantic.Orientation:
                    {
                        return "rotation";
                    }
                case AttributeSemantic.Color:
                    {
                        return "color";
                    }
                case AttributeSemantic.BlockBounds:
                    {
                        return "blockBounds";
                    }
                case AttributeSemantic.SHCoefficients:
                    {
                        return "sphericalharmonics";
                    }
                default:
                    {
                        throw new ArgumentOutOfRangeException($"Unhandled AttributeSemantic {semantic.ToString()}");
                    }
            }
        }

        private static AttributeEncoding GetEncoding(CompressionType aquaEncoding, AttributeEncoding defaultEncoding)
        {
            if (aquaEncoding == CompressionType.TEX_R16G16B16_SFLOAT)
            {
                return AttributeEncoding.Float16x3;
            }
            if (aquaEncoding == CompressionType.TEX_R16G16B16A16_SFLOAT)
            {
                return AttributeEncoding.Float16x4;
            }
            if (aquaEncoding == CompressionType.TEX_R16G16B16_UNORM)
            {
                return AttributeEncoding.UInt16x3;
            }
            
            if (aquaEncoding == CompressionType.TEX_ASTC_4x4_UNORM_BLOCK)
            {
                return AttributeEncoding.RGBA_Compressed_ASTC_4x4_LDR;
            }

            return defaultEncoding;
        }

        public float4 GetObjectIdColor()
        {
            return m_objectIdColor;
        }
    }
}
