// Copyright © 2024 Miris. All rights reserved.

Shader "Miris/Render Gaussian Splats" {
    SubShader {
        Tags {"RenderType" = "Transparent" "Queue" = "Transparent"}
        Pass {
            Name "Beauty"
            ZWrite Off
            ZTest LEqual
            Blend OneMinusDstAlpha One
            Cull Off

            HLSLPROGRAM

            #define DECLARE_AOV
            #define SAMPLE_AOV splatView.color
            #define SAMPLE_ALPHA CalculateGaussianAlpha(vertexOutput)
            #include_with_pragmas "RenderMirisAssets.hlsl"

            ENDHLSL
        }

        Pass {
            Name "Opaque"
            ZWrite On
            ZTest Always
            Blend OneMinusDstAlpha One
            Cull Off

            HLSLPROGRAM

            #define DECLARE_AOV
            #define SAMPLE_AOV splatView.color
            #define SAMPLE_ALPHA 1.0 - vertexOutput.splatFade
            #include_with_pragmas "RenderMirisAssets.hlsl"

            ENDHLSL
        }

        Pass {
            Name "ObjectID"
            ZWrite Off
            ZTest LEqual
            Blend OneMinusDstAlpha One
            Cull Off

            HLSLPROGRAM

            #define DECLARE_AOV float4 _ObjectId;
            #define SAMPLE_AOV _ObjectId
            #define SAMPLE_ALPHA CalculateGaussianAlpha(vertexOutput)
            #include_with_pragmas "RenderMirisAssets.hlsl"

            ENDHLSL
        }

        Pass {
            Name "LodHeatMap"
            ZWrite Off
            ZTest LEqual
            Blend OneMinusDstAlpha One
            Cull Off

            HLSLPROGRAM

            #define DECLARE_AOV StructuredBuffer<uint> _SortedSplatIndex;
            #define SAMPLE_AOV GetDataSourceLodIndexColor(_SortedSplatIndex[instanceId])
            #define SAMPLE_ALPHA CalculateGaussianAlpha(vertexOutput)
            #include_with_pragmas "RenderMirisAssets.hlsl"

            ENDHLSL
        }

        Pass {
            Name "Highlight"
            ZWrite Off
            ZTest LEqual
            Blend OneMinusDstAlpha One
            Cull Off

            HLSLPROGRAM

            #define DECLARE_AOV
            #define SAMPLE_AOV lerp(splatView.color,float4(1.0,1.0,0.6,0.0),0.2)
            #define SAMPLE_ALPHA CalculateGaussianAlpha(vertexOutput)
            #include_with_pragmas "RenderMirisAssets.hlsl"

            ENDHLSL
        }
    }
}
