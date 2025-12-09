Shader "Miris/SwipeLoadingShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.8, 0.8, 0.8, 1)
        _HighlightColor ("Highlight Color", Color) = (1,1,1,1)
        _SwipeWidth ("Swipe Width", Range(0.1, 1.0)) = 0.3
        _Speed ("Swipe Speed", Range(0.1, 5.0)) = 1.0
        _Angle ("Swipe Angle", Range(0, 360)) = 0
    }
    SubShader
    {
        Tags {"RenderType" = "Transparent" "Queue" = "Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            fixed4 _BaseColor;
            fixed4 _HighlightColor;
            float _SwipeWidth;
            float _Speed;
            float _Angle;

            float _TimeY;

            VertexOutput vert(VertexInput vertexInput)
            {
                VertexOutput vertexOutput;
                vertexOutput.vertex = UnityObjectToClipPos(vertexInput.vertex);
                vertexOutput.uv = vertexInput.uv;
                return vertexOutput;
            }

            fixed4 frag(VertexOutput vertexOutput) : SV_Target
            {
                float angleRadians = radians(_Angle);
                float2 dir = float2(cos(angleRadians), sin(angleRadians));
                float uvProj = dot(vertexOutput.uv, dir);

                // Loop around 0-2.0 as a function of time & speed
                float swipeCenter = fmod(_Time.y * _Speed, 2.0);
                swipeCenter = smoothstep(0, 4.0, swipeCenter);

                // Distance from current fragment to swipe center
                float dist = abs(uvProj - swipeCenter * 4);

                // Symmetrical smooth gradient falloff
                float highlight = exp(-pow(dist / _SwipeWidth, 2));

                // Final color blend
                return lerp(_BaseColor, _HighlightColor, highlight);
            }
            ENDCG
        }
    }
}