// Copyright © 2025 Miris, Inc. All rights reserved.

// Standard library
using System;
// Unity packages
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
// Unity Engine
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;

namespace Miris.Runtime
{
    public class DataFormatUtils
    {
        static public unsafe NativeArray<byte> WrapVoidPtrWithNativeArray(IntPtr bufferPtr, int totalBytes)
        {
            NativeArray<byte> nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(
                bufferPtr.ToPointer(),
                totalBytes,
                Allocator.None
            );

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(
                ref nativeArray,
                AtomicSafetyHandle.GetTempUnsafePtrSliceHandle()
            );
#endif

            return nativeArray;
        }
    }


    // Pairing of a blind data buffer with its semantic & encoding.  
    // Provides convenience accessors for loading into the renderer.
    // The semantic and encoding will inform how the data should be decoded (e.g. in the GPU shader).
    public class AttributeBuffer
    {
        private AttributeSemantic m_semantic;
        private AttributeEncoding m_encoding;
        public const int s_invalidPropertyValue = -1;
        public const int s_invalidTextureWidth = s_invalidPropertyValue;
        public const int s_invalidTextureHeight = s_invalidPropertyValue;
        private int m_textureWidth;
        private int m_textureHeight;
        private int m_blockDim;
        private int m_elementCount = s_invalidPropertyValue;
        private int m_splatCount = s_invalidPropertyValue;
        private int m_totalBytes = s_invalidPropertyValue;
        private Hash128 m_hash;
        private Texture2D m_texture;
        private CompressionType m_compressionType;
        
        // Per-component min/max values for ASTC texture normalization
        private readonly Vector4 m_minVec = Vector4.zero;
        private readonly Vector4 m_maxVec = Vector4.one;
        private bool m_isRangeNormalized = false;
        
        // Block bounds buffer for ASTC textures (optional)
        private AttributeBuffer m_blockBoundsBuffer;
        private bool m_hasBlockBoundsBuffer = false;
        private bool m_isBlockScanlineOrder = true;

        // Underlying data storage (bytes)
        protected NativeArray<byte> m_nativeArray;
        protected Texture2D[] m_externalTextures;
        protected MosaicDescriptorInfo[] m_mosaicDescriptorInfos;

        private GpuArray m_blockBoundsGpuBuffer;

        // implement compression type to GraphicsFormat mapping if needed
        GraphicsFormat ConvertToGraphicsFormat(CompressionType compressionType)
        {
            return compressionType switch
            {
                CompressionType.TEX_ASTC_4x4_UNORM_BLOCK => GraphicsFormat.RGBA_ASTC4X4_UNorm,
                CompressionType.TEX_ASTC_5x5_UNORM_BLOCK => GraphicsFormat.RGBA_ASTC5X5_UNorm,
                CompressionType.TEX_ASTC_6x6_UNORM_BLOCK => GraphicsFormat.RGBA_ASTC6X6_UNorm,
                CompressionType.TEX_ASTC_8x8_UNORM_BLOCK => GraphicsFormat.RGBA_ASTC8X8_UNorm,
                CompressionType.TEX_ASTC_10x10_UNORM_BLOCK => GraphicsFormat.RGBA_ASTC10X10_UNorm,
                CompressionType.TEX_ASTC_12x12_UNORM_BLOCK => GraphicsFormat.RGBA_ASTC12X12_UNorm,
                CompressionType.TEX_ASTC_4x4_SFLOAT_BLOCK => GraphicsFormat.RGBA_ASTC4X4_UFloat,
                CompressionType.TEX_ASTC_5x5_SFLOAT_BLOCK => GraphicsFormat.RGBA_ASTC5X5_UFloat,
                CompressionType.TEX_ASTC_6x6_SFLOAT_BLOCK => GraphicsFormat.RGBA_ASTC6X6_UFloat,
                CompressionType.TEX_ASTC_8x8_SFLOAT_BLOCK => GraphicsFormat.RGBA_ASTC8X8_UFloat,
                CompressionType.TEX_ASTC_10x10_SFLOAT_BLOCK => GraphicsFormat.RGBA_ASTC10X10_UFloat,
                CompressionType.TEX_ASTC_12x12_SFLOAT_BLOCK => GraphicsFormat.RGBA_ASTC12X12_UFloat,
                _ => GraphicsFormat.None
            };
        }

