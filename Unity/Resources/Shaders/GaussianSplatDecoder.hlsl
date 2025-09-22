// Copyright © 2024 Miris. All rights reserved.

#pragma once

// In this file we have enabled the use of shader keywords to generate shader variants at compile time,
// eliminating dynamic branching for figuring out how to decode our data at runtime.
//
// See DataBuffer.cs for how the shader keywords below are derived.
// For more information about shader keywords, see https://docs.unity3d.com/Manual/shader-keywords.html
//
// As an example for extending GPU decoding support...
// If the "Position" semantic has a new encoding called "Foo":
//  1. Add a Position_Foo to the same line as #pragma Position_Float32x3
//  2. Add a new shader function e.g. DecodeFloat3FromFoo that decodes the "Foo" encoding into a position vlaue
//  3. Add #elif Position_Float32x3 branch in GetSplatPosition to call into the newly defined DecodeFloat3FromFoo

#pragma multi_compile_local Position_Float32x3 Position_Float16x4 Position_Float16x3 Position_UInt16x3
#pragma multi_compile_local Scale_Float32x3 Scale_RGBA_Compressed_ASTC_4x4_LDR Scale_Float16x3 Scale_Float16x4
#pragma multi_compile_local Orientation_Float32x4 Orientation_RGBA_Compressed_ASTC_4x4_LDR Orientation_Float16x4
#pragma multi_compile_local Color_Float32x4 Color_RGBA_Compressed_ASTC_4x4_LDR Color_Float16x4
#pragma multi_compile_local SHCoefficients_Float32x3 SHCoefficients_Float16x3 SHCoefficients_None

#include "SphericalHarmonics.hlsl"
#include "ColorUtils.hlsl"

uint _BlockDim;

// Gaussian splat input data.w
#if defined(Position_Float32x3) || defined(Position_Float16x3) || defined(Position_Float16x4)
ByteAddressBuffer _Positions;
#elif defined(Position_UInt16x3)
ByteAddressBuffer _Positions;
ByteAddressBuffer _BlockBounds;
#endif

#if defined(Color_Float32x4) || defined(Color_Float16x4)
ByteAddressBuffer _Colors;
#elif defined(Color_RGBA_Compressed_ASTC_4x4_LDR)
Texture2D _Colors;
int _ColorsTextureWidth;
#endif

#if defined(Scale_Float32x3) || defined(Scale_Float16x3) || defined(Scale_Float16x4)
ByteAddressBuffer _Scales;
#elif defined(Scale_RGBA_Compressed_ASTC_4x4_LDR)
Texture2D _Scales;
int _ScalesTextureWidth;
#endif

#if defined(Orientation_Float32x4) || defined(Orientation_Float16x4)
ByteAddressBuffer _Orientations;
#elif defined(Orientation_RGBA_Compressed_ASTC_4x4_LDR)
Texture2D _Orientations;
int _OrientationsTextureWidth;
#endif

#if defined(SHCoefficients_Float32x3) || defined(SHCoefficients_Float16x3)
ByteAddressBuffer _SHCoefficients;
#endif

StructuredBuffer<uint> _SplatToDataSourceIndex;
StructuredBuffer<float> _DataSourceOpacity;
StructuredBuffer<int> _DataSourceLodIndex;
int _DataSourceMinLodIndex;
int _DataSourceMaxLodIndex;

void GetBlockInfo(out uint dim, out uint size)
{
    dim = _BlockDim;
    size = _BlockDim * _BlockDim;
}

// ----------------------------------------------------------------------------
// Texture helpers
// ----------------------------------------------------------------------------

uint3 SplatIndexToTextureIndex(int textureWidth, uint splatIndex) {
    // In the near future, the texture may be organized in blocks e.g. by Morton
    // encoding.  So we may need to decode that here.
    // See: https://aras-p.info/blog/2023/09/13/Making-Gaussian-Splats-smaller/
    uint dim, blockSize;
    GetBlockInfo(dim, blockSize);

    uint blockIndex = splatIndex / blockSize;
    uint intraBlockIndex = splatIndex % blockSize;
    uint textureWidthBlocks = textureWidth / blockSize;

    uint2 blockXY = uint2(blockIndex % textureWidthBlocks, blockIndex / textureWidthBlocks) +
                    uint2(intraBlockIndex % dim, intraBlockIndex / dim);
    return uint3(blockXY, 0);
}

// ----------------------------------------------------------------------------
// float3 decoding
// ----------------------------------------------------------------------------

float3 DecodeFloat3FromFloat32x3(ByteAddressBuffer dataBuffer, uint splatIndex) {
    uint stride = 12;
    uint address = stride * splatIndex;

    uint3 val = dataBuffer.Load3(address);

    return asfloat(val);
}

