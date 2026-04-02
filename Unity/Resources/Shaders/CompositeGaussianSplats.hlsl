// Copyright © 2026 Miris, Inc. All rights reserved.

#pragma vertex vert
#pragma fragment frag

#if defined(USING_URP)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#else
// BIRP fallback
#include "UnityCG.cginc"
// Define URP-style macro for BIRP compatibility
#ifndef SAMPLE_TEXTURE2D_ARRAY
#define SAMPLE_TEXTURE2D_ARRAY(tex, samp, uv, idx) tex.Sample(samp, float3(uv, idx))
#endif
#endif
#include "CommonConstants.hlsl"

#ifdef STEREO_MULTIVIEW_ON
Texture2DArray _GaussianSplatRT;
SamplerState sampler_GaussianSplatRT;
#define sample_Gaussian(tex, sampler,uv, texSize) SAMPLE_TEXTURE2D_ARRAY(tex, sampler,uv,unity_StereoEyeIndex);
#else
Texture2D _GaussianSplatRT;
SamplerState sampler_GaussianSplatRT;
#define sample_Gaussian(tex, sampler, uv, texSize) tex.Load(int3(uv*texSize, 0)); 
#endif

// GammaToLinearSpace is declared in UnityCG.cginc, so we have to use this namespaced function name
inline half3 MirisGammaToLinearSpace (half3 sRGB)
{
    // Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return sRGB * (sRGB * (sRGB * 0.305306011h + 0.682171111h) + 0.012522878h);

    // Precise version, useful for debugging.
    //return half3(GammaToLinearSpaceExact(sRGB.r), GammaToLinearSpaceExact(sRGB.g), GammaToLinearSpaceExact(sRGB.b));
}

struct v2f {
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert(uint vtxID : SV_VertexID, uint instanceID: SV_InstanceID) {
    v2f o;

    //fetch the quad vertices
    o.vertex = float4(quadPositionsInClipSpace[vtxID], 0.0, 1.0);
    
    //set uv coords in range [0,1] (UV space)
    o.uv = (quadPositionsInClipSpace[vtxID] + 1.0) * 0.5;
    
    //reverse the buffer flip test
    if (_ProjectionParams.x < 0.0) {
        o.uv.y = 1.0 - o.uv.y;
    }

    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    return o;
}

half4 frag(v2f i) : SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    //Get the texture dimensions -
    int2 textureSize = int2(_ScreenParams.x, _ScreenParams.y);

    //now fetch the texel at the coordinate
    half4 col = sample_Gaussian(_GaussianSplatRT, sampler_GaussianSplatRT, i.uv, textureSize);
    
    //do color corrections
    //col.rgb = GammaToLinearSpace(col.rgb);
    col.a = saturate(col.a);
    #ifdef DEBUG_TOTAL_OPACITY
        if (col.a == 0.0f)
        {
            col.rgb = float3(0, 1, 0);

        }else
        {
            col.a = int(col.a + 0.05); // anything more than 5% transparent will flagged as hole.
            col.rgb = col.aaa; 
        }
        col.a = 1;
    #else
        //do color corrections
        col.rgb = MirisGammaToLinearSpace(col.rgb);
    #endif
    return col;
}