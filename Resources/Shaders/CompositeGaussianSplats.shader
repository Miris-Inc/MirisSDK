// Copyright © 2024 Miris. All rights reserved.

Shader "Aqua/Composite Gaussian Splats"
{
    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma multi_compile __ DEBUG_TOTAL_OPACITY

            #include_with_pragmas "CompositeGaussianSplats.hlsl"
            
            ENDHLSL
        }
    }
}