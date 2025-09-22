// Copyright © 2024 Miris. All rights reserved.

#pragma vertex vert
#pragma fragment frag

#include "GaussianSplatting.hlsl"
#include_with_pragmas "GaussianSplatDecoder.hlsl"

// See RenderGaussianSplats.shader for this definition
DECLARE_AOV

// Inputs
StructuredBuffer<SplatViewData> _GpuSplat;
float _GaussianSigmaThreshold;
float _AlphaCullingThreshold;
float _NearClipThreshold;
float _FadeLargeSplats;
int _EyeStride;

// Constants
static const float _QuadHalfLength = 2.0;

// Macro to set vertexOutput SV_POSITION to NaN which
// triggers a discard of the entire primitive bypassing
// the vert shader
#define EARLY_PRIM_DISCARD \
    vertexOutput.splatCenterClipPosition = asfloat(0x7fc00000); \
    return vertexOutput; 

// Vertex shader output.
struct VertexOutput {
    float4 splatCenterClipPosition : SV_POSITION;
    float4 color : COLOR0;
    float2 quadNDCPosition: TEXCOORD0;
    float splatFade : TEXCOORD1;
    UNITY_VERTEX_OUTPUT_STEREO
};

// Convert _GaussianSigmaThreshold to units for scaling down our quad.  
inline float GaussianSigmaScale()
{
    return _GaussianSigmaThreshold / 3.0;
}

inline float CalculateGaussianAlpha(VertexOutput vertexOutput)
{
    float power = -dot(vertexOutput.quadNDCPosition, vertexOutput.quadNDCPosition);
    return vertexOutput.color.a * exp(power) * (1.0 - vertexOutput.splatFade);
}

// Attempt to compute a fade value based on the splat size 
inline float CalculateLargeSplatFade(SplatViewData splatData)
{
    float splatCentreDist = length(splatData.pos.xyz/splatData.pos.w) ;
    float splatCentreScaler = 1.0 - saturate(splatCentreDist);
    // Bump the scale slightly based on proximity to the centre of the view
    // this is an attempt to counter the effect of problematic splats which 
    // elongate at the edges of the display. 
    splatCentreScaler = 1.0 + pow(splatCentreScaler, 0.5) * 0.5 ;
    float splatScale =
            GaussianSigmaScale() * 6.0 * splatCentreScaler * length(splatData.majorAxis + splatData.minorAxis) / _ScreenParams.y;
    // Ensure most of the splats are left intact 
    // expose this lower limit to dial the effect in
    return smoothstep(0.5, 1.0, splatScale);
}

// This vertex shader produces quads representing the Gaussian splat ellipsoids.
//
// "vertexId" indexes into the quad topology (4 unique vertices),
// "instanceId" maps to a single splat.
VertexOutput vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID) {
    
    int baseOffset = unity_StereoEyeIndex * _EyeStride;
    SplatViewData splatView = _GpuSplat[instanceId + baseOffset];
    
    VertexOutput vertexOutput;

    bool splatClipNear = splatView.pos.w <= _NearClipThreshold;
    if (splatClipNear)
    {
        EARLY_PRIM_DISCARD
    }

    vertexOutput.splatFade = 0.0;
    if (_FadeLargeSplats > 0.0) {
        vertexOutput.splatFade = CalculateLargeSplatFade(splatView);
        if (vertexOutput.splatFade > 0.99) {
            EARLY_PRIM_DISCARD
        }
    }

    // Set the vertex color (RGBA)
    vertexOutput.color = SAMPLE_AOV;
    //remove splats with too low of alpha values early on
    if (vertexOutput.color.a <  _AlphaCullingThreshold)
    {
        EARLY_PRIM_DISCARD
    }
    
    //The quadVertexPositionNDC calculation below may look a bit cryptic.
    //if you don't know how we are extracting the quad vertices, read the comment at the end of the shader.
    vertexOutput.quadNDCPosition = getCurrentQuadVertex(vertexId) * _QuadHalfLength - 1.0;
    vertexOutput.quadNDCPosition *= _QuadHalfLength;

    // Clip the quad size by gaussian sigma threshold
    vertexOutput.quadNDCPosition *= GaussianSigmaScale();

    // Transform the quad vertex based on splat view center + axis.
    float2 deltaScreenPosition =
        (vertexOutput.quadNDCPosition.x * splatView.majorAxis + vertexOutput.quadNDCPosition.y * splatView.minorAxis) * 2 /
        _ScreenParams.xy;
    
    vertexOutput.splatCenterClipPosition = splatView.pos;
    vertexOutput.splatCenterClipPosition.xy += deltaScreenPosition * splatView.pos.w;

    // Correct for buffer Y inversion on certain platforms
    if (_ProjectionParams.x < 0) {
        vertexOutput.splatCenterClipPosition.y = -1 * vertexOutput.splatCenterClipPosition.y;
    }
    
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(vertexOutput);
    return vertexOutput;
}

float4 frag(VertexOutput vertexOutput) : SV_Target {

    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(vertexOutput);
    
    // Further clip the square quad, by discarding fragments outside of the inscribed circle.
    float radius = GaussianSigmaScale() * _QuadHalfLength;
    if (length(vertexOutput.quadNDCPosition) > radius) {
        discard;
    }
    
    float alpha = SAMPLE_ALPHA;
    
    // discard fragments with too low of alpha values
    if (alpha < (_AlphaCullingThreshold * 0.125)) {
        discard;
    }
    
    return float4(vertexOutput.color.rgb * alpha, alpha);
}