float3 DecodeFloat3FromFloat16x3(ByteAddressBuffer dataBuffer, uint splatIndex) {
    uint stride = 6;
    uint address = stride * splatIndex;
    uint alignedAddress = (address / 4) * 4;

    // ByteAddressBuffer only supports 4 byte aligned loads
    int aligned = (alignedAddress == address);

    // offsets :    [ 0,  2,  4,  6,  8, 10, 12, 14, 16, ...   ]
    // 4b aligned:  [ X,   ,  X,   ,  X,   ,  X,   ,  X, ...   ]
    // f16 data:    [x0, y0, z0, x1, y1, z1, x2, y2, z2, ...   ]

    // to load (x0, y0, z0) we load 2 * 4 bytes at address 0 and use the first 3 * u16
    // to load (x1, y1, z1) we load 2 * 4 bytes at address 4 and skip the first u16 and read the next 3 * u16

    // load 2 * 4 bytes of data into uint2
    // offsets:  [  0,  2,  4,  6]
    // data:     [u16,u16,u16,u16]
    // aligned:  [  x,  y,  z,  -]
    // unaligned:[  -,  x,  y,  z]
    //           [    u32,    u32]
    uint2 data = dataBuffer.Load2(alignedAddress);

    uint3 val = aligned ?
                         uint3(data[0] & 0xFFFF, data[0] >> 16, data[1] & 0xFFFF) :
                        uint3(data[0] >> 16, data[1] & 0xFFFF, data[1] >> 16);

    return f16tof32(val);
}

void DecodeBounds(ByteAddressBuffer boundsDataBuffer, uint splatIndex, out float3 boundsMin, out float3 boundsMax) {
    uint dim, blockSize;
    GetBlockInfo(dim, blockSize);

    uint blockIndex = splatIndex / blockSize;
    uint boundsStride = 24;
    uint boundsIndex = blockIndex * boundsStride;

    uint3 rmin = boundsDataBuffer.Load3(boundsIndex);
    uint3 rmax = boundsDataBuffer.Load3(boundsIndex + 12);

    boundsMin = asfloat(rmin);
    boundsMax = asfloat(rmax);
}

float3 DecodeFloat3FromUInt16x3(ByteAddressBuffer dataBuffer, ByteAddressBuffer boundsDataBuffer, uint splatIndex) {
    float3 boundsMin, boundsMax;

    DecodeBounds(boundsDataBuffer, splatIndex, boundsMin, boundsMax);

    int aligned = splatIndex % 2 == 0;

    uint dataStride = 6;
    uint address = dataStride * splatIndex;
    uint alignedAddress = (address / 4) * 4;

    // ByteAddressBuffer only supports 4 byte aligned loads
    uint2 data = dataBuffer.Load2(alignedAddress);
    uint3 val = aligned ? uint3(data[0] & 0xFFFF, data[0] >> 16, data[1] & 0xFFFF)
                        : uint3(data[0] >> 16, data[1] & 0xFFFF, data[1] >> 16);

    float3 normalized = saturate(val / 65535.0f);
    return normalized * (boundsMax - boundsMin) + boundsMin;
}


float4 DecodeFloat4FromFloat16x4(ByteAddressBuffer dataBuffer, uint splatIndex) {
    uint stride = 8;
    uint address = stride * splatIndex;

    // offsets :    [ 0,  2,  4,  6,  8, 10, 12, 14, 16, ...   ]
    // 4b aligned:  [ X,   ,  X,   ,  X,   ,  X,   ,  X, ...   ]
    // f16 data:    [x0, y0, z0, w0, x1, y1, z1, w1, x2, y2, z2, ...   ]

    // load 2 * 4 bytes of data into uint2
    // offsets: [  0,  2,  4,  6]
    // data:    [u16,u16,u16,u16]
    //          [    u32,    u32]
    uint2 data = dataBuffer.Load2(address);
    // make uint4 from the 4 u16s
    uint4 val = uint4(data[0] & 0xFFFF, data[0] >> 16, data[1] & 0xFFFF, data[1] >> 16);
    // convert the halves in each component to float3
    return f16tof32(val);
}

float3 DecodeFloat3FromFloat16x4(ByteAddressBuffer dataBuffer, uint splatIndex) {
    return DecodeFloat4FromFloat16x4(dataBuffer, splatIndex).xyz;
}

// ----------------------------------------------------------------------------
// float4 decoding
// ----------------------------------------------------------------------------

float4 DecodeFloat4FromFloat32x4(ByteAddressBuffer dataBuffer, uint splatIndex) {
    uint stride = 16;
    uint address = stride * splatIndex;
    uint4 val = dataBuffer.Load4(address);
    return asfloat(val);
}

