// Copyright © 2024 Miris. All rights reserved.

// Standard library
using System;

// Unity engine
using UnityEngine;

// Unity packages
using Unity.Collections;
using System.Collections.Generic;

namespace Aqua.Runtime
{

    /// <summary>
    /// GaussianSplatAquaDataSource extracts data from an C++ Aqua Scene Object to
    /// feed into the renderer.
    /// </summary>
    public class GaussianSplatAquaDataSource : GaussianSplatDataSource
    {
        public AquaSceneObject m_object = null;
        // Warning: we are caching AttributeBuffer(s) as to not allocate new objects on every query. 
        // This is assuming the nature of the underlying buffer(s) remain static which holds
        // true today, but is subject to change.  
        private Dictionary<AttributeSemantic, AttributeBuffer> m_semanticToBuffer = new();

        private Bounds m_bounds = new();

        // --------------------------------------------------------------------
        // GaussianSplatDataSource overrides
        // --------------------------------------------------------------------

        public override int GetSplatCount()
        {
            return GetBuffer(AttributeSemantic.Position).GetElementCount();
        }

        public override bool HasBuffer(AttributeSemantic semantic)
        {
            string bufferName = GetBufferName(semantic);
            return m_object.HasAttribute(bufferName);
        }

        unsafe public override AttributeBuffer GetBuffer(AttributeSemantic semantic)
        {
            // Is it cached?
            if (!dirty && m_semanticToBuffer.TryGetValue(semantic, out AttributeBuffer cachedAttributeBuffer))
            {
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

        public override Bounds GetObjectBounds()
        {
            if (dirty)
            {
                m_bounds = m_object.GetBoundingBox();
            }
            return m_bounds;
        }

        public override bool IsValid()
        {
            // TODO: Need more robust way of checking whether or not a data source is valid.
            return m_object != null && m_object.GetAttributeCount() > 0;
        }

        public override int GetLodIndex()
        {
            return m_object.GetLodIndex();
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

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
    }
}