        public bool IsBlockCompressed()
        {
            return m_compressionType >= CompressionType.TEX_ASTC_BEGIN && m_compressionType <= CompressionType.TEX_ASTC_END;
        }

        CompressionType GetCompressionType()
        {
            return m_compressionType;
        }

        // --------------------------------------------------------------------
        // Constructors
        // --------------------------------------------------------------------

        // Wrap a AttributeBuffer around pre-allocated memory, using a specified format.
        // If it is a texture, the textureWidth arg must be supplied.
        unsafe public AttributeBuffer(AttributeSemantic semantic, 
            AttributeEncoding encoding, 
            NativeArray<byte> nativeArray,
            Hash128 hash,
            CompressionType compressionType,
            int textureWidth = s_invalidTextureWidth, 
            int textureHeight = s_invalidTextureHeight,
            int blockDim = s_invalidPropertyValue,
            int elementCount = s_invalidPropertyValue,
            int totalBytes = s_invalidPropertyValue,
            int splatCount = s_invalidPropertyValue,
            Vector4 minVec = default,
            Vector4 maxVec = default,
            bool isRangeNormalized = false)
        {
            m_hash = hash;
            m_blockDim = blockDim;
            m_elementCount = elementCount;
            m_totalBytes = totalBytes;
            m_minVec = minVec;
            m_maxVec = (maxVec == default) ? Vector4.one : maxVec;
            m_isRangeNormalized = isRangeNormalized;
            ApplySemanticAndEncoding(semantic, encoding);
            m_nativeArray = nativeArray;
            m_splatCount = splatCount;
            m_compressionType = compressionType;

            if (IsBlockCompressed())
            {
                Assert.AreNotEqual(textureWidth, s_invalidTextureWidth);
                m_textureWidth = textureWidth;
                m_textureHeight = textureHeight;
                
                GraphicsFormat graphicsFormat = ConvertToGraphicsFormat(compressionType);
                m_texture = new Texture2D(m_textureWidth, m_textureHeight, graphicsFormat, 0, TextureCreationFlags.DontInitializePixels);
                if(m_texture == null)
                {
                    throw new Exception("Failed to create texture with format " + graphicsFormat);
                }
                else
                {
                    m_texture.LoadRawTextureData(m_nativeArray);
                    m_texture.Apply(false, true);
                }
            }
        }

