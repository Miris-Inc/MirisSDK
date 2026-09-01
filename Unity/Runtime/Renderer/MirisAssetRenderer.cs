// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using Unity.Profiling;

namespace Miris.Runtime
{
    /// <summary>
    /// MirisAssetRenderer is the base class for gaussian splat rendering.
    ///
    /// It provides common dataSource and buffer management functionality 
    /// as well as common utilities from which a specialized renderer can be derived. 
    ///
    /// </summary>

    // Core Gaussian Splat rendering resources & logic 
    public class MirisAssetRenderer : IDisposable
    {
        public virtual void UpdateCompositePass(Material material) { }

        // ---------------------------------------------------------
        // Resources and buffers
        // ---------------------------------------------------------

        private GPUResourceTracker m_resourceTracker = new ();
        private BufferCopier m_bufferCopier = new();
        private MosaicTextureToAtlasBufferConverter m_bufferConverter = new();

        protected struct RenderBuffers
        {
            public void Dispose()
            {
                DisposeBuffer(ref m_gpuPositions);
                DisposeBuffer(ref m_gpuPositionBounds);
                DisposeBuffer(ref m_gpuOrientations);
                DisposeBuffer(ref m_gpuScales);
                DisposeBuffer(ref m_gpuColors);
                DisposeBuffer(ref m_gpuSHCoefficients);
            }

            public IGpuBuffer m_gpuPositions;
            public IGpuBuffer m_gpuPositionBounds;
            public IGpuBuffer m_gpuOrientations;
            public IGpuBuffer m_gpuScales;
            public IGpuBuffer m_gpuColors;
            public IGpuBuffer m_gpuSHCoefficients;
        }
        
        protected RenderBuffers m_renderBuffers;

        // GPU Buffers
        protected GraphicsBuffer m_gpuDataSourceOpacity;
        protected GraphicsBuffer m_gpuSplatToDataSourceIndex;

        // CPU Buffers
        private float[] m_dataSourceOpacity;
        private uint[] m_splatToDataSourceIndex = {};

        // --------------------------------------------------------- 
        // XR
        // --------------------------------------------------------- 
        
        protected XRUtils m_xrUtils = new XRUtils();

        // ---------------------------------------------------------
        // Camera/Eye Data
        // ---------------------------------------------------------

        // The EyeData struct holds data for the left and right eye while in XR mode.
        // A HSLS variant for the EyeData struct is defined in CommonStructs.hlsl.
        protected struct EyeData {
            public Matrix4x4 view;
            public Matrix4x4 proj;
            public Vector3 position;
            public float padding;
        }

        protected ComputeBuffer m_eyeData;
        protected EyeData[] m_eyeDataArray = new EyeData[2];

        protected ComputeBuffer m_centerEyeDataBuffer;
        protected EyeData[] m_centerEyeData = new EyeData[1];

        // ---------------------------------------------------------
        // Counters
        // ---------------------------------------------------------

        protected int m_splatCount = 0;
        public int splatCount => m_splatCount;
        protected int m_shCount = 0;
        protected int m_dataSourceCount = 0;

        // ---------------------------------------------------------
        // Scalars
        // ---------------------------------------------------------

        public float m_gaussianSigmaThreshold = 3.0f;
        public float m_alphaCullingThreshold = 0.06f;
        // Alpha multiplier derived from the requested whole-asset opacity -- see SetAssetOpacity
        // for why the two differ. Folded into the per-data-source opacity at upload time rather
        // than replacing it, so it composes with the LOD cross-fade instead of fighting it.
        protected float m_assetOpacityScale = 1.0f;
        public int m_shOrder = 0;

        // ---------------------------------------------------------
        // State
        // ---------------------------------------------------------

        private bool m_disposed;

        // Whether UpdateResources actually allocated the GPU and compute buffers. It skips them
        // entirely for a data source set that carries no splats yet, while UpdateRenderer assigns
        // this renderer before calling it - so "the renderer exists" and "the renderer can draw"
        // are genuinely different questions. MirisAssetRenderComponent.CanRender asks this one.
        private bool m_resourcesReady;

        public bool HasResources => m_resourcesReady && !m_disposed;

        // ---------------------------------------------------------
        // Profiling
        // ---------------------------------------------------------

