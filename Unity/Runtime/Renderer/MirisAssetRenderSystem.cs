// Copyright © 2026 Miris, Inc. All rights reserved.

// Standard library

using System.Collections.Generic;

// Unity Engine
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

// Unity packages
using Unity.Profiling;
using UnityEngine.XR;

namespace Miris.Runtime
{
    // MirisAssetRenderSystem registers all the MirisAssetRenderComponent(s) in a scene
    // to render them as part of a single pass.
    //
    // Components may optionally have their render resources aggregated into one pool.
    //
    // When using Unity's Built-in render pipeline, MirisAssetRenderSystem will register
    // to the Camera callbacks as the means to submit graphics commands.
    //
    // When we add support for URP (Universal Render Pipeline), the URP feature / render pass
    // will access MirisAssetRenderSystem to submit the draw commands.
    public class MirisAssetRenderSystem
    {
        // Singleton instance
        public static MirisAssetRenderSystem m_instance = new MirisAssetRenderSystem();

        // Command buffer used to queue up all graphics commands for rendering Gaussian Splats.
        private CommandBuffer m_commandBuffer;

        readonly HashSet<MirisAssetRenderComponent> m_components = new();
        readonly HashSet<Camera> m_cameraHasCommandBuffer = new();
        readonly List<MirisAssetRenderComponent> m_activeComponents = new();

        // For rendering to a temporary render texture & compositing back onto main framebuffer.
        private Shader m_compositeShader;
        public Material m_compositeMaterial = null;

        // Profiler markers
        static string s_profilerPrefix = "[MirisAssetRenderSystem] ";
        static readonly ProfilerMarker s_gatherAndSortMarker = new ProfilerMarker(
            s_profilerPrefix + "Gather and sort objects"
        );
        public static readonly ProfilerMarker s_renderMarker = new ProfilerMarker(
            s_profilerPrefix + "Render"
        );
        public static readonly ProfilerMarker s_compositeMarker = new ProfilerMarker(
            s_profilerPrefix + "Composite"
        );
        
        // XR
        private XRUtils m_xrUtils = new XRUtils();
        private RenderTextureDescriptor m_xrTextureDescriptor;

        // Selects the single-pass instanced stereo path in both the splat draw shader and the
        // composite shader.
        private const string c_singlePassStereoKeyword = "MIRIS_SINGLE_PASS_STEREO";

        // Guards the unsupported-multiview warning so it is logged once, not once per frame.
        private bool m_hasWarnedUnsupportedMultiview = false;

        // For binding shaders parameters.
        private static class ShaderIds
        {
            public static readonly int MirisAssetRT = Shader.PropertyToID("_MirisAssetRT");

            public static readonly int TanTheta = Shader.PropertyToID("_TanTheta");
            public static readonly int AspectRatio = Shader.PropertyToID("_AspectRatio");
            public static readonly int ConstantSplatDistance = Shader.PropertyToID("_ConstantSplatDistance");
        }

        // Returns whether or not Unity's using the Built-in renderer pipeline
        private static bool UsingBuiltinRenderPipeline()
        {
            return GraphicsSettings.currentRenderPipeline == null;
        }

        public void RegisterRenderer(MirisAssetRenderComponent component)
        {
            Assert.IsFalse(m_components.Contains(component));

            // On initial registration & if we are using built-in renderer,
            if (m_components.Count == 0)
            {
                CreateSystemResources();
            }

            m_components.Add(component);
        }

        public void UnregisterRenderer(MirisAssetRenderComponent component)
        {
            if (!m_components.Contains(component))
            {
                return;
            }

            m_components.Remove(component);

            // If we're un-registering the last renderer
            if (m_components.Count == 0)
            {
                DestroySystemResources();
            }
        }

