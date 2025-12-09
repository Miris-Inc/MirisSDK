// Copyright © 2025 Miris, Inc. All rights reserved.

// This is a valid C++ and C# file :)

#if __cplusplus
#define public 
#define unsafe 
#else
#define USING_CSHARP
#endif 

#if USING_CSHARP
using System.Runtime.InteropServices;
namespace Miris.Runtime
{
#endif



#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
#endif
    unsafe public struct MosaicDescriptorInfo
    {
        public int m_mosaicTileWidth;
        public int m_mosaicTileHeight;
        public int m_mosaicTileX;
        public int m_mosaicTileY;
        public int m_interleaveType;
        public int m_offset;
        public int m_stride;
        public int m_eccPart;
        public float m_min;
        public float m_max;
        public int m_isShColor;
        public int m_textureWidth;
        public int m_textureHeight;
        public void* m_externalNativeHandle;
        public int m_isRangeNormalized;
    };

    // Vector4 struct compatible with both C++ and C#
#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
#endif
    public struct Vector4Value
    {
        public float x;
        public float y;
        public float z;
        public float w;
    };

    // For transporting an AttributeArray (in C++) over to AttributeBuffer (C#) 
#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
#endif
    unsafe public struct AttributeInfo
    {
        public int m_elementCount;
        public int m_bytesPerElement;
        public int m_dataSizeBytes;       // Total data buffer size in bytes
        public void* m_dataPtr;
        public int m_elementType;
        public CompressionType m_compressionType;
        public int m_textureWidth;
        public int m_textureHeight;
        public int m_splatCount;
        public void* m_mosaicDescriptors;
        public int m_mosaicDescriptorCount;
        public int m_blockDim;
        public int m_hash0;
        public int m_hash1;
        public int m_hash2;
        public int m_hash3;
        // Per-component min/max values for ASTC texture normalization
        public Vector4Value m_minValue;
        public Vector4Value m_maxValue;
        public int m_isRangeNormalized;
        public int m_blockScanlineOrder;
    }
#if USING_CSHARP
}
#else
;
#undef public 
#endif
