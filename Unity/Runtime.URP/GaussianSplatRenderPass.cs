// Copyright © 2026 Miris, Inc. All rights reserved.

#if GS_ENABLE_URP
#if !UNITY_6000_0_OR_NEWER
#error Unity Gaussian Splatting URP support only works in Unity 6 or later
#endif
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.XR;

namespace Miris.Runtime
{
    
    class GaussianSplatRenderPass : ScriptableRendererFeature
    {
        GSRenderPass m_pass;
        bool m_hasCamera;
        static XRUtils m_xrUtils = new XRUtils();
        
        class GSRenderPass : ScriptableRenderPass
        {
            const string m_gaussianSplatRTName = "_GaussianSplatRT";

            const string m_profilerTag = "GaussianSplatRenderGraph";
            static readonly ProfilingSampler s_profilingSampler = new(m_profilerTag);
            static readonly int s_gaussianSplatRT = Shader.PropertyToID(m_gaussianSplatRTName);

            private class PassData
            {
                internal UniversalCameraData m_cameraData;
                internal TextureHandle m_sourceTexture;
                internal TextureHandle m_sourceDepth;
                internal TextureHandle m_gaussianSplatRT;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType != CameraType.SceneView && cameraData.camera != Camera.main) {
                    return;
                }
                using var builder = renderGraph.AddUnsafePass(m_profilerTag, out PassData passData);

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                cameraData.camera.allowHDR = false;

                RenderTextureDescriptor rtDesc =XRSettings.enabled ? XRSettings.eyeTextureDesc:cameraData.cameraTargetDescriptor;
                
                rtDesc.depthBufferBits = 0;
                rtDesc.msaaSamples = 1;
                rtDesc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;

                TextureHandle gaussianSplatTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, rtDesc, m_gaussianSplatRTName, true);

                passData.m_cameraData = cameraData;
                passData.m_sourceTexture = resourceData.activeColorTexture;
                passData.m_sourceDepth = resourceData.activeDepthTexture;
                passData.m_gaussianSplatRT = gaussianSplatTexture;

                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
                builder.UseTexture(resourceData.activeDepthTexture);

                builder.UseTexture(gaussianSplatTexture, AccessFlags.Write);

                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }

            static void ExecutePass(PassData data, UnsafeGraphContext context) {                
                var commandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                // store the current multipass id
                if (m_xrUtils.IsMultiPassXR()) {
                    XRFrameInfo.m_multipassId = data.m_cameraData.xr.multipassId;
                }

                using var _ = new ProfilingScope(commandBuffer, s_profilingSampler);
                commandBuffer.SetGlobalTexture(s_gaussianSplatRT, data.m_gaussianSplatRT);
                
                CoreUtils.SetRenderTarget(commandBuffer, data.m_gaussianSplatRT, data.m_sourceDepth);

                using (GaussianSplatRenderSystem.s_renderMarker.Auto()) {
                    commandBuffer.BeginSample(GaussianSplatRenderSystem.s_renderMarker);
                    GaussianSplatRenderSystem.m_instance.Render(data.m_cameraData.camera, commandBuffer);   
                    commandBuffer.EndSample(GaussianSplatRenderSystem.s_renderMarker); 
                }

                if (m_xrUtils.IsSinglePassXR()) {
                    RenderTargetIdentifier xrTarget = new RenderTargetIdentifier(data.m_sourceTexture, 0, CubemapFace.Unknown, -1);
                    CoreUtils.SetRenderTarget(commandBuffer, xrTarget);
                }
                else 
                {
                    CoreUtils.SetRenderTarget(commandBuffer, data.m_sourceTexture);
                }
                
                Material matComposite = GaussianSplatRenderSystem.m_instance.m_compositeMaterial;
                
                if (matComposite != null) {
                    matComposite.renderQueue = (int)RenderQueue.Transparent + 1;

                    using (GaussianSplatRenderSystem.s_compositeMarker.Auto()) {
                        commandBuffer.BeginSample(GaussianSplatRenderSystem.s_compositeMarker);
                        commandBuffer.DrawProcedural(Matrix4x4.identity, matComposite, 0, MeshTopology.Triangles, 6, m_xrUtils.IsSinglePassXR()?2:1);                        
                        commandBuffer.EndSample(GaussianSplatRenderSystem.s_compositeMarker); 
                    } 
                }
            }            
        }

        public override void Create()
        {
            m_pass = new GSRenderPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };
        }

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData)
        {
            
            m_hasCamera = false;
            var system = GaussianSplatRenderSystem.m_instance;
            system.ProcessComponents(cameraData.camera);
                
            m_hasCamera = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!m_hasCamera)
                return;
            renderer.EnqueuePass(m_pass);
        }

        protected override void Dispose(bool disposing)
        {
            m_pass = null;
        }
    }
}

#endif 
