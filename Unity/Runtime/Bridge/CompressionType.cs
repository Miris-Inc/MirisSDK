// This is a valid C, C++ and C# file :)
#if __LINE__
#define public 
#else
namespace Aqua.Runtime {
#endif

    public enum CompressionType
    {
        TEX_R8G8B8_UNORM = 0,
        TEX_R16G16B16_SFLOAT,
        TEX_R16G16B16_UNORM,
        TEX_R32G32B32_FLOAT,
        TEX_R8G8B8A8_UNORM,
        TEX_R16G16B16A16_SFLOAT,
        TEX_R16G16B16A16_UNORM,
        TEX_R32G32B32A32_FLOAT,
        TEX_R11G10B11_UNORM,
        TEX_R10G10B10_UNORM,
        TEX_ASTC_4x4_UNORM_BLOCK,
        TEX_R32_FLOAT,
        TEX_R16_SFLOAT,
        VID_GENERIC,
        VID_MOSAIC,
        NONE
    }

#if __LINE__
;
#undef public
#else
}
#endif