        // Set-up system resources when registering a splat renderer for the first time in RegisterRenderer.
        private void CreateSystemResources() {
            MirisDebug.Log("Creating Gaussian splat render system resources");

            if (m_compositeShader == null) {
                m_compositeShader = Resources.Load<Shader>("Shaders/CompositeMirisAssets");
            }
            MirisDebug.Log("Creating composite material.");

            m_compositeMaterial = new Material(m_compositeShader) { name = "MirisAssetsCompositeMaterial" };

#if USING_URP
            m_compositeMaterial.EnableKeyword("USING_URP");
#endif

            // The stereo mode and the eye texture descriptor are deliberately not captured
            // here. Platforms whose compositor configures asynchronously report a placeholder
            // mode and descriptor for the first frames, and CreateSystemResources runs inside
            // that window. RefreshXrRenderState re-reads both every frame instead.

            if (UsingBuiltinRenderPipeline()) {
                MirisDebug.Log("Installing Camera.onPreCull callback");
                Camera.onPreCull += OnPreCullCamera;
            }
        }

        // Called from UnregisterRenderer when all splat renderers have been unregistered,
        // for destroying system resources.
        private void DestroySystemResources()
        {
            MirisDebug.Log("Destroying Gaussian splat render system resources");

            // Un-register commandBuffer from camera(s)
            if (m_cameraHasCommandBuffer != null)
            {
                if (m_commandBuffer != null)
                {
                    foreach (Camera camera in m_cameraHasCommandBuffer)
                    {
                        if (camera != null)
                        {
                            MirisDebug.Log("Removing command buffer from camera " + camera.gameObject.name);
                            camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, m_commandBuffer);
                        }
                    }
                }
                m_cameraHasCommandBuffer.Clear();
            }

            m_activeComponents.Clear();

            // Delete command buffer.
            m_commandBuffer?.Dispose();
            m_commandBuffer = null;

            // Destroy material.
            GameObject.DestroyImmediate(m_compositeMaterial);

            if (UsingBuiltinRenderPipeline())
            {
                // Un-register camera callback.
                MirisDebug.Log("Uninstalling Camera.OnPreCull callback");
                Camera.onPreCull -= OnPreCullCamera;
            }
        }

        private CommandBuffer GetInitialCommandBuffer(Camera camera)
        {
            // Lazily construct command buffer
            m_commandBuffer ??= new CommandBuffer { name = "RenderMirisAssets" };

            // Register command buffer to camera if needed (only for built-in render pipeline)
            if (UsingBuiltinRenderPipeline() && camera != null && !m_cameraHasCommandBuffer.Contains(camera))
            {
                // The command buffer will be executed before the camera begins rendering transparent objects.
                MirisDebug.Log("Installing command buffer camera " + camera.gameObject.name);
                camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, m_commandBuffer);
                m_cameraHasCommandBuffer.Add(camera);
            }