        static string s_profilerBasePrefix = "[MirisAssetRenderer] ";

        // CPU Markers
        static readonly ProfilerMarker s_createResourcesBaseMarker = new ProfilerMarker(
            s_profilerBasePrefix + "Create graphics resources"
        );
        static readonly ProfilerMarker s_destroyResourcesBaseMarker = new ProfilerMarker(
            s_profilerBasePrefix + "Destroy graphics resources"
        );

        //// Symbol names used in the shaders.
        private static class ShaderIds
        {
            public static readonly int Positions = Shader.PropertyToID("_Positions");
            public static readonly int BlockBounds = Shader.PropertyToID("_BlockBounds");
            public static readonly int Orientations = Shader.PropertyToID("_Orientations");
            public static readonly int Scales = Shader.PropertyToID("_Scales");
            public static readonly int Colors = Shader.PropertyToID("_Colors");
            public static readonly int SHCoefficients = Shader.PropertyToID("_SHCoefficients");
            public static readonly int PositionsTextureWidth = Shader.PropertyToID("_PositionsTextureWidth");
            public static readonly int PositionBoundsTextureWidth = Shader.PropertyToID("_PositionBoundsTextureWidth");
            public static readonly int OrientationsTextureWidth = Shader.PropertyToID("_OrientationsTextureWidth");
            public static readonly int ScalesTextureWidth = Shader.PropertyToID("_ScalesTextureWidth");
            public static readonly int ColorsTextureWidth = Shader.PropertyToID("_ColorsTextureWidth");
            public static readonly int SHCoefficientsTextureWidth = Shader.PropertyToID("_SHCoefficientsTextureWidth");
        }

        // ---------------------------------------------------------
        // Setters
        // ---------------------------------------------------------

        public virtual void SetGaussianSigmaThreshold(float threshold)
        {
            m_gaussianSigmaThreshold = threshold;
        }

        public virtual void SetAlphaCullingThreshold(float threshold)
        {
            if (threshold >= 0.0f && threshold <= 1.0f)
            {
                m_alphaCullingThreshold = threshold;
            }
        }

        // Optical depth assumed for a fully covered pixel when linearising the opacity control.
        //
        // Assumed, not measured: it was picked as a plausible figure for dense 3DGS content, and no
        // asset was profiled to arrive at it. Everything the control does is calibrated here, so
        // treat it as a dial rather than a constant -- lower straightens the curve and suits sparse
        // captures, higher hollows dense ones out at a touch.
        private const float c_referenceOpticalDepth = 8.0f;

        // Slider position below which the linearised curve is used as-is. Above it the curve is
        // blended back toward a plain multiply. Must stay below 1.0.
        private const float c_opacityBlendStart = 0.85f;

