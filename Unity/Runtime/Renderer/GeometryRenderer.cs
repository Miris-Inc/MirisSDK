// Copyright © 2024 Miris. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Mathematics;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;


namespace Aqua.Runtime
{
    /// <summary>
    /// GeometryRenderer performs rendering of gaussian splats through the creation of geometry quads.
    ///
    /// The splats are rendered via the following phases:
    /// 1. Splats are sorted by their distance from the camera in descending order
    ///   (e.g. the farthest splat is the first element).
    /// 2. Data is pre-computed for each visible splat. This data is used by the downstream vert and frag shaders. 
    /// 3. Splats are drawn via CommandBuffer.DrawProcedural by instancing N (where N is the number of splats)
    ///    quads which are transformed matching the shape of ellipsoids via a vertex shader, then
    ///    shaded via the gaussian distribution in the fragment shader.
    ///    See https://towardsdatascience.com/a-comprehensive-overview-of-gaussian-splatting-e7d570081362
    ///    for a precise breakdown of the rendering technique.
    ///
    /// TODO:
    /// 1. We are currently computing the per-splat view-dependent geometry in the vertex shader which
    ///    is wasteful there are 4 vertices per splat instance, thus we perform 3 redundant calculations.
    ///    This can be moved into a compute shader
    ///
    /// </summary>

    public class GeometryRenderer : GaussianSplatRenderer
    {
        // Shader pass IDs (Must match RenderGaussianSplats.shader)
        public enum ShaderPassId : int
        {
            Beauty = 0,
            Opaque,
            ObjectId,
            LodHeatMap,
            Highlight
        }

        public enum GeometryDrawMode : int
        {
            Splats,
            OpaqueSplats,
            SplatsWithBoundingBox,
            SplatsWithBoundingLocator,
            BoundingBoxOnly,
            BoundingLocatorOnly,
            ObjectId,
            LodHeatMap,
            SHOnly,
            TotalOpacity,
            Highlight
        }

        public GeometryDrawMode m_geometryDrawMode = GeometryDrawMode.Splats;

        // ---------------------------------------------------------
        // Shaders & compute shaders
        // ---------------------------------------------------------

        private Shader m_shader;
        private Material m_material;
        // Material parameter block that lets us set different material parameters in the far field and near field
        private MaterialPropertyBlock m_materialParams = new();

        private ComputeShader m_depthComputeShader;
        private int m_depthCullKernel;
        private int m_depthKernel;

        private ComputeShader m_map3DGSShader;
        private int m_map3DGSKernel;

        // ---------------------------------------------------------
        // Sort & Reduce Then Scan (GPU)
        // --------------------------------------------------------- 

        // Expose control over when the renderer performs distance based sorting of splats.
        // Mainly useful for developer debugging purposes.
        public enum SortBehavior : int
        {
            Disabled,
            OnceOnFirstFrame,
            FirstCameraPerNthFrame,
            FirstCameraPerFrame,
            PerCameraPerFrame,
        }

        private SortBehavior m_sortBehavior;
        public int m_sortNthFrame = 100;
        public int m_frameCounter = 0;

        private GpuSortAlgorithm m_gpuSortAlgorithm;
        private IGpuSort m_gpuSort;

        // ---------------------------------------------------------
        // Buffers
        // ---------------------------------------------------------

        // GPU Buffers

        private GraphicsBuffer m_gpuIndices;
        private ComputeBuffer m_sortedSplatDepth;
        private ComputeBuffer m_sortedSplatIndex;

        private ComputeBuffer m_indirectDrawBuffer;

        private GraphicsBuffer m_gpuDataSourceLodIndex;

        // The data we need to render our splats into one of the far field's cache planes
        private class CachePlaneData
        {
            // Index of the first splat to render to this cache plane
            public int firstSplat;
            
            // Number of splats in this cache plane
            public int splatCount;

            // Output of the splats mapping shader
            public ComputeBuffer mappedSplatsBuffer;
        };
        
        // Number of splats in the near field - e.g. the number directly rendered  
        private int m_splatsInNearFieldCount = 0;

        // Data for each of the planes in the far field cache: first splat, splat counts, and the buffer that holds the
        // mapped splats 
        private List<CachePlaneData> m_cachePlaneDatas = new();

        // GPU splats in the near field. This buffer contains one entry per eye for each splat
        private ComputeBuffer m_nearFieldGpuSplatBuffer = null;

        // ---------------------------------------------------------
        // Scalars
        // ---------------------------------------------------------

        private int m_dataSourceMinLodIndex = 0;
        private int m_dataSourceMaxLodIndex = 0;
        public float m_nearClipThreshold = 0.25f;
        private float4 m_objectIdColor;

        // ---------------------------------------------------------
        // Flags
        // ---------------------------------------------------------

        public bool m_fadeLargeSplats = false;

        // ---------------------------------------------------------
        // Constants
        // ---------------------------------------------------------

