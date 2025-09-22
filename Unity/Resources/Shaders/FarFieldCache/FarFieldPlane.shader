Shader "Aqua/Far Field Plane" {
SubShader {
    Tags { "RenderType"="Opaque" "Queue"="Transparent" }
    Blend OneMinusDstAlpha One
    // Turn off zclip - the far field plane will not be clipped to the camera's view frustum. This prevents flickering 
    // artifacts when the far field plane is at the camera's near or far plane
    ZClip False

    // We want the far field plane to always render, regardless of the depth or other objects in the scene. This is 
    // because the far field plane doesn't really represent a physical object in the scene - it's a way to display the 
    // far field. We can use the ZTest when rendering splats in the far field, but when we render the far field plane 
    // we should essentially just copy the far field image to the eyes - with all the corrections that Unity makes for
    // stereo rendering, of course
    ZTest Always
    ZWrite Off
    
    Pass {
        HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;

            StructuredBuffer<float> _SortedSplatDepth;
            
            float _TanTheta;
            float _AspectRatio;
            uint _FirstSplat;
            float _ConstantSplatDistance;

            // Half the height of the plane unity creates from the GameObject -> 3D Object -> Plane menu item
            static const float _UnityPlaneSize = 5.0f;
            static const float _ReciprocalUnityPlaneSize = 1.f / _UnityPlaneSize;

            v2f vert(appdata_t v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Negate the distance to match our viewspace convention
                float splatDepth = -_ConstantSplatDistance;

                // A value of -1 indicates that we should use the dynamic distance
                if(_ConstantSplatDistance < 0) {
                    splatDepth = _SortedSplatDepth[_FirstSplat];
                }
                
                // Calculate the size of the plane based on the camera's aspect ratio and splat depth
                const float farFieldPlaneHeight = splatDepth * _TanTheta;

                const float2 planeSize = float2(farFieldPlaneHeight * _AspectRatio, farFieldPlaneHeight);

                // Divide planeSize by 5 to account for the size of the Unity plane
                v.vertex.xz *= planeSize * _ReciprocalUnityPlaneSize;
                v.vertex.y = splatDepth;
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 col = _MainTex.Sample(sampler_MainTex, i.texcoord);
                col.rgb = GammaToLinearSpace(col.rgb) * col.a;
                return col;
            }
        ENDHLSL
    }
}
}
