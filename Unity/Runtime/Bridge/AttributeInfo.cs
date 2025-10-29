// This is a valid C++ and C# file :)

#if __cplusplus
#define public 
#define unsafe 
#else
#define USING_CSHARP
#endif 

#if USING_CSHARP
using System.Runtime.InteropServices;
namespace Aqua.Runtime
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

    // For transporting an AttributeArray (in C++) over to AttributeBuffer (C#) 
#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
#endif
    unsafe public struct AttributeInfo
    {
        public int m_elementCount;
        public int m_bytesPerElement;
        public void* m_dataPtr;
        public int m_elementType;
        public CompressionType m_compressionType;
        public int m_textureWidth;
        public int m_textureHeight;
        public void* m_mosaicDescriptors;
        public int m_mosaicDescriptorCount;
        public int m_blockDim;
        public int m_hash0;
        public int m_hash1;
        public int m_hash2;
        public int m_hash3;
    }
#if USING_CSHARP
}
#else
;
#undef public 
#endif
