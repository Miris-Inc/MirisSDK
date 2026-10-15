// Copyright © 2026 Miris, Inc. All rights reserved.

Shader "Miris/Color Grade"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _MirisLutTex ("Colour grading LUT", 2D) = "white" {}
        _MirisLutParams ("LUT parameters (1/w, 1/h, size-1, unused)", Vector) = (0, 0, 0, 0)
        _MirisLutStrength ("LUT strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ MIRIS_COLOR_GRADING_LUT
            // Single-pass instanced is how visionOS renders both eyes, and it is what makes
            // UNITY_DECLARE_SCREENSPACE_TEXTURE resolve to an array. Without this the pass samples
            // the non-stereo variant and grades one eye with the other eye's image.
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "UnityCG.cginc"

            #define MIRIS_LUT_COMBINED_SAMPLER
            #include "MirisColorGrading.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float4 source = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.uv);

                float4 graded = MirisApplyColorGrade(source, MIRIS_GRADE_SPACE_ACTIVE, MIRIS_GRADE_ALPHA_STRAIGHT);

                return fixed4(graded.rgb, source.a);
            }
            ENDCG
        }
    }
}