float4 DecodeFloat4FromTexture(Texture2D tex, int textureWidth, uint splatIndex) {
    uint3 textureIndex = SplatIndexToTextureIndex(textureWidth, splatIndex);
    return tex.Load(textureIndex);
}

SplatSHData DecodeSHData(ByteAddressBuffer dataBuffer, uint splatIndex, int shCount) {

    uint stride = 12; // 3 * sizeof(float) = 3 * 4 
    uint baseAddress = stride * splatIndex * shCount;

    SplatSHData shData;

    for (int i = 0; i < shCount; i++) {

        shData.sh[i] = asfloat(dataBuffer.Load3(baseAddress + stride * i));
    }
    
    return shData;
}

SplatSHData DecodeSHDataFromHalf(ByteAddressBuffer dataBuffer, uint splatIndex,  int shCount) {
    uint stride = 6; // 3 * sizeof(half) = 3 * 2 = 6
    uint baseAddress = stride * splatIndex * shCount;

    SplatSHData shData;

    uint alignedAddress = (baseAddress / 4) * 4;
    
    // aligned reads into data // 6 aligned reads = 6 * 4 = 24 (u32 values) = 48 float16 
    // When we're not aligned we waste the first 2 bytes
    uint4 data[6];
    for (int i = 0; i < 6; ++i) {
        data[i] = dataBuffer.Load4(alignedAddress + i * 16);
    }

    if (alignedAddress == baseAddress) {
        shData.sh[0] = f16tof32(float3(data[0].x & 0xFFFF, data[0].x >> 16, data[0].y & 0xFFFF));
        shData.sh[1] = f16tof32(float3(data[0].y >> 16, data[0].z & 0xFFFF, data[0].z >> 16));
        shData.sh[2] = f16tof32(float3(data[0].w & 0xFFFF, data[0].w >> 16, data[1].x & 0xFFFF));
        shData.sh[3] = f16tof32(float3(data[1].x >> 16, data[1].y & 0xFFFF, data[1].y >> 16));
        shData.sh[4] = f16tof32(float3(data[1].z & 0xFFFF, data[1].z >> 16, data[1].w & 0xFFFF));
        shData.sh[5] = f16tof32(float3(data[1].w >> 16, data[2].x & 0xFFFF, data[2].x >> 16));
        shData.sh[6] = f16tof32(float3(data[2].y & 0xFFFF, data[2].y >> 16, data[2].z & 0xFFFF));
        shData.sh[7] = f16tof32(float3(data[2].z >> 16, data[2].w & 0xFFFF, data[2].w >> 16));
        shData.sh[8] = f16tof32(float3(data[3].x & 0xFFFF, data[3].x >> 16, data[3].y & 0xFFFF));
        shData.sh[9] = f16tof32(float3(data[3].y >> 16, data[3].z & 0xFFFF, data[3].z >> 16));
        shData.sh[10] = f16tof32(float3(data[3].w & 0xFFFF, data[3].w >> 16, data[4].x & 0xFFFF));
        shData.sh[11] = f16tof32(float3(data[4].x >> 16, data[4].y & 0xFFFF, data[4].y >> 16));
        shData.sh[12] = f16tof32(float3(data[4].z & 0xFFFF, data[4].z >> 16, data[4].w & 0xFFFF));
        shData.sh[13] = f16tof32(float3(data[4].w >> 16, data[5].x & 0xFFFF, data[5].x >> 16));
        shData.sh[14] = f16tof32(float3(data[5].y & 0xFFFF, data[5].y >> 16, data[5].z & 0xFFFF));
    } else {
        shData.sh[0] = f16tof32(float3(data[0].x >> 16, data[0].y & 0xFFFF, data[0].y >> 16));
        shData.sh[1] = f16tof32(float3(data[0].z & 0xFFFF, data[0].z >> 16, data[0].w & 0xFFFF));
        shData.sh[2] = f16tof32(float3(data[0].w >> 16, data[1].x & 0xFFFF, data[1].x >> 16));
        shData.sh[3] = f16tof32(float3(data[1].y & 0xFFFF, data[1].y >> 16, data[1].z & 0xFFFF));
        shData.sh[4] = f16tof32(float3(data[1].z >> 16, data[1].w & 0xFFFF, data[1].w >> 16));
        shData.sh[5] = f16tof32(float3(data[2].x & 0xFFFF, data[2].x >> 16, data[2].y & 0xFFFF));
        shData.sh[6] = f16tof32(float3(data[2].y >> 16, data[2].z & 0xFFFF, data[2].z >> 16));
        shData.sh[7] = f16tof32(float3(data[2].w & 0xFFFF, data[2].w >> 16, data[3].x & 0xFFFF));
        shData.sh[8] = f16tof32(float3(data[3].x >> 16, data[3].y & 0xFFFF, data[3].y >> 16));
        shData.sh[9] = f16tof32(float3(data[3].z & 0xFFFF, data[3].z >> 16, data[3].w & 0xFFFF));
        shData.sh[10] = f16tof32(float3(data[3].w >> 16, data[4].x & 0xFFFF, data[4].x >> 16));
        shData.sh[11] = f16tof32(float3(data[4].y & 0xFFFF, data[4].y >> 16, data[4].z & 0xFFFF));
        shData.sh[12] = f16tof32(float3(data[4].z >> 16, data[4].w & 0xFFFF, data[4].w >> 16));
        shData.sh[13] = f16tof32(float3(data[5].x & 0xFFFF, data[5].x >> 16, data[5].y & 0xFFFF));
        shData.sh[14] = f16tof32(float3(data[5].y >> 16, data[5].z & 0xFFFF, data[5].z >> 16));
    }
    
    return shData;
}