        // Whole-asset transparency in [0, 1]. What gets uploaded is not this value but the alpha
        // scale that lands the *composited* result near it.
        //
        // Splats accumulate front-to-back (Blend OneMinusDstAlpha One), so a pixel covered by many
        // of them reaches 1 - exp(-tau) and saturates. Passing the raw opacity through as a
        // multiplier therefore does almost nothing over most of its range: at tau = 8, halving
        // every splat's alpha still composites to 0.98, and the control only appears to engage
        // near 0.2 where the exponential finally comes off its shoulder. Solving
        // 1 - exp(-scale * tau) = opacity * (1 - exp(-tau)) for scale undoes that.
        //
        // Exact only where local optical depth is near c_referenceOpticalDepth. Sparser parts of
        // the asset -- wispy edges, isolated splats -- thin out ahead of the dense core, because
        // one accumulation buffer shared by every asset leaves nowhere to scale a single asset's
        // composited alpha directly.
        public virtual void SetAssetOpacity(float opacity)
        {
            float clampedOpacity = Mathf.Clamp01(opacity);

            // Algebraically 1 - opacity * (1 - exp(-tau)), rearranged to keep it out of float32's
            // blind spot. That form subtracts two nearly equal numbers as opacity approaches 1: at
            // tau = 16 the endpoint is already off by 3.6e-3, and by tau = 20 it underflows to zero
            // outright, so Log returns -Infinity, the blend below evaluates Inf * 0, and a NaN
            // reaches _DataSourceOpacity. This form never subtracts near-equal values -- at
            // opacity 1 it is exactly exp(-tau) -- and stays exact at both endpoints for any tau.
            float transmittance = Mathf.Exp(-c_referenceOpticalDepth);
            float remapped =
                -Mathf.Log((1.0f - clampedOpacity) + clampedOpacity * transmittance) / c_referenceOpticalDepth;

            // Ease back toward a plain multiply as the slider approaches 1. The linearised curve is
            // near vertical up there -- its slope at 1.0 is about 373, so 0.99 already asks for a
            // 0.57 alpha scale -- and the first pixel of travel off full opacity visibly halves
            // anything thinner than c_referenceOpticalDepth. Blending leaves 1.0 with slope 1, so a
            // nudge reads as a nudge, and drops the steepest slope anywhere on the curve to ~7.
            //
            // Written out rather than calling Mathf.SmoothStep, which interpolates between its
            // first two arguments instead of returning a 0..1 weight the way the GLSL function of
            // the same name does. Zero derivative at both ends, so neither handoff has a kink.
            float t = Mathf.Clamp01((clampedOpacity - 1.0f) / (c_opacityBlendStart - 1.0f));
            float weight = t * t * (3.0f - 2.0f * t);

            // The cost is accuracy at the top of the range: on content actually at
            // c_referenceOpticalDepth, 0.9 now composites to about 0.97 rather than 0.90. Below
            // c_opacityBlendStart the weight saturates and the curve is untouched.
            m_assetOpacityScale = Mathf.Clamp01(Mathf.Lerp(clampedOpacity, remapped, weight));
        }

        public virtual void SetSHOrder(int order)
        {
            if (order >= 0 && order <= 3)
            {
                m_shOrder = order;
            }
        }

        // Returns the maximum SH order supported by the loaded data.
        // Converts coefficient count to SH order using: order = sqrt(coefficients + 1) - 1
        public int GetMaxSHOrder()
        {
            if (m_shCount <= 0) return 0;
            // Formula: coefficients = (order + 1)^2 - 1, so order = sqrt(coefficients + 1) - 1
            return (int)Mathf.Sqrt(m_shCount + 1) - 1;
        }

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        public MirisAssetRenderer()
        {
            CreateConstantResources();
        }

        // ---------------------------------------------------------
        // Resource creation
        // ---------------------------------------------------------

        protected virtual void CreateConstantResources() { }

        // ---------------------------------------------------------
        // Resource management
        // ---------------------------------------------------------

        public void UpdateResources(MirisAssetDataSource[] dataSources)
        {
            //MirisDebug.Log($"[MirisAssetRenderer] Updating graphics resources for {dataSources.Length} data sources");

            using (s_createResourcesBaseMarker.Auto())
            {
                m_splatCount = 0;
                foreach (var dataSource in dataSources)
                {
                    m_splatCount += dataSource.GetSplatCount();
                }

                // Set after the calls, not from m_splatCount alone: if either throws, the buffers
                // are half-built and this renderer must not be handed to the render system.
                m_resourcesReady = false;
                if (m_splatCount > 0)
                {
                    UpdateGraphicsResources(dataSources);
                    UpdateComputeResources(dataSources);
                    m_resourcesReady = true;
                }

                foreach (var dataSource in dataSources)
                {
                    dataSource.m_dirty = false;
                }
            }
        }

        protected virtual void UpdateComputeResources(MirisAssetDataSource[] dataSources) { }