            m_commandBuffer.Clear();
            return m_commandBuffer;
        }

        // This is our hook to queue up the graphics commands.
        private void OnPreCullCamera(Camera camera)
        {
            if (camera.cameraType == CameraType.SceneView || camera == Camera.main) // Ensure it's the main camera or we are the Editor's SceneView
            {
                // Enable a depth texture on the camera (if the platform or project has not already enabled it.)
                DepthTextureMode currentMode = camera.depthTextureMode;
                if (((currentMode & DepthTextureMode.Depth) == 0) &&
                    ((currentMode & DepthTextureMode.DepthNormals) == 0))
                    camera.depthTextureMode = DepthTextureMode.Depth;

                CommandBuffer commandBuffer = GetInitialCommandBuffer(camera);
                // send an callback on the render thread to the native plugin
                commandBuffer.IssuePluginEvent(MirisApi.GetRenderEventCallbackPtr(), 0);
                ProcessComponents(camera);
                Render(camera, commandBuffer);
            }
        }

        public void ProcessComponents(Camera camera)
        {
            using (s_gatherAndSortMarker.Auto())
            {
                // Figure out which renderers are actually in a good state to render.
                m_activeComponents.Clear();
                foreach (MirisAssetRenderComponent component in m_components)
                {
                    if (component.CanRender())
                    {
                        m_activeComponents.Add(component);
                    }
                }

                // Sort the active renderer(s) by distance to camera, ascending order (render the closest object first)
                Vector3 cameraPosition = camera.transform.position;
                m_activeComponents.Sort((a, b) =>
                {
                    // Get the centers of the object bounding box of each gaussian splat object.
                    Vector3 objectCenterA = a.GetObjectBounds().center;
                    Vector3 objectCenterB = b.GetObjectBounds().center;

                    // Transform the bound centers to worldspace.
                    Vector3 worldCenterA = a.m_transform.TransformPoint(objectCenterA);
                    Vector3 worldCenterB = b.m_transform.TransformPoint(objectCenterB);

                    // Compare distance from camera to object center in world space.
                    float distA = (cameraPosition - worldCenterA).sqrMagnitude;
                    float distB = (cameraPosition - worldCenterB).sqrMagnitude;
                    return distA.CompareTo(distB);
                });
            }
        }

        // Returns whether the platform has actually populated XRSettings.eyeTextureDesc.
        // eyeTextureDesc disagreeing with eyeTextureWidth/Height is the tell that it has not.
        private static bool HasValidXrTextureDescriptor()
        {
            RenderTextureDescriptor eyeDescriptor = XRSettings.eyeTextureDesc;

            return XRSettings.eyeTextureWidth > 0
                && XRSettings.eyeTextureHeight > 0
                && eyeDescriptor.width == XRSettings.eyeTextureWidth
                && eyeDescriptor.height == XRSettings.eyeTextureHeight;
        }

        // Re-reads the stereo rendering mode and eye texture descriptor for the current frame.
        //
        // These cannot be captured once. visionOS reports MultiPass with a 256x256 placeholder
        // descriptor while Compositor Services configures, then switches to SinglePassInstanced
        // with a Tex2DArray descriptor a few frames later. Anything captured inside that window
        // leaves the render target and the composite built for different stereo modes for the
        // rest of the session.
        private void RefreshXrRenderState(Camera camera)
        {
            // Instanced only, not IsSinglePassXR: the eye unpacking in the splat and composite
            // shaders assumes the doubled instance stream that single-pass instancing produces.
            // Multiview reports as single-pass but does not double it, so it must not take this
            // path -- doing so halves every splat index and derives the eye from splat parity.
            bool isSinglePassInstanced = m_xrUtils.IsSinglePassInstancedXR();

            // Set the keyword globally rather than on the composite material: the splat draw
            // shader needs the same variant and draws with its own materials.
            if (isSinglePassInstanced)
            {
                Shader.EnableKeyword(c_singlePassStereoKeyword);
            }
            else
            {
                Shader.DisableKeyword(c_singlePassStereoKeyword);
            }

            // The splat path has no multiview variant: the composite samples a texture array by
            // slice using an eye index recovered from the instance stream, which multiview does
            // not supply. Report it rather than rendering a wrong image in silence.
            if (!m_hasWarnedUnsupportedMultiview && m_xrUtils.IsSinglePassMultiviewXR())
            {
                m_hasWarnedUnsupportedMultiview = true;
                Debug.LogWarning("[MirisAssetRenderSystem] Single-pass multiview is not supported by " +
                    "the current render path. Use Multi-pass, or a graphics API whose Single Pass " +
                    "Instanced mode resolves to instancing rather than multiview.");
            }

            // Only the stereo paths size their render target from the descriptor.
            if (!m_xrUtils.IsStereo())
            {
                return;
            }

            bool hasValidDescriptor = HasValidXrTextureDescriptor();

            if (hasValidDescriptor)
            {
                m_xrTextureDescriptor = XRSettings.eyeTextureDesc;
                m_xrTextureDescriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            }
            else if (!m_xrUtils.IsSinglePassXR() && camera != null)
            {
                // Deliberately the broad single-pass check, not isSinglePassInstanced: multiview
                // needs the descriptor's Tex2DArray layout just as instancing does, so neither
                // single-pass mode can fall back to this camera-sized Tex2D. Only multi-pass can.
                m_xrTextureDescriptor = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight)
                {
                    graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
                    depthBufferBits = 0,
                    dimension = TextureDimension.Tex2D,
                    volumeDepth = 1,
                    msaaSamples = 1
                };
            }
        }

        public void Render(Camera camera, CommandBuffer commandBuffer) {
            RefreshXrRenderState(camera);

            if (UsingBuiltinRenderPipeline()) {
                RenderUsingBuiltinPipeline(camera, commandBuffer);
                return;
            }

            foreach (MirisAssetRenderComponent component in m_activeComponents) {
                component.Render(camera, commandBuffer);
            }
        }

        // Add graphics commands for active renderers.
        private void RenderUsingBuiltinPipeline(Camera camera, CommandBuffer commandBuffer)
        {
            // RefreshXrRenderState could not produce a usable eye texture descriptor for this
            // frame, which happens while a platform's compositor is still configuring. Skip the
            // pass rather than allocating a degenerate render target from it.
            if (m_xrUtils.IsStereo() &&
                (m_xrTextureDescriptor.width <= 0 || m_xrTextureDescriptor.height <= 0))
            {
                return;
            }

            using (s_renderMarker.Auto())
            {
                // Create a fully transparent temporary texture to render all our splats on-to, front to back.
                m_commandBuffer.BeginSample(s_renderMarker);

                // For XR applications, Unity recommends to use the XRSettings.EyeTextureDesc. This is a Texture Descriptor
                // providing the optimal settings for rendering to each eye in XR Applications. It includes texture dimension, color format,
                // depth buffer, msaa, etc. For example, in Multi-Pass mode, the texture dimension should be Tex2D. For Single-Pass/Multi-View
                // the texture dimension should be Tex2DARRAY. The XRSettings.EyeTextureDesc sets the right configuration for each mode automatically. 
                if (m_xrUtils.IsStereo()) 
                {
                    m_commandBuffer.GetTemporaryRT(ShaderIds.MirisAssetRT, m_xrTextureDescriptor, FilterMode.Point);
                }
                else 
                {
                    m_commandBuffer.GetTemporaryRT(ShaderIds.MirisAssetRT, -1, -1, 0, FilterMode.Point, GraphicsFormat.R8G8B8A8_UNorm);
                }

                if (m_xrUtils.IsSinglePassXR()) 
                { 
                    m_commandBuffer.SetRenderTarget(ShaderIds.MirisAssetRT, BuiltinRenderTextureType.Depth, 0,CubemapFace.Unknown,-1);    
                }
                else 
                {
                    m_commandBuffer.SetRenderTarget(ShaderIds.MirisAssetRT, BuiltinRenderTextureType.Depth);
                }
                
                m_commandBuffer.ClearRenderTarget(RTClearFlags.Color, new Color(0, 0, 0, 0), 0);

                // Render non-aggregated components
                foreach (MirisAssetRenderComponent component in m_activeComponents)
                {
                    component.Render(camera, commandBuffer);
                    component.UpdateCompositePass(m_compositeMaterial);
                }
                m_commandBuffer.EndSample(s_renderMarker);
            }

            using (s_compositeMarker.Auto())
            {
                // Composite the temporary texture onto main frame buffer.
                m_commandBuffer.BeginSample(s_compositeMarker);

                if (m_xrUtils.IsSinglePassXR()) 
                {
                    RenderTargetIdentifier xrTarget = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget, 0, CubemapFace.Unknown, -1);
                    m_commandBuffer.SetRenderTarget(xrTarget);
                }
                else 
                {
                    m_commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                }
                
                // One instance, even for single-pass instanced stereo: Unity supplies the
                // second instance for the second eye.
                m_commandBuffer.DrawProcedural(Matrix4x4.identity, m_compositeMaterial, 0, MeshTopology.Triangles, 6, 1);
                m_commandBuffer.ReleaseTemporaryRT(ShaderIds.MirisAssetRT);
                m_commandBuffer.EndSample(s_compositeMarker);
            }
        }
    }
}