        unsafe public AttributeBuffer(AttributeSemantic semantic,
            AttributeEncoding encoding,
            MosaicDescriptorInfo[] mosaicDescriptorInfos,
            Hash128 hash,
            CompressionType compressionType,
            int textureWidth = s_invalidTextureWidth,
            int textureHeight = s_invalidTextureHeight,
            int blockDim = s_invalidPropertyValue,
            int elementCount = s_invalidPropertyValue,
            int totalBytes = s_invalidPropertyValue, 
            int splatCount = s_invalidPropertyValue,
            Vector4 minVec = default,
            Vector4 maxVec = default,
            bool isRangeNormalized = false)
        {
            m_hash = hash;
            m_blockDim = blockDim;
            m_mosaicDescriptorInfos = mosaicDescriptorInfos;
            m_totalBytes = totalBytes;
            m_splatCount = splatCount;
            m_compressionType = compressionType;
            m_minVec = minVec;
            m_maxVec = (maxVec == default) ? Vector4.one : maxVec;
            m_isRangeNormalized = isRangeNormalized;
            ApplySemanticAndEncoding(semantic, encoding);
            if (m_mosaicDescriptorInfos.Length > 0) {
                m_externalTextures = new Texture2D[m_mosaicDescriptorInfos.Length];
                for(int i = 0; i < m_mosaicDescriptorInfos.Length; i++)
                {
                    MosaicDescriptorInfo mosaicDescriptorInfo = m_mosaicDescriptorInfos[i];
                    Assert.IsTrue(mosaicDescriptorInfo.m_mosaicTileWidth > 0 && mosaicDescriptorInfo.m_mosaicTileHeight > 0, "MosaicDescriptorInfo must have valid tile dimensions.");
                    Assert.IsTrue(mosaicDescriptorInfo.m_mosaicTileX >= 0 && mosaicDescriptorInfo.m_mosaicTileY >= 0, "MosaicDescriptorInfo must have valid tile coordinates.");
                    Assert.IsTrue(mosaicDescriptorInfo.m_offset >= 0 && mosaicDescriptorInfo.m_stride > 0, "MosaicDescriptorInfo must have valid offset and stride.");
                    Assert.IsTrue(mosaicDescriptorInfo.m_min <= mosaicDescriptorInfo.m_max, "MosaicDescriptorInfo min must be less than or equal to max.");
                    Assert.IsTrue(mosaicDescriptorInfo.m_externalNativeHandle != null, "MosaicDescriptorInfo must have a valid external native handle.");
                    Assert.IsTrue(mosaicDescriptorInfo.m_interleaveType >= 0, "MosaicDescriptorInfo must have a valid interleave type.");
             

                    if(mosaicDescriptorInfo.m_isRangeNormalized == 0)
                    {
                        m_externalTextures[i] = Texture2D.CreateExternalTexture(
                            mosaicDescriptorInfo.m_textureHeight,
                            mosaicDescriptorInfo.m_textureWidth,
                            TextureFormat.RFloat,
                            false,
                            true,
                            (IntPtr)mosaicDescriptorInfo.m_externalNativeHandle);
                    }
                    else { 
                        m_externalTextures[i] = Texture2D.CreateExternalTexture(
                            mosaicDescriptorInfo.m_textureHeight,
                            mosaicDescriptorInfo.m_textureWidth,
                            TextureFormat.R8,
                            false,
                            true,
                            (IntPtr)mosaicDescriptorInfo.m_externalNativeHandle);
                    }
                }

                m_elementCount = elementCount;
            }
            if (IsTexture() && !IsGPUBuffer())
            {
                Assert.AreNotEqual(textureWidth, s_invalidTextureWidth);
                m_textureWidth = textureWidth;
            }
        }

        // --------------------------------------------------------------------
        // Accessors
        // --------------------------------------------------------------------

        public AttributeSemantic GetSemantic()
        {
            return m_semantic;
        }

        public int GetComponentCount()
        {
            return m_semantic switch
            {
                AttributeSemantic.Scale => 3,
                AttributeSemantic.SHCoefficients => throw new NotImplementedException(),
                AttributeSemantic.Color => 4,
                AttributeSemantic.Position => 3,
                AttributeSemantic.Orientation => 4,
                AttributeSemantic.BlockBounds => throw new NotImplementedException(),
                _ => throw new NotImplementedException()
            };
        }

        public MosaicDescriptorInfo[] GetMosaicDescriptorInfos()
        {
            return m_mosaicDescriptorInfos;
        }

        public Hash128 GetHash()
        {
            return m_hash;
        }

        public AttributeEncoding GetEncoding()
        {
            return m_encoding;
        }

        public int GetElementCount()
        {
            return m_elementCount;
        }
        public int GetSplatCount()
        {
            return m_splatCount;
        }

        public int GetTotalBytes()
        {
            return m_totalBytes;
        }

        // See GaussianSplatRenderer and GaussianSplatDecoder.hlsl for how this shader keyword is consumed.
        public string GetShaderKeyword()
        {
            return GenerateShaderKeyword(m_semantic, m_encoding);
        }

