Shader "Custom/TransparentSkyboxShader"
{
    Properties
    {
        _MainTex ("Cubemap", CUBE) = "black" {}
        _Tint ("Tint Color", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0,1)) = 1.0
        _Yaw("Yaw", Float) = 0.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Packages/com.miris.sdk.core/Resources/Shaders/MathUtils.hlsl"
            struct appdata
            {
                half4 vertex : POSITION;
                half3 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                half3 texcoord : TEXCOORD0;
                half4 vertex : SV_POSITION;
            };

            samplerCUBE _MainTex;
            half4 _Tint;
            half _Alpha;
            float _Yaw;
            
            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                float3x3 rotationMatrix = rotateAboutYAxis(_Yaw);
                
                o.texcoord = mul(rotationMatrix,v.vertex.xyz);
                
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 skyColor = texCUBE(_MainTex, i.texcoord);
                return half4(skyColor.rgb * _Tint.rgb, skyColor.a * _Alpha);
            }
            ENDHLSL
        }
    }
}