// ----------------------------------------------------------------------------
// Public API
// ----------------------------------------------------------------------------

float3 GetSplatPosition(uint splatIndex) {
#if defined(Position_Float32x3)
    return DecodeFloat3FromFloat32x3(_Positions, splatIndex);
#elif defined(Position_Float16x3)
    return DecodeFloat3FromFloat16x3(_Positions, splatIndex);
#elif defined(Position_Float16x4)
    return DecodeFloat3FromFloat16x4(_Positions, splatIndex);
#elif defined(Position_UInt16x3)
    return DecodeFloat3FromUInt16x3(_Positions, _BlockBounds, splatIndex);
#endif
}

float3 GetSplatScale(uint splatIndex) {
#if defined(Scale_Float32x3)
    return DecodeFloat3FromFloat32x3(_Scales, splatIndex);
#elif defined(Scale_Float16x3)
    return DecodeFloat3FromFloat16x3(_Scales, splatIndex);
#elif defined(Scale_Float16x4)
    return DecodeFloat3FromFloat16x4(_Scales, splatIndex);
#elif defined(Scale_RGBA_Compressed_ASTC_4x4_LDR)
    return DecodeFloat4FromTexture(_Scales, _ScalesTextureWidth, splatIndex);
#endif
}

float4 GetSplatOrientation(uint splatIndex) {
#if defined(Orientation_Float32x4)
    return DecodeFloat4FromFloat32x4(_Orientations, splatIndex);
#elif defined(Orientation_Float16x4)
    return DecodeFloat4FromFloat16x4(_Orientations, splatIndex);
#elif defined(Orientation_RGBA_Compressed_ASTC_4x4_LDR)
    return DecodeFloat4FromTexture(_Orientations, _OrientationsTextureWidth, splatIndex);
#endif
}

float4 GetSplatColor(uint splatIndex) {
#if defined(Color_Float32x4)
    return DecodeFloat4FromFloat32x4(_Colors, splatIndex);
#elif defined(Color_Float16x4)
    return DecodeFloat4FromFloat16x4(_Colors, splatIndex);
#elif defined(Color_RGBA_Compressed_ASTC_4x4_LDR)
    return DecodeFloat4FromTexture(_Colors, _ColorsTextureWidth, splatIndex);
#endif
}

SplatSHData GetSplatSHData(uint splatIndex, int shCount) {
#if defined(SHCoefficients_Float32x3)
    return DecodeSHData(_SHCoefficients, splatIndex, shCount);
#elif defined(SHCoefficients_Float16x3)
    return DecodeSHDataFromHalf(_SHCoefficients, splatIndex, shCount);
#elif defined(SHCoefficients_None)
    SplatSHData shData = (SplatSHData)0;
    return shData;
#endif
}

float GetDataSourceOpacity(uint splatIndex)
{
    uint dataSourceIndex = _SplatToDataSourceIndex[splatIndex];
    return _DataSourceOpacity[dataSourceIndex];
}

float4 GetDataSourceLodIndexColor(uint splatIndex)
{
    uint dataSourceIndex = _SplatToDataSourceIndex[splatIndex];
    int lodIndex = _DataSourceLodIndex[dataSourceIndex];

    // Compute the normalized lod index value between min and max, handling the case when min == max
    float lodIndexNorm = clamp(lodIndex, _DataSourceMinLodIndex, _DataSourceMaxLodIndex);
    if ((_DataSourceMaxLodIndex - _DataSourceMinLodIndex) != 0)
    {
        lodIndexNorm = (lodIndexNorm - _DataSourceMinLodIndex) / (_DataSourceMaxLodIndex - _DataSourceMinLodIndex);
    }
    else
    {
        lodIndexNorm = 0.0f;
    }

    return float4(HueToRgb(lodIndexNorm * 0.6f), 1.0f);
}