        const int kGpuSplatDataSize = 48;
        const float kCosineFiveDegrees = 0.9961946f;

        // ---------------------------------------------------------
        // State
        // ---------------------------------------------------------

        private bool m_disposed;
        private int m_lastUpdateFrame = 0;
        private Vector3 m_lastSortRayToCamera = new Vector3(1.0f, 0.0f, 0.0f);

        // ---------------------------------------------------------
        // Profiler markers
        // ---------------------------------------------------------

        static string s_profilerPrefix = "[GeometryRenderer] ";

        // CPU Markers
        static readonly ProfilerMarker s_createResourcesMarker = new ProfilerMarker(
            s_profilerPrefix + "Create graphics resources"
        );
        static readonly ProfilerMarker s_destroyResourcesMarker = new ProfilerMarker(
            s_profilerPrefix + "Destroy graphics resources"
        );
        static readonly ProfilerMarker s_sortCpuMarker = new ProfilerMarker(
            s_profilerPrefix + "Populate sort commands"
        );
        static readonly ProfilerMarker s_drawCpuMarker = new ProfilerMarker(
            s_profilerPrefix + "Populate draw commands"
        );

        // GPU Markers
        static readonly ProfilerMarker s_calculateDepthGpuMarker = new ProfilerMarker(
            ProfilerCategory.Render, s_profilerPrefix + "Calculate Depth (GPU)", MarkerFlags.SampleGPU
        );

        static readonly ProfilerMarker s_calculateReduceScanGpuMarker = new ProfilerMarker(
            ProfilerCategory.Render, s_profilerPrefix + "Reduce-Scan (GPU)", MarkerFlags.SampleGPU
        );

        static readonly ProfilerMarker s_sortGaussiansGpuMarker = new ProfilerMarker(
            ProfilerCategory.Render, s_profilerPrefix + "Sort Gaussians (GPU)", MarkerFlags.SampleGPU
        );

        static readonly ProfilerMarker s_drawGpuMarker = new ProfilerMarker(
            ProfilerCategory.Render, s_profilerPrefix + "Draw (GPU)", MarkerFlags.SampleGPU
        );

        static readonly ProfilerMarker s_map3DGSGpuMarker = new ProfilerMarker(
            ProfilerCategory.Render, s_profilerPrefix + "Map 3DGS (GPU)", MarkerFlags.SampleGPU
        );

        static readonly ProfilerMarker s_splitFarFieldMarker = new ProfilerMarker(
            ProfilerCategory.Render, s_profilerPrefix + "Split far field splats", MarkerFlags.SampleGPU
        );

        // Symbol names used in the shaders.
        private static class ShaderIds
        {
            // Gaussian Splatting related
            public static readonly int BlockDim = Shader.PropertyToID("_BlockDim");

            public static readonly int SplatDepth = Shader.PropertyToID("_SplatDepth");
            public static readonly int SplatIndex = Shader.PropertyToID("_SplatIndex");
            public static readonly int SortedSplatDepth = Shader.PropertyToID("_SortedSplatDepth");
            public static readonly int SortedSplatIndex = Shader.PropertyToID("_SortedSplatIndex");
            public static readonly int SplatToDataSourceIndex = Shader.PropertyToID("_SplatToDataSourceIndex");
            public static readonly int DataSourceOpacity = Shader.PropertyToID("_DataSourceOpacity");
            public static readonly int DataSourceLodIndex = Shader.PropertyToID("_DataSourceLodIndex");
            public static readonly int DataSourceMinLodIndex = Shader.PropertyToID("_DataSourceMinLodIndex");
            public static readonly int DataSourceMaxLodIndex = Shader.PropertyToID("_DataSourceMaxLodIndex");

            public static readonly int NumSplats = Shader.PropertyToID("_NumSplats");
            public static readonly int FirstSplat = Shader.PropertyToID("_FirstSplat");

            public static readonly int SHOrder = Shader.PropertyToID("_SH_Order");
            public static readonly int SHOnly = Shader.PropertyToID("_SH_Only");
            public static readonly int SHCount = Shader.PropertyToID("_SH_Count");

            public static readonly int ObjectId = Shader.PropertyToID("_ObjectId");

            public static readonly int ModelInWorldSpace = Shader.PropertyToID("_ModelInWorldSpace");
            public static readonly int VecScreenParams = Shader.PropertyToID("_VecScreenParams");
            public static readonly int VecProjectionParams = Shader.PropertyToID("_VecProjectionParams");

            public static readonly int GpuSplat = Shader.PropertyToID("_GpuSplat");
            public static readonly int IndirectDrawBuffer = Shader.PropertyToID("_IndirectDrawBuffer");
            public static readonly int GaussianSigmaThreshold = Shader.PropertyToID("_GaussianSigmaThreshold");
            public static readonly int AlphaCullingThreshold = Shader.PropertyToID("_AlphaCullingThreshold");
            public static readonly int NearClipThreshold = Shader.PropertyToID("_NearClipThreshold");
            public static readonly int FadeLargeSplats = Shader.PropertyToID("_FadeLargeSplats");

