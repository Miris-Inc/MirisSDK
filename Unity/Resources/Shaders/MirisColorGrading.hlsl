// Copyright © 2026 Miris, Inc. All rights reserved.

#pragma once

#define MIRIS_GRADE_SPACE_SRGB 0
#define MIRIS_GRADE_SPACE_LINEAR 1

#define MIRIS_GRADE_ALPHA_STRAIGHT 0
#define MIRIS_GRADE_ALPHA_PREMULTIPLIED 1

// A Gamma project hands the pass values that are already sRGB-encoded; a Linear one does not.
// Encoding a Gamma buffer a second time feeds the LUT a doubly-encoded colour, which an identity
// LUT hides - the decode on the way out cancels it - and any real grade gets wrong.
#if defined(UNITY_COLORSPACE_GAMMA)
    #define MIRIS_GRADE_SPACE_ACTIVE MIRIS_GRADE_SPACE_SRGB
#else
    #define MIRIS_GRADE_SPACE_ACTIVE MIRIS_GRADE_SPACE_LINEAR
#endif

#define MIRIS_GRADE_EPSILON 1e-5

// lerp(step()) rather than a ternary so the selection is unambiguously component-wise, and the pow
// arguments are clamped away from zero so a NaN can never be lerped in.
float3 MirisSRGBToLinearExact(float3 c)
{
    float3 lo = c * (1.0 / 12.92);
    float3 hi = pow(max(c + 0.055, 1e-8) * (1.0 / 1.055), 2.4);
    return lerp(lo, hi, step(0.04045, c));
}

float3 MirisLinearToSRGBExact(float3 c)
{
    float3 lo = c * 12.92;
    float3 hi = 1.055 * pow(max(c, 1e-8), 1.0 / 2.4) - 0.055;
    return lerp(lo, hi, step(0.0031308, c));
}

#ifdef MIRIS_COLOR_GRADING_LUT

// (1/width, 1/height, height - 1, unused)
float4 _MirisLutParams;

// Blend between ungraded and fully graded. Exactly 0 is the identity.
float _MirisLutStrength;

// Shark's composite is a CGPROGRAM and defines MIRIS_LUT_COMBINED_SAMPLER before including this,
// because Texture2D/SamplerState object syntax cannot be assumed there. Neither branch uses
// SAMPLE_TEXTURE2D or TEXTURE2D_PARAM: those are URP-only
#if defined(MIRIS_LUT_COMBINED_SAMPLER)
    sampler2D _MirisLutTex;
    #define MIRIS_SAMPLE_LUT(uv) tex2Dlod(_MirisLutTex, float4((uv), 0.0, 0.0))
#else
    Texture2D _MirisLutTex;
    SamplerState sampler_MirisLutTex;
    #define MIRIS_SAMPLE_LUT(uv) _MirisLutTex.SampleLevel(sampler_MirisLutTex, (uv), 0.0)
#endif

// Standard horizontal-strip LUT: `size` square slices side by side, slice selected by blue.
// Hardware bilinear interpolates red (x) and green (y) within a slice; the lerp below interpolates
// blue between the two nearest slices.
//
// The half-texel offset and the (size - 1) scale together place every sample between the first and
// last texel CENTRE of its slice, so hardware bilinear in x can never bleed into a neighbouring
// slice. That correctness depends on the LUT being uncompressed and mipmap-free, which
// MirisColorGradeState.Validate checks.
float3 MirisApplyLut2D(float3 c)
{
    c = saturate(c);

    float3 params = _MirisLutParams.xyz;
    float sliceCoordinate = c.b * params.z;
    float slice = floor(sliceCoordinate);
    float sliceBlend = sliceCoordinate - slice;

    float2 uv = c.rg * params.z * params.xy + params.xy * 0.5;
    uv.x += slice * params.y;

    float3 lowerSlice = MIRIS_SAMPLE_LUT(uv).rgb;
    float3 upperSlice = MIRIS_SAMPLE_LUT(uv + float2(params.y, 0.0)).rgb;
    return lerp(lowerSlice, upperSlice, sliceBlend);
}

#endif // MIRIS_COLOR_GRADING_LUT

float4 MirisApplyColorGrade(float4 source, const int space, const int alphaMode)
{
#ifndef MIRIS_COLOR_GRADING_LUT
    return source;
#else
    float alpha = source.a;
    bool premultiplied = (alphaMode == MIRIS_GRADE_ALPHA_PREMULTIPLIED);

    float3 encoded = (space == MIRIS_GRADE_SPACE_LINEAR)
        ? MirisLinearToSRGBExact(source.rgb)
        : source.rgb;

    // Unpremultiply. A LUT is a non-linear function, so it has to see the splat's own colour
    // rather than that colour scaled by its coverage - grading premultiplied values would make the
    // grade drift with transparency, which reads as a lighting bug rather than a grading one.
    // saturate because 8 bit quantisation can round rgb up while rounding alpha down.
    // Skipped for straight-alpha sources (e.g. a full-screen camera buffer), where rgb was never
    // scaled by alpha in the first place.
    float3 color = premultiplied ? saturate(encoded / max(alpha, MIRIS_GRADE_EPSILON)) : encoded;

    if (_MirisLutStrength > 0.0)
    {
        color = lerp(color, MirisApplyLut2D(color), _MirisLutStrength);
    }

    // Re-premultiply with the original alpha
    encoded = premultiplied ? color * alpha : color;

    float3 graded = (space == MIRIS_GRADE_SPACE_LINEAR)
        ? MirisSRGBToLinearExact(encoded)
        : encoded;

    return float4(graded, alpha);
#endif
}
