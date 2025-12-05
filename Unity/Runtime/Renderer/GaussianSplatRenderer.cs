// Copyright © 2024 Miris. All rights reserved.

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
    /// GaussianSplatRenderer is the base class for gaussian splat rendering.
    ///
    /// It provides common dataSource and buffer management functionality 
    /// as well as common utilities from which a specialized renderer can be derived. 
    ///
    /// </summary>

    // Core Gaussian Splat rendering resources & logic 
    public class GaussianSplatRenderer : IDisposable
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
        public int m_shOrder = 0;

        // ---------------------------------------------------------
        // State
        // ---------------------------------------------------------

        private bool m_disposed;

        // ---------------------------------------------------------
        // Profiling
        // ---------------------------------------------------------

        static string s_profilerBasePrefix = "[GaussianSplatRenderer] ";

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

        public virtual void SetSHOrder(int order)
        {
            if (order >= 0 && order <= 3)
            {
                m_shOrder = order;
            }
        }

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        public GaussianSplatRenderer()
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

        public void UpdateResources(GaussianSplatDataSource[] dataSources)
        {
            //MirisDebug.Log($"[GaussianSplatsRenderer] Updating graphics resources for {dataSources.Length} data sources");

            using (s_createResourcesBaseMarker.Auto())
            {
                m_splatCount = 0;
                foreach (var dataSource in dataSources)
                {
                    m_splatCount += dataSource.GetSplatCount();
                }

                if (m_splatCount > 0)
                {
                    UpdateGraphicsResources(dataSources);
                    UpdateComputeResources(dataSources);
                }

                foreach (var dataSource in dataSources)
                {
                    dataSource.m_dirty = false;
                }
            }
        }

        protected virtual void UpdateComputeResources(GaussianSplatDataSource[] dataSources) { }

        private AttributeBuffer[] CreateAndTrackGpuBuffer(GaussianSplatDataSource[] dataSources, AttributeSemantic semantic, int shaderId, int textureWidthShaderId, ref IGpuBuffer gpuBuffer, string name)
        {
            AttributeBuffer[] buffers = CollectCpuBuffers(dataSources, semantic);
            GpuBufferFactory.CreateGpuBuffer(
                buffers,
                shaderId,
                textureWidthShaderId,
                ref gpuBuffer,
                name
            );
            m_resourceTracker.Track(dataSources, semantic, m_bufferConverter);
            m_resourceTracker.Merge(dataSources, semantic, gpuBuffer, m_bufferCopier);
            return buffers;
        }
        protected virtual void UpdateGraphicsResources(GaussianSplatDataSource[] dataSources)
        {
            // SHCoefficients (optional)
            if (dataSources[0].HasBuffer(AttributeSemantic.SHCoefficients))
            {
                CreateAndTrackGpuBuffer(dataSources,AttributeSemantic.SHCoefficients, ShaderIds.SHCoefficients, ShaderIds.SHCoefficientsTextureWidth, ref m_renderBuffers.m_gpuSHCoefficients, "shBuffer");
                int lastShCount = m_shCount;
                m_shCount = GetShCount(dataSources, AttributeSemantic.SHCoefficients);
                if (lastShCount != m_shCount) MirisDebug.Log($"[GaussianSplatRenderer] dataSource contains {m_shCount} SH coefficients.");
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
                if (m_resourceTracker.Contains(positionBuffers[index].GetAquaHash()))
                {
                    copiedSplatCount += positionBuffers[index].GetElementCount();
                }
            }
            Debug.Assert(copiedSplatCount == m_splatCount, $"[GaussianSplatRenderer] UpdateGraphicsResources expected {m_splatCount} total splats but only {copiedSplatCount} were copied to GPU buffers");
            m_splatCount = copiedSplatCount;
            
            // Create splat index -> data source index buffer.  So we can look up per-data-source properties in the shaders.
            m_dataSourceCount = positionBuffers.Length;
            // uint[] splatToDataSourceIndex = new uint[m_splatCount];
            int currentDataSourceIndexSize = m_splatToDataSourceIndex.Length;
            if (currentDataSourceIndexSize < m_splatCount)
            {
                MirisDebug.Log($"[GaussianSplatRenderer] allocating space for {m_splatCount} dataSource index array items mapping {m_dataSourceCount} sources");

                // allocate a new buffer for storing the dataSourceIndex map in 
                // large chunks to avoid constant reallocations 
                const int chunkSize = 1024 * 256;
                int newDataSourceIndexSize = ((m_splatCount / chunkSize) + 1) * chunkSize;
                m_splatToDataSourceIndex = new uint[newDataSourceIndexSize];
            }
            int splatIndexOffset = 0;
            for (uint dataSourceIndex = 0; dataSourceIndex < m_dataSourceCount; ++dataSourceIndex)
            {
                AttributeBuffer attrBuffer = positionBuffers[dataSourceIndex];
                if (m_resourceTracker.Contains(positionBuffers[dataSourceIndex].GetAquaHash()))
                {
                    int splatCount = attrBuffer.GetElementCount();
                    Array.Fill(m_splatToDataSourceIndex, dataSourceIndex, splatIndexOffset, splatCount);
                    splatIndexOffset += splatCount;
                }
            }

            m_gpuSplatToDataSourceIndex?.Dispose();
            m_gpuSplatToDataSourceIndex = new GraphicsBuffer(
                GraphicsBuffer.Target.Index,
                m_splatToDataSourceIndex.Length,
                Marshal.SizeOf(typeof(uint))
            );
            m_gpuSplatToDataSourceIndex.SetData(m_splatToDataSourceIndex);

            // Create per-data-source opacity
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

        public void UpdateDataSourceProperties(GaussianSplatDataSource[] dataSources)
        {
            Debug.Assert(m_dataSourceOpacity != null);
            Debug.Assert(dataSources.Length == m_dataSourceOpacity.Length, $"data sources length: {dataSources.Length}, opacity array length: {m_dataSourceOpacity.Length}");
            for (uint dataSourceIndex = 0; dataSourceIndex < dataSources.Length; ++dataSourceIndex)
            {
                m_dataSourceOpacity[dataSourceIndex] = dataSources[dataSourceIndex].m_opacity;
            }
            m_gpuDataSourceOpacity.SetData(m_dataSourceOpacity);
        }

        private AttributeBuffer[] CollectCpuBuffers(GaussianSplatDataSource[] dataSources, AttributeSemantic semantic)
        {
            AttributeBuffer[] attributeBuffers = new AttributeBuffer[dataSources.Length];
            for (int dataSourceIndex = 0; dataSourceIndex < attributeBuffers.Length; ++dataSourceIndex)
            {
                attributeBuffers[dataSourceIndex] = dataSources[dataSourceIndex].GetBuffer(semantic);
            }
            return attributeBuffers;
        }

        private int GetShCount(GaussianSplatDataSource[] dataSources, AttributeSemantic semantic)
        {
            int coefficientsPerSplat = 0;

            if (dataSources.Length > 0)
            {

                int totalBytes = dataSources[0].GetBuffer(AttributeSemantic.SHCoefficients).GetTotalBytes();
                int elementCount = dataSources[0].GetBuffer(AttributeSemantic.Position).GetElementCount();
                int sizeOfCoefficients = dataSources[0].GetBuffer(AttributeSemantic.SHCoefficients).GetTotalBytes() / dataSources[0].GetBuffer(AttributeSemantic.SHCoefficients).GetElementCount() / 3;

                coefficientsPerSplat = totalBytes / (elementCount * sizeOfCoefficients) / 3;
            }

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

        public virtual void Run(CommandBuffer commandBuffer, Camera camera, AquaTransform transform) 
        { 
            UpdateEyeData(camera);
        }

        // ---------------------------------------------------------
        // Shader variant selection
        // See https://docs.unity3d.com/Manual/shader-keywords.html
        // ---------------------------------------------------------

        const string c_missingShCoefficientsKeyword = "SHCoefficients_None";

        static protected void EnableShaderKeywords(GaussianSplatDataSource dataSource, Material material)
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

        static protected void EnableShaderKeywords(GaussianSplatDataSource dataSource, ComputeShader computeShader)
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
