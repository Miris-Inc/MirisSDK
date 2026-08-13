// Copyright © 2024 Miris. All rights reserved.

Shader "Miris/Debug Render Bounds" {
    SubShader {
        Pass {
            ZTest On	
            Cull Off

            CGPROGRAM

#pragma vertex vert
#pragma fragment frag

StructuredBuffer<float3> _Positions;
float3 _BoxCenter;
float3 _BoxExtents;
float4 _Color;

struct VertexOutput
{
    float4 position : SV_POSITION;
};

VertexOutput vert(uint vertexId : SV_VertexID)
{
    VertexOutput vertexOutput;
    float3 position = _Positions[vertexId];
    position = float3(position.x * _BoxExtents.x, position.y * _BoxExtents.y, position.z * _BoxExtents.z);
    position = position + _BoxCenter;
    vertexOutput.position = UnityObjectToClipPos(position);
    return vertexOutput;
}

half4 frag(VertexOutput vertexOutput) : SV_Target
{
    return half4(_Color);
}

            ENDCG
        }
    }
}
