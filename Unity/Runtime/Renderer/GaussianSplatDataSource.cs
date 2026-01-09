// Copyright © 2025 Miris, Inc. All rights reserved.

// Standard library
using System;
using System.Collections.Generic;

// Unity packages
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

namespace Miris.Runtime
{
    /// <summary>
    /// GaussianSplatDataSource provides data to a GaussianSplatRenderer.
    /// </summary>
    public class GaussianSplatDataSource
    {
        public SceneObject m_object = null;
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
        public float4 m_objectIdColor = new float4(1.0f, 0.0f, 0.0f, 1.0f);

        public int GetSplatCount() {
            var positionBuffer = GetBuffer(AttributeSemantic.Position);
            int splatCount = positionBuffer.GetSplatCount();
            if (splatCount > 0) {
                return splatCount;
            }
            return positionBuffer.GetElementCount();
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
                return cachedAttributeBuffer;
            }

            // Nope, create it
            string bufferName = GetBufferName(semantic);

            AttributeInfo attributeInfo = m_object.GetAttribute(bufferName);
            int totalBytes = attributeInfo.m_dataSizeBytes;
            IntPtr bufferPtr = new IntPtr(attributeInfo.m_dataPtr);

            NativeArray<byte> nativeArray = DataFormatUtils.WrapVoidPtrWithNativeArray(bufferPtr, totalBytes);
            AttributeEncoding encoding = GetEncoding(attributeInfo.m_compressionType, semantic.GetDefaultEncoding());
            CompressionType compressionType = attributeInfo.m_compressionType;
            int width = AttributeBuffer.s_invalidTextureWidth;
            int height = AttributeBuffer.s_invalidTextureWidth;
            
            int blockDim = attributeInfo.m_blockDim;
            Hash128 hash = new Hash128(attributeInfo.m_hash0, attributeInfo.m_hash1, attributeInfo.m_hash2,
                attributeInfo.m_hash3);
            if (AttributeEncodingExtensions.IsTextureEncoding(encoding) || (compressionType >= CompressionType.TEX_ASTC_BEGIN && compressionType <= CompressionType.TEX_ASTC_END))
            {
                width = attributeInfo.m_textureWidth;
                height = attributeInfo.m_textureHeight;
            }

            // Extract min/max vectors once for both code paths
            Vector4 minVec = new Vector4(attributeInfo.m_minValue.x, attributeInfo.m_minValue.y, attributeInfo.m_minValue.z, attributeInfo.m_minValue.w);
            Vector4 maxVec = new Vector4(attributeInfo.m_maxValue.x, attributeInfo.m_maxValue.y, attributeInfo.m_maxValue.z, attributeInfo.m_maxValue.w);
            bool isRangeNormalized = attributeInfo.m_isRangeNormalized != 0;

            if (attributeInfo.m_mosaicDescriptorCount > 0)
            {
                int descriptorCount = attributeInfo.m_mosaicDescriptorCount;

                MosaicDescriptorInfo[] mosaicDescriptors = new MosaicDescriptorInfo[descriptorCount];
                MirisApi.GetMosaicDescriptors((long)attributeInfo.m_mosaicDescriptors, mosaicDescriptors);
                
                AttributeBuffer ab = new AttributeBuffer(semantic, encoding, mosaicDescriptors, hash, compressionType, width, height, blockDim, attributeInfo.m_elementCount, totalBytes, attributeInfo.m_splatCount, minVec, maxVec, isRangeNormalized);
                
                // For ASTC compressed attributes, try to find and associate the corresponding block bounds buffer
                TryAssociateBlockBoundsBuffer(ab, bufferName, attributeInfo);
                
                m_semanticToBuffer[semantic] = ab;
                return ab;
            }

            AttributeBuffer attributeBuffer = new AttributeBuffer(semantic, encoding,  nativeArray, hash, attributeInfo.m_compressionType, width, height, blockDim, attributeInfo.m_elementCount, totalBytes, attributeInfo.m_splatCount, minVec, maxVec, isRangeNormalized);
            