            public static readonly int EyeData = Shader.PropertyToID("_EyeData");
            public static readonly int BaseOffset = Shader.PropertyToID("_BaseOffset");
            public static readonly int EyeCount = Shader.PropertyToID("_EyeCount");
            public static readonly int EyeDataIndex = Shader.PropertyToID("_EyeDataIndex");

            public static readonly int CameraPosition = Shader.PropertyToID("_CameraPosition");

            public static readonly int GaussianSplatRT = Shader.PropertyToID("_GaussianSplatRT");
        }

        // ---------------------------------------------------------
        // Setters
        // ---------------------------------------------------------

        public void SetSortAlgorithm(GpuSortAlgorithm algorithm)
        {
            m_gpuSortAlgorithm = algorithm;
        }

        public void SetSortBehavior(SortBehavior behavior)
        {
            m_sortBehavior = behavior;
        }

        public void SetObjectIdColor(float4 color)
        {
            m_objectIdColor = color;
        }

        public void SetMinMaxLodIndices(int minLodIndex, int maxLodIndex)
        {
            m_dataSourceMinLodIndex = minLodIndex;
            m_dataSourceMaxLodIndex = maxLodIndex;
        }

        // ---------------------------------------------------------
        // Constructor
        // ---------------------------------------------------------

        public GeometryRenderer() { }

        // ---------------------------------------------------------
        // Resource creation
        // ---------------------------------------------------------

        protected override void CreateConstantResources()
        {
            if (m_shader == null)
            {
                m_shader = (Shader)Resources.Load("Shaders/RenderGaussianSplats");
                Assert.IsNotNull(m_shader, "Default shader look-up should not fail.");
            }

            m_material = new Material(m_shader) { name = "GaussianSplatsMaterial" };

            // We explicitly call ComputeShader.Instantiate on each of the ComputeShaders below
            // so that we can emulate the effect of Local shader keywords.  
            //
            // Otherwise the ComputeShader instance returned from Resources.Load is shared one 
            // accessible by all other render components.

            // Get the handle for the depth compute
            m_depthComputeShader = ComputeShader.Instantiate((ComputeShader)Resources.Load("Shaders/DepthCalculationKernel"));
            m_depthCullKernel = m_depthComputeShader.FindKernel("Calculate3DGSDepth");
            m_depthKernel = m_depthComputeShader.FindKernel("CalculateDepth");

            // Get handle for 3DGS compute kernel
            m_map3DGSShader = ComputeShader.Instantiate((ComputeShader)Resources.Load("Shaders/GaussianSplatMapKernel"));
            m_map3DGSKernel = m_map3DGSShader.FindKernel("GaussianMapping");

            // Set indices to draw a quad in Triangles mode.
            uint[] quadIndices = new uint[]
            {
                    0, 1, 2, 1, 3, 2
            };
            m_gpuIndices = new GraphicsBuffer(
                GraphicsBuffer.Target.Index,
                quadIndices.Length,
                Marshal.SizeOf(typeof(uint))
            );
            m_gpuIndices.SetData(quadIndices);
        }

        // ---------------------------------------------------------
        // Resource management
        // ---------------------------------------------------------

        protected override void UpdateGraphicsResources(GaussianSplatDataSource[] dataSources)
        {
            base.UpdateGraphicsResources(dataSources);

            EnableShaderKeywords(dataSources[0], m_material);

            // Create per-data-source lod index
            m_gpuDataSourceLodIndex?.Dispose();
            m_gpuDataSourceLodIndex = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                m_dataSourceCount,
                Marshal.SizeOf(typeof(int))
            );
            var dataSourceLodIndex = new int[m_dataSourceCount];
            for (uint dataSourceIndex = 0; dataSourceIndex < dataSources.Length; ++dataSourceIndex)
            {
                dataSourceLodIndex[dataSourceIndex] = dataSources[dataSourceIndex].GetLodIndex();
            }
            m_gpuDataSourceLodIndex.SetData(dataSourceLodIndex);

            m_lastUpdateFrame = Time.frameCount;
        }

        protected override void UpdateComputeResources(GaussianSplatDataSource[] dataSources)
        {
            // See EnableShaderKeywords comment in GaussianSplatsRenderer.CreateGraphicsResources
            EnableShaderKeywords(dataSources[0], m_depthComputeShader);
            EnableShaderKeywords(dataSources[0], m_map3DGSShader);

            // Create our gpu sort object.
            if (m_gpuSort == null)
            {
                m_gpuSort = GpuSortFactory.CreateGpuSort(
                    m_gpuSortAlgorithm,
                    m_splatCount,
                    ref m_sortedSplatDepth,
                    ref m_sortedSplatIndex,
                    keyType: typeof(float),
                    payloadType: typeof(uint)
                );
            }
            else {
                m_gpuSort.CreateResources(
                    m_splatCount,
                    ref m_sortedSplatDepth,
                    ref m_sortedSplatIndex,
                    typeof(float),
                    typeof(uint)
                );
            }

            uint[] indirectDrawBuffer = new uint[]
            {
                6, // Indices per instance 
                (uint)m_splatCount, // Number of instances (splats)
                0,
                0,
                0,
            };
            m_indirectDrawBuffer?.Dispose();
            m_indirectDrawBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
            m_indirectDrawBuffer.SetData(indirectDrawBuffer);

            m_lastUpdateFrame = Time.frameCount;
        }

