Shader "Custom/WireframeGrid"
{
    Properties
    {
        _Color ("Line Color", Color) = (1,1,1,1)
        _GridSize ("Grid Size", Float) = 1
        _LineWidth ("Line Width", Float) = 0.05
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t 
            {
                float4 vertex : POSITION;
            };

            struct v2f 
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _GridSize;
            float _LineWidth;
            float4 _Color;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.vertex.xz / _GridSize; 
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 grid = frac(i.uv);
                float lineRatio = min(abs(grid.x - 0.5), abs(grid.y - 0.5));
                float alpha = smoothstep(_LineWidth * 2, _LineWidth, lineRatio); 
                return float4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}