        public bool IsTexture()
        {
            return m_encoding.IsTextureEncoding();
        }
        public Texture2D GetTexture()
        {
            return m_texture;
        }

        public bool IsGPUBuffer()
        {
            if (m_externalTextures != null)
            {
                return m_externalTextures.Length > 0;
            }
            return false;
        }
        public Texture2D[] GetExternalTextures()
        {
            return m_externalTextures;
        }

        public (int, int) GetTextureSize()
        {
            Assert.IsTrue(IsTexture());
            return CalculateTextureSize(m_textureHeight, m_textureWidth);
        }
        
        public int GetBlockDim()
        {
            return m_blockDim;
        }

        // Get per-component min/max ranges for ASTC textures
        public (Vector4 min, Vector4 max) GetMinMaxVectors()
        {
            return (m_minVec, m_maxVec);
        }

        public bool GetIsRangeNormalized()
        {
            return m_isRangeNormalized;
        }

        // Set the block bounds buffer for this attribute (used for ASTC compression)
        public void SetBlockBoundsBuffer(AttributeBuffer blockBoundsBuffer)
        {
            m_blockBoundsBuffer = blockBoundsBuffer;
        }
        public void SetHasBlockBoundsBuffer(bool hasBlockBoundsBuffer)
        {
            m_hasBlockBoundsBuffer = hasBlockBoundsBuffer;
        }
        public bool HasBlockBoundsBuffer()
        {
            return m_hasBlockBoundsBuffer;
        }

        public void SetBlockScanlineOrder(bool isScanlineOrder)
        {
            m_isBlockScanlineOrder = isScanlineOrder;
        }
        public bool IsBlockScanlineOrder()
        {
            return m_isBlockScanlineOrder;
        }

        // Get the block bounds buffer (returns null if not available)
        public AttributeBuffer GetBlockBoundsBuffer()
        {
            return m_blockBoundsBuffer;
        }

        // Get block bounds as GPU buffer for shader use (returns null if not available)
        public IGpuBuffer GetBlockBoundsGpuBuffer()
        {
            if (m_blockBoundsBuffer == null)
                return null;
            
            if (m_blockBoundsGpuBuffer != null)
                return m_blockBoundsGpuBuffer;
            // Create a GpuArray for the block bounds data
            int totalBytes = m_blockBoundsBuffer.GetTotalBytes();
            int blockBoundsBufferId = Shader.PropertyToID("_blockBoundsBuffer");
            var gpuBuffer = new GpuArray(totalBytes, blockBoundsBufferId, 0, "BlockBounds");
            gpuBuffer.SetData(m_blockBoundsBuffer.GetArray());
            m_blockBoundsGpuBuffer = gpuBuffer;
            return gpuBuffer;
        }

        // --------------------------------------------------------------------
        // Array access
        // --------------------------------------------------------------------

        public NativeArray<byte> GetArray()
        {
            return m_nativeArray;
        }

        // Interpret the underlying bytes as an array of type U.
        // This is like a re-interpret cast.  So use with care!
        public NativeArray<U> ReinterpretArray<U>() where U : struct
        {
            // Our native array is of type "byte", so its size is 1.
            return m_nativeArray.Reinterpret<U>(expectedTypeSize: 1);
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        static public (int, int) CalculateTextureSize(int elementCount, int textureWidth)
        {
            int textureHeight = (elementCount + textureWidth - 1) / textureWidth;
            return (textureWidth, textureHeight);
        }

        private void ApplySemanticAndEncoding(AttributeSemantic semantic, AttributeEncoding encoding)
        {
            semantic.ValidateEncodingSupport(encoding);
            m_semantic = semantic;
            m_encoding = encoding;
        }

        static private string GenerateShaderKeyword(AttributeSemantic semantic, AttributeEncoding encoding)
        {
            return semantic.ToString() + "_" + encoding.ToString();
        }
    }
}
