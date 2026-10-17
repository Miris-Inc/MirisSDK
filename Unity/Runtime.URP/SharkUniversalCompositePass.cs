// Copyright © 2026 Miris, Inc. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Miris.Runtime
{
    // Draws SharkCameraViews' per-camera composite under URP, which never runs the camera
    // CommandBuffer the built-in pipeline composites with. Enqueued onto each camera's renderer as
    // it begins rendering, so a project needs no renderer feature to show splats.
    //
    // Only the per-camera path needs this: the frame path composites with a MeshRenderer, which URP
    // draws in its transparent pass like any other.
    class SharkUniversalCompositePass : ScriptableRenderPass
    {
        static readonly SharkUniversalCompositePass s_pass = new();

        class PassData
        {
            internal Mesh m_mesh;
            internal Material m_material;
            internal MaterialPropertyBlock m_properties;
        }

        SharkUniversalCompositePass()
        {
            // Where the built-in path composites (AfterForwardAlpha): over transparents, under
            // post-processing.
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Register()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!SharkSplatRenderSystem.m_instance.TryGetCameraComposite(camera, out _, out _, out _))
            {
                return;
            }

            camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(s_pass);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!SharkSplatRenderSystem.m_instance.TryGetCameraComposite(
                    cameraData.camera, out Mesh mesh, out Material material, out MaterialPropertyBlock properties))
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            using IRasterRenderGraphBuilder builder =
                renderGraph.AddRasterRenderPass("Shark composite", out PassData passData);
            passData.m_mesh = mesh;
            passData.m_material = material;
            passData.m_properties = properties;

            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                context.cmd.DrawMesh(data.m_mesh, Matrix4x4.identity, data.m_material, 0, 0, data.m_properties));
        }
    }
}