        private AttributeBuffer[] CreateAndTrackGpuBuffer(MirisAssetDataSource[] dataSources, AttributeSemantic semantic, int shaderId, int textureWidthShaderId, ref IGpuBuffer gpuBuffer, string name)
        {
            AttributeBuffer[] buffers = CollectCpuBuffers(dataSources, semantic);
            GpuBufferFactory.CreateGpuBuffer(
                buffers,
                shaderId,
                textureWidthShaderId,
                ref gpuBuffer,
                name
            );
            m_resourceTracker.Track(buffers, m_bufferConverter);
            m_resourceTracker.Merge(buffers, semantic, gpuBuffer, m_bufferCopier);
            return buffers;
        }
        protected virtual void UpdateGraphicsResources(MirisAssetDataSource[] dataSources)
        {
            // SHCoefficients (optional)
            if (dataSources[0].HasBuffer(AttributeSemantic.SHCoefficients))
            {
                CreateAndTrackGpuBuffer(dataSources,AttributeSemantic.SHCoefficients, ShaderIds.SHCoefficients, ShaderIds.SHCoefficientsTextureWidth, ref m_renderBuffers.m_gpuSHCoefficients, "shBuffer");
                int lastShCount = m_shCount;
                m_shCount = GetShCount(dataSources, AttributeSemantic.SHCoefficients);
                if (lastShCount != m_shCount) MirisDebug.Log($"[MirisAssetRenderer] dataSource contains {m_shCount} SH coefficients.");
            }

            // Orientation
            CreateAndTrackGpuBuffer(dataSources,AttributeSemantic.Orientation, ShaderIds.Orientations, ShaderIds.OrientationsTextureWidth, ref m_renderBuffers.m_gpuOrientations, "orientationBuffer");

            AttributeBuffer[] positionBuffers = CreateAndTrackGpuBuffer(dataSources,AttributeSemantic.Position, ShaderIds.Positions, ShaderIds.PositionsTextureWidth, ref m_renderBuffers.m_gpuPositions, "positionBuffer");

            // BlockBounds (optional)
            if (dataSources[0].GetSupportedSemantics().Contains(AttributeSemantic.BlockBounds))
            {
                CreateAndTrackGpuBuffer(dataSources,AttributeSemantic.BlockBounds, ShaderIds.BlockBounds, ShaderIds.PositionBoundsTextureWidth, ref m_renderBuffers.m_gpuPositionBounds, "boundsBuffer");
            }

            // Scale
            CreateAndTrackGpuBuffer(dataSources,AttributeSemantic.Scale, ShaderIds.Scales, ShaderIds.ScalesTextureWidth, ref m_renderBuffers.m_gpuScales, "scalesBuffer");

            // Color
            CreateAndTrackGpuBuffer(dataSources,AttributeSemantic.Color, ShaderIds.Colors, ShaderIds.ColorsTextureWidth, ref m_renderBuffers.m_gpuColors, "colorBuffer");

            // Initiate all uploading and copying of data between cpu & gpu buffers
            m_resourceTracker.TriggerUpload(m_bufferCopier);
            m_resourceTracker.TriggerCopy(m_bufferCopier);

            // update splat count to be the actual number of copied splats (positions)
            int copiedSplatCount = 0;
            for (int index = 0; index < positionBuffers.Length; ++index)
            {
                if (m_resourceTracker.Contains(positionBuffers[index].GetHash()))
                {
                    copiedSplatCount += positionBuffers[index].GetSplatCount();
                }
            }
            Debug.Assert(copiedSplatCount == m_splatCount, $"[MirisAssetRenderer] UpdateGraphicsResources expected {m_splatCount} total splats but only {copiedSplatCount} were copied to GPU buffers");
            m_splatCount = copiedSplatCount;
            
            // Create splat index -> data source index buffer.  So we can look up per-data-source properties in the shaders.
            m_dataSourceCount = positionBuffers.Length;
            
            const int chunkSize = 1024 * 128;
            int currentDataSourceIndexSize = m_splatToDataSourceIndex.Length;
            int currentChunkCount = currentDataSourceIndexSize / chunkSize;
            int lowerDataSourceIndexSize = (currentChunkCount - 1) * chunkSize;
            
            // If the current number of splats does not lie within the boundary of the last 'chunk'
            // of data then reallocate. This both expands and shrinks the chunked buffer to keep it 
            // reasonably sized
            if (currentDataSourceIndexSize < m_splatCount || lowerDataSourceIndexSize > m_splatCount)
            {
                MirisDebug.Log($"[MirisAssetRenderer] updating storage for {m_splatCount} splat index array items mapping {m_dataSourceCount} dataSources");

                // allocate a new buffer for storing the dataSourceIndex map in 
                // large chunks to avoid constant reallocations 
                int newDataSourceIndexSize = ((m_splatCount / chunkSize) + 1) * chunkSize;
                m_splatToDataSourceIndex = new uint[newDataSourceIndexSize];

                // Re-allocate the GPU buffer that is used to send this data
                // this ensures the source data and GPU data sizes are in-sync
                m_gpuSplatToDataSourceIndex?.Dispose();
                m_gpuSplatToDataSourceIndex = new GraphicsBuffer(
                    GraphicsBuffer.Target.Raw,
                    newDataSourceIndexSize,
                    Marshal.SizeOf(typeof(uint))
                );
            }
            int splatIndexOffset = 0;
            for (uint dataSourceIndex = 0; dataSourceIndex < m_dataSourceCount; ++dataSourceIndex)
            {
                AttributeBuffer attrBuffer = positionBuffers[dataSourceIndex];
                if (m_resourceTracker.Contains(positionBuffers[dataSourceIndex].GetHash()))
                {
                    int splatCount = attrBuffer.GetSplatCount();
                    Array.Fill(m_splatToDataSourceIndex, dataSourceIndex, splatIndexOffset, splatCount);
                    splatIndexOffset += splatCount;
                }
            }

            // Update per-splat data source index 
            m_gpuSplatToDataSourceIndex.SetData(m_splatToDataSourceIndex);

            // Create and update per-data-source opacity
            m_gpuDataSourceOpacity?.Dispose();
            m_gpuDataSourceOpacity = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                positionBuffers.Length,
                Marshal.SizeOf(typeof(float))
            );
            m_dataSourceOpacity = new float[m_dataSourceCount];
            Array.Fill(m_dataSourceOpacity, 1.0f);
            m_gpuDataSourceOpacity.SetData(m_dataSourceOpacity);

