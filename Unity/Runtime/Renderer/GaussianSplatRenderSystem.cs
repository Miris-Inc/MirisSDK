// Copyright © 2025 Miris, Inc. All rights reserved.

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
    // GaussianSplatRenderSystem registers all the GaussianSplatRenderComponent(s) in a scene
    // to render them as part of a single pass.
    //
    // Components may optionally have their render resources aggregated into one pool.
    //
    // When using Unity's Built-in render pipeline, GaussianSplatRenderSystem will register
    // to the Camera callbacks as the means to submit graphics commands.
    //
    // When we add support for URP (Universal Render Pipeline), the URP feature / render pass
    // will access GaussianSplatRenderSystem to submit the draw commands.
    public class GaussianSplatRenderSystem
    {
        // Singleton instance
        public static GaussianSplatRenderSystem m_instance = new GaussianSplatRenderSystem();

        // Command buffer used to queue up all graphics commands for rendering Gaussian Splats.
        private CommandBuffer m_commandBuffer;

        readonly HashSet<GaussianSplatRenderComponent> m_components = new();
        readonly HashSet<Camera> m_cameraHasCommandBuffer = new();
        readonly List<GaussianSplatRenderComponent> m_activeComponents = new();

        // For rendering to a temporary render texture & compositing back onto main framebuffer.
        private Shader m_compositeShader;
        public Material m_compositeMaterial = null;

        // Profiler markers
        static string s_profilerPrefix = "[GaussianSplatRenderSystem] ";
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

        // For binding shaders parameters.
        private static class ShaderIds
        {
            public static readonly int GaussianSplatRT = Shader.PropertyToID("_GaussianSplatRT");

            public static readonly int TanTheta = Shader.PropertyToID("_TanTheta");
            public static readonly int AspectRatio = Shader.PropertyToID("_AspectRatio");
            public static readonly int ConstantSplatDistance = Shader.PropertyToID("_ConstantSplatDistance");
        }

        // Returns whether or not Unity's using the Built-in renderer pipeline
        private static bool UsingBuiltinRenderPipeline()
        {
            return GraphicsSettings.currentRenderPipeline == null;
        }

        public void RegisterRenderer(GaussianSplatRenderComponent component)
        {
            Assert.IsFalse(m_components.Contains(component));

            // On initial registration & if we are using built-in renderer,
            if (m_components.Count == 0)
            {
                CreateSystemResources();
            }

            m_components.Add(component);
        }

        public void UnregisterRenderer(GaussianSplatRenderComponent component)
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
                m_compositeShader = Resources.Load<Shader>("Shaders/CompositeGaussianSplats");
            }
            MirisDebug.Log("Creating composite material.");

            m_compositeMaterial = new Material(m_compositeShader) { name = "GaussianSplatsCompositeMaterial" };

#if USING_URP
            m_compositeMaterial.EnableKeyword("USING_URP");
#endif

            if (m_compositeMaterial != null && m_xrUtils.IsSinglePassXR()) {
                m_compositeMaterial.EnableKeyword("STEREO_MULTIVIEW_ON");
            }

            // Set XR texture descriptor
            m_xrTextureDescriptor = XRSettings.eyeTextureDesc;
            m_xrTextureDescriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;

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
            m_commandBuffer ??= new CommandBuffer { name = "RenderGaussianSplats" };

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
                foreach (GaussianSplatRenderComponent component in m_components)
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

        public void Render(Camera camera, CommandBuffer commandBuffer) {
            if (UsingBuiltinRenderPipeline()) {
                RenderUsingBuiltinPipeline(camera, commandBuffer);
                return;
            }

            foreach (GaussianSplatRenderComponent component in m_activeComponents) {
                component.Render(camera, commandBuffer);
            }
        }

        // Add graphics commands for active renderers.
        private void RenderUsingBuiltinPipeline(Camera camera, CommandBuffer commandBuffer)
        {
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
                    m_commandBuffer.GetTemporaryRT(ShaderIds.GaussianSplatRT, m_xrTextureDescriptor, FilterMode.Point);
                }
                else 
                {
                    m_commandBuffer.GetTemporaryRT(ShaderIds.GaussianSplatRT, -1, -1, 0, FilterMode.Point, GraphicsFormat.R8G8B8A8_UNorm);
                }

                if (m_xrUtils.IsSinglePassXR()) 
                { 
                    m_commandBuffer.SetRenderTarget(ShaderIds.GaussianSplatRT, BuiltinRenderTextureType.Depth, 0,CubemapFace.Unknown,-1);    
                }
                else 
                {
                    m_commandBuffer.SetRenderTarget(ShaderIds.GaussianSplatRT, BuiltinRenderTextureType.Depth);
                }
                
                m_commandBuffer.ClearRenderTarget(RTClearFlags.Color, new Color(0, 0, 0, 0), 0);

                // Render non-aggregated components
                foreach (GaussianSplatRenderComponent component in m_activeComponents)
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
                
                m_commandBuffer.DrawProcedural(Matrix4x4.identity, m_compositeMaterial, 0, MeshTopology.Triangles, 6, m_xrUtils.IsSinglePassXR()?2:1);
                m_commandBuffer.ReleaseTemporaryRT(ShaderIds.GaussianSplatRT);
                m_commandBuffer.EndSample(s_compositeMarker);
            }
        }
    }
}