        // Destroy all of the GPU resources associated with the asset.
        protected override void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    using (s_destroyResourcesMarker.Auto())
                    {
                        DisposeBuffer(ref m_gpuIndices);
                        DisposeBuffer(ref m_sortedSplatDepth);
                        DisposeBuffer(ref m_sortedSplatIndex);
                        DisposeBuffer(ref m_indirectDrawBuffer);
                        DisposeBuffer(ref m_nearFieldGpuSplatBuffer);
                        DisposeBuffer(ref m_gpuDataSourceLodIndex);

                        m_gpuSort?.Dispose();
                        m_gpuSort = null;

                        GameObject.DestroyImmediate(m_material);
                    }
                }
                this.m_disposed = true;
            }
            base.Dispose(disposing);
        }

        // ---------------------------------------------------------
        // Render execution
        // ---------------------------------------------------------

        public override void Run(CommandBuffer commandBuffer, Camera camera, Transform transform) {
            base.Run(commandBuffer, camera, transform);

            var isRenderingToFirstEye = camera.stereoActiveEye is Camera.MonoOrStereoscopicEye.Left or Camera.MonoOrStereoscopicEye.Mono;

            // generate ray from this object to the camera's world space position
            Vector3 rayToCamera = transform.InverseTransformPoint(camera.transform.position).normalized;
            // handle unlikely case where camera and object are co-located  
            // which can negatively effect subsequent angle tests and prevent
            // further camera position triggered updates
            if (rayToCamera == Vector3.zero) {
                rayToCamera.Set(1.0f, 0.0f, 0.0f);
            }
            bool cameraRelativePositionChanged = Vector3.Dot(m_lastSortRayToCamera, rayToCamera) < kCosineFiveDegrees ? true : false;

            // only execute a sort if this was a frame where the data was updated or
            // position of the viewing camera has caused a viewing angle change
            if (m_lastUpdateFrame == Time.frameCount || cameraRelativePositionChanged) {
                // update viewing angle change
                m_lastSortRayToCamera = rayToCamera;
                SortSplats(commandBuffer, camera, transform);
            }

            var renderSystem = GaussianSplatRenderSystem.m_instance;
            
            var useFarField = renderSystem.IsFarFieldActive();

            // Split the splats into far field and near field. Render the far field to a texture, then render the near
            // field normally. Farther-away splats appear later in the buffer, so for the near field we draw the front
            // of the buffer

            var cachePlanes = renderSystem.GetCachePlanes();

            m_splatsInNearFieldCount = m_splatCount;
            
            // Initialize the data for the far field's cache planes
            if (useFarField) {
                m_splatsInNearFieldCount = (int)(m_splatCount * renderSystem.GetSplatsInNearFieldProportion());
                var curFieldStart = m_splatsInNearFieldCount;
                
                while (m_cachePlaneDatas.Count < cachePlanes.Count) {
                    m_cachePlaneDatas.Add(new());
                }
                for (var i = 0; i < cachePlanes.Count; i++) {
                    var cachePlaneDefinition = cachePlanes[i];
                    var cachePlaneData = m_cachePlaneDatas[i];

                    cachePlaneData.firstSplat = curFieldStart;
                    cachePlaneData.splatCount = (int)(cachePlaneDefinition.splatsInCacheProportion * m_splatCount);
                    EnsureGpuSplatBufferHasCapacity(ref cachePlaneData.mappedSplatsBuffer, cachePlaneData.splatCount);

                    if (isRenderingToFirstEye && cachePlaneData.splatCount > 0) {
                        // Map the splats in the current cache plane for the center camera
                        // We do this here so that all our dispatches will be together, and all our rasterization will
                        // be together. This will hopefully prevent constantly switching from compute to graphics
                        // subchannels and will enable better work overlap 
                        GPUMapSplats(commandBuffer, camera, transform, m_geometryDrawMode == GeometryDrawMode.SHOnly,
                            m_centerEyeDataBuffer, cachePlaneData.firstSplat, cachePlaneData.splatCount,
                            cachePlaneData.mappedSplatsBuffer);
                    }

                    curFieldStart += cachePlaneData.splatCount;
                }
            }

            EnsureGpuSplatBufferHasCapacity(ref m_nearFieldGpuSplatBuffer, m_splatsInNearFieldCount * m_xrUtils.GetEyeCount());

            if (m_splatsInNearFieldCount > 0) {
                // Map the splats in the near field for the current eye(s)
                GPUMapSplats(commandBuffer, camera, transform, m_geometryDrawMode == GeometryDrawMode.SHOnly, m_eyeData, 0, m_splatsInNearFieldCount, m_nearFieldGpuSplatBuffer);
            }

            // If we haven't yet updated the frame counter, this is the first eye. Render the far field images, then
            // restore the GS render target
            if (isRenderingToFirstEye && useFarField) {
                // Loop through all the planes and draw their splats
                for (var i = 0; i < cachePlanes.Count; i++) {
                    var cachePlaneDefinition = cachePlanes[i];
                    var cachePlaneData = m_cachePlaneDatas[i];
                    
                    commandBuffer.SetRenderTarget(cachePlaneDefinition.renderTexture);
                    commandBuffer.ClearRenderTarget(RTClearFlags.Color, new Color(0, 0, 0, 0), 0, 0);

                    if (cachePlaneData.splatCount > 0) {
                        DrawSplats(commandBuffer, transform, cachePlaneData.splatCount, cachePlaneData.mappedSplatsBuffer, false);
                    }
                }
                
                // Restore the original render target
                if (m_xrUtils.IsSinglePassXR()) {
                    commandBuffer.SetRenderTarget(ShaderIds.GaussianSplatRT, BuiltinRenderTextureType.Depth, 0, CubemapFace.Unknown, -1);
                } else {
                    commandBuffer.SetRenderTarget(ShaderIds.GaussianSplatRT, BuiltinRenderTextureType.Depth);
                }
            }

            // Draw the near field
            DrawSplats(commandBuffer, transform, m_splatsInNearFieldCount, m_nearFieldGpuSplatBuffer, m_xrUtils.IsSinglePassXR());
            
            m_frameCounter = Time.frameCount;
        }

        private void EnsureGpuSplatBufferHasCapacity(ref ComputeBuffer gpuSplatBuffer, int count) {
            // This method lets us size the near and far field splat buffers to the number of splats in each field
            // Right now the only way to change the numbers of splats in each field is through the debug menu, so I
            // don't expect this method to be called frequently during normal operation

            // Unity doesn't like allocating a 0-byte buffer
            count = Math.Max(count, 1);

            if (gpuSplatBuffer != null && gpuSplatBuffer.count != count) {
                gpuSplatBuffer.Dispose();
                gpuSplatBuffer = null;
            }

            gpuSplatBuffer ??= new ComputeBuffer(count, kGpuSplatDataSize);
        }

        private void SortSplats(CommandBuffer commandBuffer, Camera camera, Transform transform)
        {
            bool doSort = false;

            // TODO revisit these options in light of updated block frustum culling
            //  since we are already limiting sort to happen only when the data
            //  is updated.
            switch (m_sortBehavior)
            {
                case SortBehavior.OnceOnFirstFrame:
                    if (m_frameCounter == 1)
                        doSort = true;
                    break;
                case SortBehavior.FirstCameraPerNthFrame:
                    if (m_frameCounter != Time.frameCount && m_frameCounter % m_sortNthFrame == 0)
                        doSort = true;
                    break;
                case SortBehavior.FirstCameraPerFrame:
                    // In the case of XR, we will render from two cameras.  Lets only execute the sort
                    // for the first eye and re-use the calculated order for the other eye.
                    if (m_frameCounter != Time.frameCount)
                        doSort = true;
                    break;
                case SortBehavior.PerCameraPerFrame:
                    doSort = true;
                    break;
            }

            if (doSort && m_xrUtils.SortMonoOrLeftEye(camera))
            {
                using (s_sortCpuMarker.Auto())
                {
                    Assert.IsNotNull(camera);
                    DispatchDepthCalculationKernel(commandBuffer, camera, transform.localToWorldMatrix);
                    DispatchSortKernel(commandBuffer);
                }
            }
        }

        private void GPUMapSplats(CommandBuffer commandBuffer, Camera camera, Transform transform, bool shOnly, ComputeBuffer eyeDataBuffer, int firstSplat, int splatsToMapCount, ComputeBuffer gpuSplats) {
            Dispatch3DGSCalculationKernel(commandBuffer, camera, transform, shOnly, eyeDataBuffer, firstSplat, splatsToMapCount, gpuSplats);
        }

        private void DrawSplats(CommandBuffer commandBuffer, Transform transform, int splatsToDrawCount, ComputeBuffer gpuSplatsBuffer, bool isSinglePassXR)
        {
            switch (m_geometryDrawMode)
            {
                case GeometryDrawMode.Splats:
                case GeometryDrawMode.SplatsWithBoundingBox:
                case GeometryDrawMode.SplatsWithBoundingLocator:
                case GeometryDrawMode.SHOnly:
                case GeometryDrawMode.TotalOpacity:
                    Draw(commandBuffer, transform, shaderPassId: GeometryRenderer.ShaderPassId.Beauty, splatsToDrawCount, gpuSplatsBuffer, isSinglePassXR);
                    break;
                case GeometryDrawMode.OpaqueSplats:
                    Draw(commandBuffer, transform, shaderPassId: GeometryRenderer.ShaderPassId.Opaque, splatsToDrawCount, gpuSplatsBuffer, isSinglePassXR);
                    break;
                case GeometryDrawMode.ObjectId:
                    Draw(commandBuffer, transform, shaderPassId: GeometryRenderer.ShaderPassId.ObjectId, splatsToDrawCount, gpuSplatsBuffer, isSinglePassXR);
                    break;
                case GeometryDrawMode.LodHeatMap:
                    Draw(commandBuffer, transform, shaderPassId: GeometryRenderer.ShaderPassId.LodHeatMap, splatsToDrawCount, gpuSplatsBuffer, isSinglePassXR);
                    break;
                case GeometryDrawMode.Highlight:
                    Draw(commandBuffer, transform, shaderPassId: GeometryRenderer.ShaderPassId.Highlight, splatsToDrawCount, gpuSplatsBuffer, isSinglePassXR);
                    break;
                default:
                    break;
            }
        }

        private void Draw(CommandBuffer commandBuffer, Transform transform, ShaderPassId shaderPassId, int splatsToDrawCount, ComputeBuffer gpuSplatsBuffer, bool isSinglePassXR)
        {
            using (s_drawCpuMarker.Auto())
            {
                // Pass input data to the shader. Use a MaterialPropertyBlock so that repeated executions get different splats
                m_materialParams.SetBuffer(ShaderIds.GpuSplat, gpuSplatsBuffer);

                m_renderBuffers.m_gpuPositionBounds?.SetBufferOnMaterial(m_material);
                m_material.SetBuffer(ShaderIds.SplatToDataSourceIndex, m_gpuSplatToDataSourceIndex);
                m_material.SetFloat(ShaderIds.GaussianSigmaThreshold, m_gaussianSigmaThreshold);
                m_material.SetFloat(ShaderIds.AlphaCullingThreshold, m_alphaCullingThreshold);
                m_material.SetFloat(ShaderIds.NearClipThreshold, m_nearClipThreshold);
                m_material.SetFloat(ShaderIds.FadeLargeSplats, m_fadeLargeSplats ? 1.0f : 0.0f);

                m_material.SetInt("_EyeStride", isSinglePassXR ? m_splatCount : 0);

                // Shader pass dependent bindings
                switch (shaderPassId)
                {
                    case ShaderPassId.ObjectId:
                        m_material.SetVector(ShaderIds.ObjectId, m_objectIdColor);
                        break;

                    case ShaderPassId.LodHeatMap:
                        m_material.SetBuffer(ShaderIds.SortedSplatIndex, m_sortedSplatIndex);
                        m_material.SetBuffer(ShaderIds.DataSourceLodIndex, m_gpuDataSourceLodIndex);
                        m_material.SetInt(ShaderIds.DataSourceMinLodIndex, m_dataSourceMinLodIndex);
                        m_material.SetInt(ShaderIds.DataSourceMaxLodIndex, m_dataSourceMaxLodIndex);
                        break;
                }

                commandBuffer.BeginSample(s_drawGpuMarker);

                commandBuffer.DrawProcedural(
                    m_gpuIndices,
                    transform.localToWorldMatrix,
                    m_material,
                    (int)shaderPassId,
                    MeshTopology.Triangles,
                    6,
                    splatsToDrawCount,
                    m_materialParams);

                commandBuffer.EndSample(s_drawGpuMarker);
            }
        }

        // ---------------------------------------------------------
        // Compute dispatch
        // ---------------------------------------------------------

        //function dispatches kernel which computes depth and frustum culling for each splat. (see AppendGPUSortCommand() for more info)
        private void DispatchDepthCullCalculationKernel(CommandBuffer commandBuffer, Camera camera, Matrix4x4 localToWorldMatrix)
        {
            //bind data
            commandBuffer.SetComputeMatrixParam(m_depthComputeShader, ShaderIds.ModelInWorldSpace, localToWorldMatrix);
            commandBuffer.SetComputeIntParam(m_depthComputeShader, ShaderIds.BlockDim, m_renderBuffers.m_gpuPositions.GetBlockDim());
            commandBuffer.SetComputeIntParam(m_depthComputeShader, "_PayloadSize", m_splatCount);

            commandBuffer.SetComputeBufferParam(m_depthComputeShader, m_depthCullKernel, ShaderIds.SplatDepth, m_sortedSplatDepth);
            commandBuffer.SetComputeBufferParam(m_depthComputeShader, m_depthCullKernel, ShaderIds.SplatIndex, m_sortedSplatIndex);
            commandBuffer.SetComputeBufferParam(m_depthComputeShader, m_depthCullKernel, ShaderIds.EyeData, m_eyeData);

            m_renderBuffers.m_gpuPositions.SetBufferOnComputeShader(commandBuffer, m_depthComputeShader, m_depthCullKernel);
            m_renderBuffers.m_gpuScales.SetBufferOnComputeShader(commandBuffer, m_depthComputeShader, m_depthCullKernel);
            m_renderBuffers.m_gpuPositionBounds?.SetBufferOnComputeShader(commandBuffer, m_depthComputeShader, m_depthCullKernel);

            var (threadGroupCountX, _, _) = ComputeKernelUtils.CalculateThreadGroupCount(m_depthComputeShader,
                m_depthCullKernel, m_splatCount);

            //Dispatch the compute shader
            commandBuffer.BeginSample(s_calculateDepthGpuMarker);
            commandBuffer.DispatchCompute(m_depthComputeShader, m_depthCullKernel, threadGroupCountX, 1, 1);
            commandBuffer.EndSample(s_calculateDepthGpuMarker);
        }

        //function dispatches kernel which computes depth for each splat
        private void DispatchDepthCalculationKernel(CommandBuffer commandBuffer, Camera camera, Matrix4x4 localToWorldMatrix)
        {
            //bind data
            commandBuffer.SetComputeMatrixParam(m_depthComputeShader, ShaderIds.ModelInWorldSpace, localToWorldMatrix);
            commandBuffer.SetComputeIntParam(m_depthComputeShader, ShaderIds.NumSplats, m_splatCount);

            commandBuffer.SetComputeBufferParam(m_depthComputeShader, m_depthKernel, ShaderIds.SplatDepth, m_sortedSplatDepth);
            commandBuffer.SetComputeBufferParam(m_depthComputeShader, m_depthKernel, ShaderIds.SplatIndex, m_sortedSplatIndex);
            commandBuffer.SetComputeBufferParam(m_depthComputeShader, m_depthKernel, ShaderIds.EyeData, m_eyeData);

            m_renderBuffers.m_gpuPositions.SetBufferOnComputeShader(commandBuffer, m_depthComputeShader, m_depthKernel);

            var (threadGroupCountX, _, _) = ComputeKernelUtils.CalculateThreadGroupCount(m_depthComputeShader,
                m_depthKernel, m_splatCount);

            //Dispatch the compute shader
            commandBuffer.BeginSample(s_calculateDepthGpuMarker);
            commandBuffer.DispatchCompute(m_depthComputeShader, m_depthKernel, threadGroupCountX, 1, 1);
            commandBuffer.EndSample(s_calculateDepthGpuMarker);
        }

        //function dispatches kernel which sorts splats depending on depth. (see AppendGPUSortCommand() for more info)
        private void DispatchSortKernel(CommandBuffer commandBuffer)
        {
            commandBuffer.BeginSample(s_sortGaussiansGpuMarker);

            m_gpuSort.Sort(
                commandBuffer,
                m_splatCount,
                m_sortedSplatDepth,
                m_sortedSplatIndex,
                m_indirectDrawBuffer,
                keyType: typeof(float),
                payloadType: typeof(uint)
            );

            commandBuffer.EndSample(s_sortGaussiansGpuMarker);
        }

        private void Dispatch3DGSCalculationKernel(
            CommandBuffer commandBuffer,
            Camera camera,
            Transform transform,
            bool shOnly,
            ComputeBuffer eyeDataBuffer,
            int firstSplat,
            int splatsToMapCount,
            ComputeBuffer gpuSplats)
        {
            commandBuffer.BeginSample(s_map3DGSGpuMarker);

            Vector4 cameraPosition = camera.transform.localToWorldMatrix.GetPosition();
            commandBuffer.SetComputeVectorParam(m_map3DGSShader, ShaderIds.CameraPosition, cameraPosition);

            Vector4 projectionParams = new Vector4(
                -1.0f,
                camera.nearClipPlane,
                camera.farClipPlane,
                1.0f / camera.farClipPlane
            );

            int screenW = camera.pixelWidth;
            int screenH = camera.pixelHeight;

            if (m_xrUtils.IsStereo())
            {
                screenW = XRSettings.eyeTextureWidth;
                screenH = XRSettings.eyeTextureHeight;
            }

            Vector4 screenParams = new Vector4(screenW, screenH, 0, 0);

            commandBuffer.SetComputeMatrixParam(m_map3DGSShader, "_MatrixObjectToWorld", transform.localToWorldMatrix);
            commandBuffer.SetComputeMatrixParam(m_map3DGSShader, "_MatrixWorldToObject", transform.worldToLocalMatrix);

            commandBuffer.SetComputeIntParam(m_map3DGSShader, ShaderIds.BaseOffset, m_splatCount);

            // This index controls which eye the mapping kernel computes its data for
            int eyeDataIndex = 0;
            if (m_xrUtils.IsMultiPassXR())
            {
                // set the appropriate eye data index for the active eye in this pass
                eyeDataIndex = (int)camera.stereoActiveEye; 
                
                // Unity is not sending the correct active eye when using URP + Multi-pass.
                // stereoActiveEye is read as 2, which means Mono, thus incorrect. 
                // The snippet below checks if we are using URP and fetches the correct active eye using the URP camera.xr.multipasId
                if (GraphicsSettings.currentRenderPipeline != null) {
                    eyeDataIndex = XRFrameInfo.m_multipassId;
                }
                
            }

            commandBuffer.SetComputeIntParam(m_map3DGSShader, ShaderIds.EyeDataIndex, eyeDataIndex);
            commandBuffer.SetComputeIntParam(m_map3DGSShader, ShaderIds.EyeCount, m_xrUtils.GetEyeCount());

            commandBuffer.SetComputeVectorParam(m_map3DGSShader, ShaderIds.VecProjectionParams, projectionParams);
            commandBuffer.SetComputeVectorParam(m_map3DGSShader, ShaderIds.VecScreenParams, screenParams);

            commandBuffer.SetComputeBufferParam(m_map3DGSShader, m_map3DGSKernel, ShaderIds.EyeData, eyeDataBuffer);
            commandBuffer.SetComputeBufferParam(m_map3DGSShader, m_map3DGSKernel, ShaderIds.SortedSplatIndex, m_sortedSplatIndex);
            commandBuffer.SetComputeBufferParam(m_map3DGSShader, m_map3DGSKernel, ShaderIds.IndirectDrawBuffer, m_indirectDrawBuffer);
            commandBuffer.SetComputeBufferParam(m_map3DGSShader, m_map3DGSKernel, ShaderIds.GpuSplat, gpuSplats);

            m_renderBuffers.m_gpuPositions.SetBufferOnComputeShader(commandBuffer, m_map3DGSShader, m_map3DGSKernel);
            m_renderBuffers.m_gpuScales.SetBufferOnComputeShader(commandBuffer, m_map3DGSShader, m_map3DGSKernel);
            m_renderBuffers.m_gpuColors.SetBufferOnComputeShader(commandBuffer, m_map3DGSShader, m_map3DGSKernel);
            m_renderBuffers.m_gpuOrientations.SetBufferOnComputeShader(commandBuffer, m_map3DGSShader, m_map3DGSKernel);

            if (m_shCount > 0)
            {
                m_renderBuffers.m_gpuSHCoefficients.SetBufferOnComputeShader(commandBuffer, m_map3DGSShader, m_map3DGSKernel);
            }

            commandBuffer.SetComputeBufferParam(m_map3DGSShader, m_map3DGSKernel, ShaderIds.SplatToDataSourceIndex, m_gpuSplatToDataSourceIndex);
            commandBuffer.SetComputeBufferParam(m_map3DGSShader, m_map3DGSKernel, ShaderIds.DataSourceOpacity, m_gpuDataSourceOpacity);

            commandBuffer.SetComputeIntParam(m_map3DGSShader, ShaderIds.NumSplats, splatsToMapCount);
            commandBuffer.SetComputeIntParam(m_map3DGSShader, ShaderIds.FirstSplat, firstSplat);
            commandBuffer.SetComputeIntParam(m_map3DGSShader, ShaderIds.SHOrder, m_shOrder);
            commandBuffer.SetComputeIntParam(m_map3DGSShader, ShaderIds.SHOnly, shOnly ? 1 : 0);
            commandBuffer.SetComputeIntParam(m_map3DGSShader, ShaderIds.SHCount, m_shCount);
            
            var (threadGroupCountX, _, _) = ComputeKernelUtils.CalculateThreadGroupCount(m_map3DGSShader,
                m_map3DGSKernel, splatsToMapCount);
            commandBuffer.DispatchCompute(m_map3DGSShader, m_map3DGSKernel, threadGroupCountX, 1, 1);

            commandBuffer.EndSample(s_map3DGSGpuMarker);
        }

        // ---------------------------------------------------------
        // Shader/Material Functions
        // ---------------------------------------------------------

        public override void UpdateCompositePass(Material material)
        {
            if (m_geometryDrawMode == GeometryDrawMode.TotalOpacity)
                material.EnableKeyword("DEBUG_TOTAL_OPACITY");
            else
                material.DisableKeyword("DEBUG_TOTAL_OPACITY");
        }

        public override void SetFarFieldParameters(Material farFieldMaterial) {
            // The count of splats in the near field doubles as the index of the first splat in the far field, since all
            // the splats are tightly packed into one buffer
            // This variable may need a better name, PRs are welcome 
            farFieldMaterial.SetInt(ShaderIds.FirstSplat, m_splatsInNearFieldCount);
            farFieldMaterial.SetBuffer(ShaderIds.SortedSplatDepth, m_sortedSplatDepth);
        }
    }
}