            // Create Eye data buffer
            int eyeStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(EyeData));
            m_eyeData?.Dispose();
            m_eyeData = new ComputeBuffer(2, eyeStride);

            m_centerEyeDataBuffer?.Dispose();
            m_centerEyeDataBuffer = new ComputeBuffer(1, eyeStride);

            m_resourceTracker.LogAndResetStats();
        }

        public void UpdateDataSourceProperties(MirisAssetDataSource[] dataSources)
        {
            if (m_dataSourceOpacity == null)
            {
                return;
            }

            // The loop below writes one entry per data source, so a longer dataSources would run off
            // the end of the opacity array.
            if (dataSources.Length != m_dataSourceOpacity.Length)
            {
                Debug.Assert(false,
                             $"[MirisAssetRenderer] {dataSources.Length} data sources but the "
                             + $"opacity buffer holds {m_dataSourceOpacity.Length} - skipping this update");
                return;
            }

            for (uint dataSourceIndex = 0; dataSourceIndex < dataSources.Length; ++dataSourceIndex)
            {
                // The data source's own opacity is the LOD cross-fade's; multiplying rather than
                // overwriting keeps whole-asset transparency independent of an in-flight fade.
                m_dataSourceOpacity[dataSourceIndex] =
                    dataSources[dataSourceIndex].m_opacity * m_assetOpacityScale;
            }
            m_gpuDataSourceOpacity.SetData(m_dataSourceOpacity);
        }

        private AttributeBuffer[] CollectCpuBuffers(MirisAssetDataSource[] dataSources, AttributeSemantic semantic)
        {
            AttributeBuffer[] attributeBuffers = new AttributeBuffer[dataSources.Length];
            for (int dataSourceIndex = 0; dataSourceIndex < attributeBuffers.Length; ++dataSourceIndex)
            {
                attributeBuffers[dataSourceIndex] = dataSources[dataSourceIndex].GetBuffer(semantic);
            }
            return attributeBuffers;
        }

        // Returns the number of SH coefficients per splat (e.g., 0, 3, 8, or 15).
        // Calculates based on total bytes, element count, and encoding size.
        private int GetShCount(MirisAssetDataSource[] dataSources, AttributeSemantic semantic)
        {
            if (dataSources.Length == 0)
            {
                return 0;
            }

            AttributeBuffer shBuffer = dataSources[0].GetBuffer(AttributeSemantic.SHCoefficients);
            AttributeBuffer posBuffer = dataSources[0].GetBuffer(AttributeSemantic.Position);

            int shTotalBytes = shBuffer.GetTotalBytes();
            int splatCount = posBuffer.GetSplatCount();

            if (splatCount == 0 || shTotalBytes == 0)
            {
                return 0;
            }

            // Determine bytes per RGB triplet based on encoding
            int bytesPerElement;
            AttributeEncoding encoding = shBuffer.GetEncoding();
            switch (encoding)
            {
                case AttributeEncoding.Float32x3:
                    bytesPerElement = 12; // 3 * 4 bytes
                    break;
                case AttributeEncoding.Float16x3:
                    bytesPerElement = 6;  // 3 * 2 bytes
                    break;
                default:
                    bytesPerElement = 12; // Default to float32x3
                    break;
            }

            // Total SH coefficients per splat = totalBytes / (splatCount * bytesPerElement)
            int coefficientsPerSplat = shTotalBytes / (splatCount * bytesPerElement);
            return coefficientsPerSplat;
        }

        public void BuildBuffers(CommandBuffer commandBuffer)
        {
            // The buffer converter must execute before the buffer copier because the copier relies on the output
            // of the converter to perform its operations correctly. Changing this order may result in incorrect behavior.
            m_bufferConverter.Execute(commandBuffer);
            m_bufferCopier.Execute(commandBuffer);
        }

        // Invoke overridable Dispose(bool) function
        public void Dispose()
        {
            this.Dispose(true);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    using (s_destroyResourcesBaseMarker.Auto())
                    {
                        m_renderBuffers.Dispose();

                        DisposeBuffer(ref m_gpuSplatToDataSourceIndex);
                        DisposeBuffer(ref m_gpuDataSourceOpacity);
                        DisposeBuffer(ref m_eyeData);
                        DisposeBuffer(ref m_centerEyeDataBuffer);

                        m_resourceTracker?.Dispose();
                    }
                    m_disposed = true;
                }
            }
        }

        // Destroy a single GPU resource.
        protected static void DisposeBuffer(ref IGpuBuffer gpuBuffer)
        {
            gpuBuffer?.Dispose();
            gpuBuffer = null;
        }

        protected static void DisposeBuffer(ref GraphicsBuffer buffer)
        {
            buffer?.Dispose();
            buffer = null;
        }

        protected static void DisposeBuffer(ref ComputeBuffer buffer)
        {
            buffer?.Dispose();
            buffer = null;
        }

        // ---------------------------------------------------------
        // Camera Utilities
        // ---------------------------------------------------------

        protected void UpdateEyeData(Camera camera, bool renderToTexture=false) 
        {
            // check if we are in playing mode while using the editor. If so, get need to get the transforms for each eye
#if UNITY_EDITOR

            if (Application.isPlaying && camera.stereoEnabled) {
                UpdateSpecificEyeFromCamera(camera, Camera.StereoscopicEye.Left);
                UpdateSpecificEyeFromCamera(camera, Camera.StereoscopicEye.Right);

                UpdateCenterEyeDataFromXRCamera(camera);

            } else {
                // In edit mode, Unity uses the main camera. So, let's set the left and right eye variables to main camera space and projection
                UpdateEyeDataFromNonXRCamera(camera, renderToTexture);
            }

#else
            if (XRSettings.isDeviceActive) {
                // only update the camera eye information if we are the left/mono eye
                // this ensures we are rendering a matching stereo-pair in multi-pass mode
                if (camera.stereoActiveEye == (Camera.MonoOrStereoscopicEye) Camera.StereoscopicEye.Left || 
                    camera.stereoActiveEye == (Camera.MonoOrStereoscopicEye) Camera.MonoOrStereoscopicEye.Mono) {
                    UpdateSpecificEyeFromCamera(camera, Camera.StereoscopicEye.Left);
                    UpdateSpecificEyeFromCamera(camera, Camera.StereoscopicEye.Right);
                    
                    UpdateCenterEyeDataFromXRCamera(camera);
                }
            } else {
                UpdateEyeDataFromNonXRCamera(camera, false);
            }
#endif
            //set eye compute buffer data 
            m_eyeData.SetData(m_eyeDataArray);
            m_centerEyeDataBuffer.SetData(m_centerEyeData);
        }

        protected void UpdateCenterEyeDataFromXRCamera(Camera camera) {
            // Set the view matrix to have the rotation of the left eye, and a position in the middle of the eyes

            var leftEyeView = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
            var rightEyeView = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right);

            var leftEyePosition = leftEyeView.GetPosition();
            var rightEyePosition = rightEyeView.GetPosition();                        
            var centerPosition = (leftEyePosition + rightEyePosition) / 2.0f;

            m_centerEyeData[0].view = leftEyeView;
            m_centerEyeData[0].view[12] = centerPosition.x;
            m_centerEyeData[0].view[13] = centerPosition.y;
            m_centerEyeData[0].view[14] = centerPosition.z;

            var proj = Matrix4x4.Perspective(camera.fieldOfView, camera.aspect, camera.nearClipPlane, camera.farClipPlane);
            m_centerEyeData[0].proj = GL.GetGPUProjectionMatrix(proj, false);
        }

        protected void UpdateSpecificEyeFromCamera(Camera camera, Camera.StereoscopicEye eyeType) 
        {
            int eye = (int) eyeType;
                        
            m_eyeDataArray[eye].view = camera.GetStereoViewMatrix(eyeType);
            Matrix4x4 proj = camera.GetStereoProjectionMatrix(eyeType); 
            m_eyeDataArray[eye].proj = GL.GetGPUProjectionMatrix(proj, false); 
            m_eyeDataArray[eye].position = camera.transform.localToWorldMatrix.GetPosition();  
        }
        
        private void UpdateEyeDataFromNonXRCamera(Camera camera, bool renderToTexture) {            
            Matrix4x4 viewSpace = camera.worldToCameraMatrix;
            Matrix4x4 projectionSpace = GL.GetGPUProjectionMatrix(camera.projectionMatrix, renderToTexture);
            Vector4 cameraPosition = new Vector4(camera.transform.position.x, camera.transform.position.y,
                camera.transform.position.z, 0.0f);
                        
            m_eyeDataArray[0].view = viewSpace;
            m_eyeDataArray[1].view = viewSpace;
            m_eyeDataArray[0].proj = projectionSpace;
            m_eyeDataArray[1].proj = projectionSpace;
            m_eyeDataArray[0].position = cameraPosition;
            m_eyeDataArray[1].position = cameraPosition;

            m_centerEyeData[0].view = viewSpace;
            m_centerEyeData[0].proj = projectionSpace;
            m_centerEyeData[0].position = cameraPosition;
        }

        // ---------------------------------------------------------
        // Renderer execution
        // ---------------------------------------------------------

        public virtual void Run(CommandBuffer commandBuffer, Camera camera, MirisTransform transform) 
        { 
            UpdateEyeData(camera);
        }

        // ---------------------------------------------------------
        // Shader variant selection
        // See https://docs.unity3d.com/Manual/shader-keywords.html
        // ---------------------------------------------------------

        const string c_missingShCoefficientsKeyword = "SHCoefficients_None";

        static protected void EnableShaderKeywords(MirisAssetDataSource dataSource, Material material)
        {
            // Clear all local keywords.
            string[] enabledKeywords = material.shaderKeywords;
            foreach (string keyword in enabledKeywords)
            {
                material.DisableKeyword(keyword);
            }

            // Enable required keywords.
            foreach (AttributeBuffer attributeBuffer in dataSource.GetBuffers())
            {
                material.EnableKeyword(attributeBuffer.GetShaderKeyword());
            }

            // Temporary fix for lack of SH on some assets.
            if (!dataSource.HasBuffer(AttributeSemantic.SHCoefficients))
            {
                material.EnableKeyword(c_missingShCoefficientsKeyword);
            }
        }

        static protected void EnableShaderKeywords(MirisAssetDataSource dataSource, ComputeShader computeShader)
        {
            // Clear all local keywords.
            string[] enabledKeywords = computeShader.shaderKeywords;
            foreach (string keyword in enabledKeywords)
            {
                computeShader.DisableKeyword(keyword);
            }

            // Enable required keywords.
            foreach (AttributeBuffer attributeBuffer in dataSource.GetBuffers())
            {
                computeShader.EnableKeyword(attributeBuffer.GetShaderKeyword());
            }

            // Temporary fix for lack of SH on some assets.
            if (!dataSource.HasBuffer(AttributeSemantic.SHCoefficients))
            {
                computeShader.EnableKeyword(c_missingShCoefficientsKeyword);
            }
        }
    }
}
