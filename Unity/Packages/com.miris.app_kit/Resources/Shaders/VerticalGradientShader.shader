Shader "Custom/VerticalGradientShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _FadeStart ("Fade Start", Float) = 0
        _FadeEnd ("Fade End", Float) = 1
        _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionIntensity ("Emission Intensity", Float) = 1
        _BackfaceAlpha ("Backface Alpha", Range (0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass 
        {
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off 
            Lighting Off
            CGPROGRAM
            #pragma vertex vert 
            #pragma fragment frag 
            #include "UnityCG.cginc"

            struct vert_t
            {
                float4 vertex : POSITION;
                float3 normal: NORMAL;
            };

            struct v2f 
            {
                float4 pos : SV_POSITION;
                float fade : TEXCOORD0;
                float facing : TEXCOORD1;
            };

            float4 _Color;
            float _FadeStart;
            float _FadeEnd;
            float4 _EmissionColor;
            float _EmissionIntensity;
            float _BackfaceAlpha;

            v2f vert(vert_t v){
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                float height = v.vertex.y;
                o.fade = saturate((height - _FadeStart) / (_FadeEnd - _FadeStart));
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                o.facing = viewNormal.z < 0 ? 1.0 : 0.0;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float alpha = 1 - i.fade;
                if ( i.facing > 0.5){
                    alpha *= _BackfaceAlpha;
                }


                float3 baseColor = _Color.rgb * alpha;
                float3 emission = _EmissionColor.rgb * (1 - i.fade) * _EmissionIntensity;
                return fixed4(baseColor + emission, alpha * _EmissionColor.a);
            }
            ENDCG

        }
    }
    FallBack "Diffuse"
}