            // For ASTC compressed attributes, try to find and associate the corresponding block bounds buffer
            TryAssociateBlockBoundsBuffer(attributeBuffer, bufferName, attributeInfo);
            
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
                MirisDebug.Log(
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
                case AttributeSemantic.SparkPackedSplats:
                    {
                        return "sparkPackedSplats";
                    }
                default:
                    {
                        throw new ArgumentOutOfRangeException($"Unhandled AttributeSemantic {semantic.ToString()}");
                    }
            }
        }

        private static AttributeEncoding GetEncoding(CompressionType compressionType, AttributeEncoding defaultEncoding)
        {
            if (compressionType == CompressionType.TEX_R16G16B16_SFLOAT)
            {
                return AttributeEncoding.Float16x3;
            }
            if (compressionType == CompressionType.TEX_R16G16B16A16_SFLOAT)
            {
                return AttributeEncoding.Float16x4;
            }
            if (compressionType == CompressionType.TEX_R16G16B16_UNORM)
            {
                return AttributeEncoding.UInt16x3;
            }

            return defaultEncoding;
        }

        public float4 GetObjectIdColor()
        {
            return m_objectIdColor;
        }

        /// <summary>
        /// Attempts to associate block bounds buffer with the given AttributeBuffer for block compressed attributes.
        /// </summary>
        /// <param name="attributeBuffer">The attribute buffer to associate block bounds with</param>
        /// <param name="bufferName">The name of the main buffer</param>
        /// <param name="attributeInfo">The attribute info containing block scanline order</param>
        unsafe private void TryAssociateBlockBoundsBuffer(AttributeBuffer attributeBuffer, string bufferName, AttributeInfo attributeInfo)
        {
            if (!attributeBuffer.IsBlockCompressed())
            {
                return;
            }

            string blockBoundsName = "blockBounds" + bufferName;
            try
            {
                AttributeInfo blockBoundsInfo = m_object.GetAttribute(blockBoundsName);
                if (blockBoundsInfo.m_dataPtr != null) // Valid data pointer
                {
                    IntPtr blockBoundsPtr = new IntPtr(blockBoundsInfo.m_dataPtr);
                    int blockBoundsTotalBytes = blockBoundsInfo.m_bytesPerElement * blockBoundsInfo.m_elementCount;
                    NativeArray<byte> blockBoundsArray = DataFormatUtils.WrapVoidPtrWithNativeArray(blockBoundsPtr, blockBoundsTotalBytes);
                    Hash128 blockBoundsHash = new Hash128(blockBoundsInfo.m_hash0, blockBoundsInfo.m_hash1, blockBoundsInfo.m_hash2, blockBoundsInfo.m_hash3);
                    
                    AttributeBuffer blockBoundsBuffer = new AttributeBuffer(
                        AttributeSemantic.BlockBounds, 
                        AttributeEncoding.Float32, 
                        blockBoundsArray, 
                        blockBoundsHash, 
                        CompressionType.NONE, 
                        AttributeBuffer.s_invalidTextureWidth, 
                        AttributeBuffer.s_invalidTextureHeight, 
                        -1, 
                        blockBoundsInfo.m_elementCount, 
                        blockBoundsTotalBytes, 
                        blockBoundsInfo.m_splatCount
                    );
                    
                    attributeBuffer.SetBlockBoundsBuffer(blockBoundsBuffer);
                    attributeBuffer.SetHasBlockBoundsBuffer(blockBoundsInfo.m_dataSizeBytes != 4);
                    attributeBuffer.SetBlockScanlineOrder(attributeInfo.m_blockScanlineOrder != 0);
                }
            }
            catch (Exception ex)
            {
                // Block bounds not available for this attribute, which is expected for some attributes.
                // If this occurs unexpectedly, log the exception for debugging purposes.
                MirisDebug.Log($"[GaussianSplatDataSource] Block bounds not available for attribute '{blockBoundsName}': {ex.Message}");
            }
        }
    }
}
