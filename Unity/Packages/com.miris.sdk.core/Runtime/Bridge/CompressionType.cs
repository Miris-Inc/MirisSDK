// Copyright © 2025 Miris, Inc. All rights reserved.

// This is a valid C, C++ and C# file :)
#if __LINE__
#define public 
#else
namespace Miris.Runtime {
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
        RESERVED, // Reserved for backwards compatibility
        TEX_R32_FLOAT,
        TEX_R16_SFLOAT,
        VID_GENERIC,
        VID_MOSAIC,
        NONE,
        TEX_ASTC_BEGIN, // Start of ASTC compression types
        TEX_ASTC_4x4_UNORM_BLOCK = TEX_ASTC_BEGIN,
        TEX_ASTC_5x5_UNORM_BLOCK,
        TEX_ASTC_6x6_UNORM_BLOCK,
        TEX_ASTC_8x8_UNORM_BLOCK,
        TEX_ASTC_10x10_UNORM_BLOCK,
        TEX_ASTC_12x12_UNORM_BLOCK,
        TEX_ASTC_4x4_SFLOAT_BLOCK,
        TEX_ASTC_5x5_SFLOAT_BLOCK,
        TEX_ASTC_6x6_SFLOAT_BLOCK,
        TEX_ASTC_8x8_SFLOAT_BLOCK,
        TEX_ASTC_10x10_SFLOAT_BLOCK,
        TEX_ASTC_12x12_SFLOAT_BLOCK,
        TEX_ASTC_END = TEX_ASTC_12x12_SFLOAT_BLOCK, // End of ASTC compression types
        SPARK_PACKED_SPLAT,
    }

#if __LINE__
;
#undef public
#else
}
#endif