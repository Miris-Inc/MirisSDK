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
    
    class MirisAssetRenderPass : ScriptableRendererFeature
    {
        GSRenderPass m_pass;
        ColorGradePass m_gradePass;
        bool m_hasCamera;
        static XRUtils m_xrUtils = new XRUtils();
        
        class GSRenderPass : ScriptableRenderPass
        {
            const string m_mirisAssetRTName = "_MirisAssetRT";

            const string m_profilerTag = "MirisAssetRenderGraph";
            static readonly ProfilingSampler s_profilingSampler = new(m_profilerTag);
            static readonly int s_mirisAssetRT = Shader.PropertyToID(m_mirisAssetRTName);

            private class PassData
            {
                internal UniversalCameraData m_cameraData;
                internal TextureHandle m_sourceTexture;
                internal TextureHandle m_sourceDepth;
                internal TextureHandle m_mirisAssetRT;
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
                rtDesc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;

                TextureHandle mirisAssetTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, rtDesc, m_mirisAssetRTName, true);

                passData.m_cameraData = cameraData;
                passData.m_sourceTexture = resourceData.activeColorTexture;
                passData.m_sourceDepth = resourceData.activeDepthTexture;
                passData.m_mirisAssetRT = mirisAssetTexture;

                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
                builder.UseTexture(resourceData.activeDepthTexture);

                builder.UseTexture(mirisAssetTexture, AccessFlags.Write);

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
                commandBuffer.SetGlobalTexture(s_mirisAssetRT, data.m_mirisAssetRT);
                
                CoreUtils.SetRenderTarget(commandBuffer, data.m_mirisAssetRT, data.m_sourceDepth);

                using (MirisAssetRenderSystem.s_renderMarker.Auto()) {
                    commandBuffer.BeginSample(MirisAssetRenderSystem.s_renderMarker);
                    MirisAssetRenderSystem.m_instance.Render(data.m_cameraData.camera, commandBuffer);   
                    commandBuffer.EndSample(MirisAssetRenderSystem.s_renderMarker); 
                }

                if (m_xrUtils.IsSinglePassXR()) {
                    RenderTargetIdentifier xrTarget = new RenderTargetIdentifier(data.m_sourceTexture, 0, CubemapFace.Unknown, -1);
                    CoreUtils.SetRenderTarget(commandBuffer, xrTarget);
                }
                else 
                {
                    CoreUtils.SetRenderTarget(commandBuffer, data.m_sourceTexture);
                }
                
                Material matComposite = MirisAssetRenderSystem.m_instance.m_compositeMaterial;
                
                if (matComposite != null) {
                    matComposite.renderQueue = (int)RenderQueue.Transparent + 1;

                    using (MirisAssetRenderSystem.s_compositeMarker.Auto()) {
                        commandBuffer.BeginSample(MirisAssetRenderSystem.s_compositeMarker);
                        // One instance, even for single-pass instanced stereo: Unity supplies the
                        // second instance for the second eye.
                        commandBuffer.DrawProcedural(Matrix4x4.identity, matComposite, 0, MeshTopology.Triangles, 6, 1);                        
                        commandBuffer.EndSample(MirisAssetRenderSystem.s_compositeMarker); 
                    } 
                }
            }            
        }

        // The scene-wide colour grade. A separate pass which grades all rendered content
        // after Unity's own post-processing rather than before transparents.
        class ColorGradePass : ScriptableRenderPass
        {
            const string m_gradeSourceName = "_MirisGradeSource";
            const string m_profilerTag = "MirisColorGrade";
            static readonly ProfilingSampler s_profilingSampler = new(m_profilerTag);

            Material m_material;
            bool m_hasWarnedMissingShader;

            public bool HasMaterial => m_material != null;

            public void EnsureMaterial()
            {
                if (m_material != null)
                {
                    return;
                }

                Shader shader = Resources.Load<Shader>(MirisColorGradeState.c_shaderResourcePath);
                if (shader == null)
                {
                    // AddRenderPasses retries every frame per camera while a LUT is active, so the
                    // failure has to be reported once rather than once per frame.
                    if (!m_hasWarnedMissingShader)
                    {
                        m_hasWarnedMissingShader = true;
                        Debug.LogError($"[MirisAssetRenderPass] '{MirisColorGradeState.c_shaderResourcePath}' is "
                                       + "missing from the package's Resources - colour grading will not be applied.");
                    }
                    return;
                }

                m_material = new Material(shader)
                {
                    name = "MirisColorGradeMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            public void Cleanup()
            {
                CoreUtils.Destroy(m_material);
                m_material = null;
                m_hasWarnedMissingShader = false;
            }

            private class PassData
            {
                internal TextureHandle m_source;
                internal TextureHandle m_target;
                internal Material m_material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType != CameraType.SceneView && cameraData.camera != Camera.main)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                TextureHandle source = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, descriptor, m_gradeSourceName, false);

                using var builder = renderGraph.AddUnsafePass(m_profilerTag, out PassData passData);

                passData.m_source = source;
                passData.m_target = resourceData.activeColorTexture;
                passData.m_material = m_material;

                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }

            static void ExecutePass(PassData data, UnsafeGraphContext context)
            {
                var commandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                using var _ = new ProfilingScope(commandBuffer, s_profilingSampler);

                MirisColorGradeState.ApplyTo(data.m_material);

                commandBuffer.Blit(data.m_target, data.m_source);
                commandBuffer.Blit(data.m_source, data.m_target, data.m_material);
            }
        }

        public override void Create()
        {
            m_pass = new GSRenderPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };

            m_gradePass = new ColorGradePass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData)
        {
            
            m_hasCamera = false;
            var system = MirisAssetRenderSystem.m_instance;
            system.ProcessComponents(cameraData.camera);
                
            m_hasCamera = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!m_hasCamera)
                return;
            renderer.EnqueuePass(m_pass);

            if (MirisColorGradeState.IsActive)
            {
                m_gradePass.EnsureMaterial();
                if (m_gradePass.HasMaterial)
                {
                    renderer.EnqueuePass(m_gradePass);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            m_pass = null;
            m_gradePass?.Cleanup();
            m_gradePass = null;
        }
    }
}

#endif 
