// Copyright © 2024 Miris. All rights reserved.

// Standard library
using System;

// Unity Engine
using UnityEngine.Assertions;

// Unity packages
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Aqua.Runtime
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
        public const int s_invalidTextureWidth = -1;
        private int m_textureWidth;
        private int m_blockDim;
        private int m_elementCount = -1;
        private AquaHash m_hash;

        // Underlying data storage (bytes)
        protected NativeArray<byte> m_nativeArray;
        protected Texture2D[] m_externalTextures;
        protected MosaicDescriptorInfo[] m_mosaicDescriptorInfos;

        // --------------------------------------------------------------------
        // Constructors
        // --------------------------------------------------------------------

        // Allocate new memory with a specified format.
        public AttributeBuffer(AttributeSemantic semantic, AttributeEncoding encoding, int elementCount)
        {
            ApplySemanticAndEncoding(semantic, encoding);

            // Allocate a new array.
            int totalBytes = elementCount * GetBytesPerElement();
            m_nativeArray = new(totalBytes, Allocator.Temp);
        }

        // Wrap a AttributeBuffer around pre-allocated memory, using a specified format.
        // If it is a texture, the textureWidth arg must be supplied.
        unsafe public AttributeBuffer(AttributeSemantic semantic, 
            AttributeEncoding encoding, 
            NativeArray<byte> nativeArray,
            AquaHash hash,
            int textureWidth = s_invalidTextureWidth, 
            int blockDim = -1)
        {
            m_hash = hash;
            m_blockDim = blockDim;
            ApplySemanticAndEncoding(semantic, encoding);
            m_nativeArray = nativeArray;

            if (IsTexture())
            {
                Assert.AreNotEqual(textureWidth, s_invalidTextureWidth);
                m_textureWidth = textureWidth;
            }
        }

        unsafe public AttributeBuffer(AttributeSemantic semantic,
            AttributeEncoding encoding,
            MosaicDescriptorInfo[] mosaicDescriptorInfos,
            AquaHash hash,
            int textureWidth = s_invalidTextureWidth,
            int blockDim = -1,
            int elementCount = -1)
        {
            m_hash = hash;
            m_blockDim = blockDim;
            m_mosaicDescriptorInfos = mosaicDescriptorInfos;
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
             
                    m_externalTextures[i] = Texture2D.CreateExternalTexture(
                        mosaicDescriptorInfo.m_textureHeight,
                        mosaicDescriptorInfo.m_textureWidth,
                        TextureFormat.R8, 
                        false, 
                        true, 
                        (IntPtr)mosaicDescriptorInfo.m_externalNativeHandle);
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

        public MosaicDescriptorInfo[] GetMosaicDescriptorInfos()
        {
            return m_mosaicDescriptorInfos;
        }

        public AquaHash GetAquaHash()
        {
            return m_hash;
        }

        public AttributeEncoding GetEncoding()
        {
            return m_encoding;
        }

        public int GetElementCount()
        {
            if (m_elementCount > 0)
            {
                return m_elementCount;
            }
            return m_nativeArray.Length / GetBytesPerElement();
        }

        public int GetBytesPerElement()
        {
            return m_encoding.GetBytesPerElement();
        }

        public int GetTotalBytes()
        {
            if (m_elementCount > 0)
            {
                return GetElementCount() * GetBytesPerElement();
            }
            return m_nativeArray.Length;
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
            return CalculateTextureSize(GetElementCount(), m_textureWidth);
        }
        
        public int GetBlockDim()
        {
            return m_blockDim;
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
