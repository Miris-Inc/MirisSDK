// Copyright © 2026 Miris, Inc. All rights reserved.
//
// Draws each eye's Shark surface straight over the eye buffer, one texel per pixel.
//
// A fullscreen blit expressed as a mesh draw, rather than CommandBuffer.Blit: Blit's behaviour
// under single-pass instanced in the built-in pipeline is unreliable, and DrawProcedural would
// mean writing an XR fullscreen pass from scratch. A MeshRenderer reliably reaches both eyes.
//
// The mesh's positions ARE clip space: no UnityObjectToClipPos, no view or projection, no
// dependence on the transform. That is what makes this exactly 1:1 - each eye's surface was
// rendered with that eye's own frustum, so mapping it to that eye's full viewport is the identity.
// A world-space quad cannot do this: sized from the mono projection, which is neither eye's, it
// leaves the image both under-sampled and partly off-screen.

Shader "Miris/SplatComposite"
{
    Properties
    {
        _LeftTex ("Left eye surface", 2D) = "black" {}
        _RightTex ("Right eye surface", 2D) = "black" {}
        // 1 flips V when sampling, 0 does not. A toggle rather than a constant because the correct
        // value is a fact about Metal, Dawn and Unity's texture conventions stacked on each other,
        // and can only be established on device. See SplatRenderer.m_compositeVFlip.
        _VFlip ("Flip V", Float) = 1
    }

    SubShader
    {
        // Overlay, and depth off in both directions: this is the last thing drawn and it covers
        // everything. ZTest Always because the clip-space z below is arbitrary - the composite
        // carries no meaningful depth, so anything testing against it tests a made-up number.
        Tags { "RenderType" = "Overlay" "Queue" = "Overlay" }
        Cull Off
        ZWrite Off
        ZTest Always

        // PREMULTIPLIED, not the usual SrcAlpha/OneMinusSrcAlpha. Shark blends with colour
        // SrcAlpha/OneMinusSrcAlpha and alpha One/OneMinusSrcAlpha (RendererWGPU.cpp) over a
        // transparent-black clear, so what lands in the surface is already colour times alpha.
        // Multiplying by alpha again here would darken every splat in proportion to its own
        // transparency - subtle enough to read as a lighting or tone-mapping problem rather
        // than as a blend-mode one.
        //
        // It also has to preserve destination alpha for passthrough: in a mixed immersive space
        // the compositor reads the eye buffer's alpha, so a blend that flattened it to 1 would
        // turn passthrough opaque.
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

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
                // Single-pass instanced is how visionOS renders both eyes
                // (XRSettings.stereoRenderingMode reports SinglePassInstanced). Without this the
                // composite reaches one eye or neither, indistinguishable from empty surfaces.
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _LeftTex;
            sampler2D _RightTex;
            float _VFlip;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Straight to clip space. The mesh is authored at +/-1 in XY - see
                // SplatRenderer.BuildCompositeMesh - so this covers the viewport exactly.
                //
                // _ProjectionParams.x is REQUIRED here and easy to miss. Rendering into a texture
                // on a top-left-origin API - Metal, which is every target this path runs on - Unity
                // negates the projection's Y so the texture lands upright. Geometry drawn through
                // UnityObjectToClipPos gets that for free; this shader bypasses the projection, so
                // it must apply the same correction by hand. Without it the composite is vertically
                // mirrored, which inverts vertical parallax and reads as broken tracking rather
                // than as a flipped blit.
                o.pos = float4(v.vertex.x, v.vertex.y * _ProjectionParams.x, 0.0, 1.0);

                // A separate flip, for a separate reason, which is why it is not folded into the
                // one above: Shark rasterizes through WebGPU, whose framebuffer row 0 is the TOP
                // of the image and which Metal stores that way too, while Unity's own texture
                // convention puts row 0 at the BOTTOM.
                //
                // Not folded into unityProjectionToShark either: the flip is a property of how
                // Unity samples an externally-written Metal texture, not of the camera. Negating
                // the projection's Y row would drag fy, fov and cy negative and corrupt the
                // covariance and culling inputs derived from them.
                o.uv = float2(v.uv.x, lerp(v.uv.y, 1.0 - v.uv.y, _VFlip));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Required before reading unity_StereoEyeIndex in a fragment shader - the vertex
                // stage's instance id does not survive to here on its own.
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return unity_StereoEyeIndex == 0 ? tex2D(_LeftTex, i.uv) : tex2D(_RightTex, i.uv);
            }
            ENDCG
        }
    }
}
