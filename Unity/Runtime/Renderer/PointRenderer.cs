// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.XR;

using Unity.Profiling;
using Unity.Profiling.LowLevel;

namespace Miris.Runtime
{
    /// <summary>
    /// PointRenderer performs rendering of gaussian splat centres as points through direct rasterization to a render texture.
    /// It provides a method of rendering that is useful for debugging and inspecting raw splat data.
    /// </summary>

    public class PointRenderer : MirisAssetRenderer
    {
        // ---------------------------------------------------------
        // Rendering Flags
        // ---------------------------------------------------------

        public enum PointDrawMode : int
        {
            White,
            SplatColor,
            SplatScaleAxis,
            SplatFlatness,
            SplatFlatNormal,
            SplatFlatFacing,
            DataSource,
            SphericalHarmonics,
            SphericalHarmonicsLighting
        }

        public PointDrawMode m_pointDrawMode = PointDrawMode.SplatColor;

        public enum SHAxis : int
        {
            X,
            Y,
            Z
        }

        public SHAxis m_pointSHAxis = SHAxis.Y;

        public enum SHChannel : int
        {
            Red,
            Green,
            Blue,
            CombinedPositive,
            CombinedNegative
       }

        public SHChannel m_pointSHChannel = SHChannel.Red;

        // ---------------------------------------------------------
        // Shaders & compute shaders
        // ---------------------------------------------------------

        private ComputeShader m_pointRenderShader;
        private int m_renderPointsKernel;
        private int m_renderPointsDepthKernel;        
        private int m_clearRenderBuffersKernel;    

        // ---------------------------------------------------------
        // Buffers
        // ---------------------------------------------------------

        private RenderTexture m_renderTexture;
        private ComputeBuffer m_renderDepthBuffer;

        // ---------------------------------------------------------
        // Scalars
        // ---------------------------------------------------------

        private int m_renderWidth = 0;
        private int m_renderHeight = 0;
        private int m_eyeIndex = 0;
        public int m_flatnessPercent = 2;

        // ---------------------------------------------------------
        // Flags
        // ---------------------------------------------------------

        public bool m_reverseDepth = false;
        public bool m_stereoRender = false;
        public bool m_depthAttenuate = true;

        // ---------------------------------------------------------
        // State
        // ---------------------------------------------------------

        private bool m_disposed;

        // ---------------------------------------------------------
        // Profiler markers
        // ---------------------------------------------------------

        static string s_profilerPrefix = "[PointRenderer] ";

        // CPU Markers
        static readonly ProfilerMarker s_updateResourcesMarker = new ProfilerMarker(
            s_profilerPrefix + "Update graphics resources"
        );

        static readonly ProfilerMarker s_destroyResourcesMarker = new ProfilerMarker(
            s_profilerPrefix + "Destroy graphics resources"
        );

        static readonly ProfilerMarker s_pointRenderGpuMarker = new ProfilerMarker(
            ProfilerCategory.Render, s_profilerPrefix + "Render Points (GPU)", MarkerFlags.SampleGPU
        );

        // Symbol names used in the shaders.
        private static class ShaderIds
        {
            public static readonly int MirisAssetRT = Shader.PropertyToID("_MirisAssetRT");

            public static readonly int VecScreenParamsID = Shader.PropertyToID("_VecScreenParams");
            public static readonly int VecProjectionParamsID = Shader.PropertyToID("_VecProjectionParams");

            public static readonly int MatrixModelViewProjectionID = Shader.PropertyToID("_MatrixModelViewProjection");
            public static readonly int MatrixProjectionID = Shader.PropertyToID("_MatrixProjection");
            public static readonly int MatrixModelViewID = Shader.PropertyToID("_MatrixModelView");
            public static readonly int MatrixModelViewInverseID = Shader.PropertyToID("_MatrixModelViewInverse");

            public static readonly int NumSplatsID = Shader.PropertyToID("_NumSplats");
            public static readonly int ImageWidthID = Shader.PropertyToID("_ImageWidth");
            public static readonly int ImageHeightID = Shader.PropertyToID("_ImageHeight");
            public static readonly int StereoRenderID = Shader.PropertyToID("_StereoRender");
            public static readonly int ReverseDepthID = Shader.PropertyToID("_ReverseDepth");
            public static readonly int ColorModeID = Shader.PropertyToID("_ColorMode");
            public static readonly int DepthAttenuateID = Shader.PropertyToID("_DepthAttenuate");
            public static readonly int FlatnessPercentID = Shader.PropertyToID("_FlatnessPercent");
            public static readonly int AlphaCullThresholdID = Shader.PropertyToID("_AlphaCullThreshold");
            public static readonly int SHCountID = Shader.PropertyToID("_SHCount");
            public static readonly int SHOrderID = Shader.PropertyToID("_SHOrder");
            public static readonly int SHAxisID = Shader.PropertyToID("_SHAxis");
            public static readonly int SHChannelID = Shader.PropertyToID("_SHChannel");

            public static readonly int SplatToDataSourceIndexID = Shader.PropertyToID("_SplatToDataSourceIndex");

            public static readonly int RenderResultID = Shader.PropertyToID("_RenderResult");
            public static readonly int DepthBufferID = Shader.PropertyToID("_DepthBuffer");
        }

        // ---------------------------------------------------------
        // Constructor
        // ---------------------------------------------------------

        public PointRenderer() { }

        // ---------------------------------------------------------
        // Resource creation
        // ---------------------------------------------------------

        protected override void CreateConstantResources()
        {
            base.CreateConstantResources();

            m_pointRenderShader = ComputeShader.Instantiate((ComputeShader)Resources.Load("Shaders/PointRender"));
            m_renderPointsKernel = m_pointRenderShader.FindKernel("RenderPoints");
            m_renderPointsDepthKernel = m_pointRenderShader.FindKernel("RenderPointsDepth");
            m_clearRenderBuffersKernel = m_pointRenderShader.FindKernel("ClearRenderBuffers");
        }

        // ---------------------------------------------------------
        // Resource management
        // ---------------------------------------------------------

        protected override void UpdateGraphicsResources(MirisAssetDataSource[] dataSources)
        {
            base.UpdateGraphicsResources(dataSources);
        }

        protected override void UpdateComputeResources(MirisAssetDataSource[] dataSources)
        {
            base.UpdateComputeResources(dataSources);

            EnableShaderKeywords(dataSources[0], m_pointRenderShader);
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
                        m_renderTexture?.Release();
                        UnityEngine.Object.DestroyImmediate(m_renderTexture);
                        m_renderDepthBuffer?.Dispose();
                    }
                }
                this.m_disposed = true;
            }
            base.Dispose(disposing);
        }

        // ---------------------------------------------------------
        // Renderer execution
        // ---------------------------------------------------------

        public override void Run(CommandBuffer commandBuffer, Camera camera, MirisTransform transform )
        {
            base.Run(commandBuffer, camera, transform);

            m_eyeIndex = 1;
            m_stereoRender = camera.stereoEnabled;

            if (Application.platform == RuntimePlatform.Android)
            {
                m_reverseDepth = true;
            }

            if (camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left ||
                camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Mono)
            {
                m_eyeIndex = 0;
            }

            // render
            DispatchRenderPointsKernel(commandBuffer, camera, transform);
        }

        // ---------------------------------------------------------
        // Compute utilities
        // ---------------------------------------------------------

        private Vector4 SetCameraProjectionParamsForShader(CommandBuffer commandBuffer, Camera camera, ComputeShader computeShader)
        {
            // get and set camera projection data
            Vector4 projectionParams = new Vector4(
                -1.0f,
                camera.nearClipPlane,
                camera.farClipPlane,
                1.0f / camera.farClipPlane
            );
            commandBuffer.SetComputeVectorParam(computeShader, ShaderIds.VecProjectionParamsID, projectionParams);
            return projectionParams;
        }

        private Vector4 SetScreenParamsForShader(CommandBuffer commandBuffer, Camera camera, ComputeShader computeShader)
        {
            int screenW = camera.pixelWidth;
            int screenH = camera.pixelHeight;
            Vector4 screenParams = new Vector4(screenW, screenH, 0, 0);
            commandBuffer.SetComputeVectorParam(computeShader, ShaderIds.VecScreenParamsID, screenParams);
            return screenParams;
        }

        private void SetCommonRenderingParametersForShader(CommandBuffer commandBuffer, Camera camera, ComputeShader computeShader)
        {
            int screenW = camera.pixelWidth;
            int screenH = camera.pixelHeight;

            if (m_xrUtils.IsStereo())
            {
                screenW = XRSettings.eyeTextureWidth;
                screenH = XRSettings.eyeTextureHeight;
            } 

            if (screenW != m_renderWidth || screenH != m_renderHeight)
            {
                // recreate the render texture if the screen dimensions have changed
                m_renderTexture?.Release();
                m_renderTexture = new RenderTexture(screenW, screenH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                m_renderTexture.enableRandomWrite = true;
                m_renderTexture.Create();
                // recreate the depth buffer
                m_renderDepthBuffer?.Dispose();
                m_renderDepthBuffer = new ComputeBuffer(screenW * screenH, sizeof(uint));

                // record the render dimensions
                m_renderWidth = screenW;
                m_renderHeight = screenH;
            }

            // bind scalars
            commandBuffer.SetComputeIntParam(computeShader, ShaderIds.ImageWidthID, m_renderWidth);
            commandBuffer.SetComputeIntParam(computeShader, ShaderIds.ImageHeightID, m_renderHeight);
        }

        // ---------------------------------------------------------
        // Compute dispatch
        // ---------------------------------------------------------

        public void DispatchRenderPointsKernel(CommandBuffer commandBuffer, Camera camera, MirisTransform transform)
        {
            commandBuffer.BeginSample(s_pointRenderGpuMarker);

            Vector4 projectionParams =
                SetCameraProjectionParamsForShader(commandBuffer, camera, m_pointRenderShader);
            SetScreenParamsForShader(commandBuffer, camera, m_pointRenderShader);
            SetCommonRenderingParametersForShader(commandBuffer, camera, m_pointRenderShader);

            // set scalars
            commandBuffer.SetComputeIntParam(m_pointRenderShader, ShaderIds.NumSplatsID, m_splatCount);
            commandBuffer.SetComputeIntParam(m_pointRenderShader, ShaderIds.ColorModeID, (int)m_pointDrawMode);
            commandBuffer.SetComputeFloatParam(m_pointRenderShader, ShaderIds.AlphaCullThresholdID,
                m_alphaCullingThreshold);
            commandBuffer.SetComputeIntParam(m_pointRenderShader, ShaderIds.DepthAttenuateID,
                m_depthAttenuate ? 1 : 0);
            commandBuffer.SetComputeIntParam(m_pointRenderShader, ShaderIds.FlatnessPercentID, m_flatnessPercent);
            commandBuffer.SetComputeIntParam(m_pointRenderShader, ShaderIds.SHCountID, m_shCount);
            commandBuffer.SetComputeIntParam(m_pointRenderShader, ShaderIds.SHOrderID, m_shOrder);
            commandBuffer.SetComputeIntParam(m_pointRenderShader, ShaderIds.SHAxisID, (int)m_pointSHAxis);
            commandBuffer.SetComputeIntParam(m_pointRenderShader, ShaderIds.SHChannelID, (int)m_pointSHChannel);

            // set matrices
            Matrix4x4 modelView = m_eyeDataArray[m_eyeIndex].view * transform.localToWorldMatrix;
            commandBuffer.SetComputeMatrixParam(m_pointRenderShader, ShaderIds.MatrixModelViewID, modelView);
            commandBuffer.SetComputeMatrixParam(m_pointRenderShader, ShaderIds.MatrixModelViewInverseID,
                modelView.inverse);
            Matrix4x4 invertYMatrix = Matrix4x4.identity;
            invertYMatrix[1, 1] = projectionParams.x;
            Matrix4x4 eyeProjSpace = invertYMatrix * m_eyeDataArray[m_eyeIndex].proj;
            Matrix4x4 modelViewProjection = eyeProjSpace * modelView;
            commandBuffer.SetComputeMatrixParam(m_pointRenderShader, ShaderIds.MatrixModelViewProjectionID,
                modelViewProjection);
            
            commandBuffer.SetComputeMatrixParam(m_pointRenderShader, "_MatrixObjectToWorld", transform.localToWorldMatrix);
            commandBuffer.SetComputeMatrixParam(m_pointRenderShader, "_MatrixWorldToObject", transform.worldToLocalMatrix);

            Vector4 cameraPosition = camera.transform.localToWorldMatrix.GetPosition();
            commandBuffer.SetComputeVectorParam(m_pointRenderShader, "_CameraPosition", cameraPosition);

            // clear buffers
            {
                int tilesX = (m_renderWidth / 8) + 1;
                int tilesY = (m_renderHeight / 8) + 1;
                commandBuffer.SetComputeTextureParam(m_pointRenderShader, m_clearRenderBuffersKernel,
                    ShaderIds.RenderResultID, m_renderTexture);
                commandBuffer.SetComputeBufferParam(m_pointRenderShader, m_clearRenderBuffersKernel, ShaderIds.DepthBufferID, m_renderDepthBuffer);
                commandBuffer.DispatchCompute(m_pointRenderShader, m_clearRenderBuffersKernel, tilesX, tilesY, 1);
            }

            // render depth
            {
                m_renderBuffers.m_gpuPositions.SetBufferOnComputeShader(commandBuffer, m_pointRenderShader,
                    m_renderPointsDepthKernel);
                m_renderBuffers.m_gpuColors.SetBufferOnComputeShader(commandBuffer, m_pointRenderShader,
                    m_renderPointsDepthKernel);
                commandBuffer.SetComputeBufferParam(m_pointRenderShader, m_renderPointsDepthKernel, ShaderIds.DepthBufferID, m_renderDepthBuffer);
                var (threadGroupCountX, _, _) = ComputeKernelUtils.CalculateThreadGroupCount(m_pointRenderShader,
                    m_renderPointsDepthKernel, m_splatCount);
                commandBuffer.DispatchCompute(m_pointRenderShader, m_renderPointsDepthKernel, threadGroupCountX, 1,
                    1);
            }

            // render points
            {
                m_renderBuffers.m_gpuPositions.SetBufferOnComputeShader(commandBuffer, m_pointRenderShader,
                    m_renderPointsKernel);
                m_renderBuffers.m_gpuScales.SetBufferOnComputeShader(commandBuffer, m_pointRenderShader,
                    m_renderPointsKernel);
                m_renderBuffers.m_gpuOrientations.SetBufferOnComputeShader(commandBuffer, m_pointRenderShader,
                    m_renderPointsKernel);
                m_renderBuffers.m_gpuColors.SetBufferOnComputeShader(commandBuffer, m_pointRenderShader,
                    m_renderPointsKernel);
                // send sh data if available
                if (m_shCount > 0 && m_renderBuffers.m_gpuSHCoefficients != null) {
                    m_renderBuffers.m_gpuSHCoefficients.SetBufferOnComputeShader(commandBuffer, m_pointRenderShader,
                        m_renderPointsKernel);
                }

                commandBuffer.SetComputeBufferParam(m_pointRenderShader, m_renderPointsKernel,
                    ShaderIds.SplatToDataSourceIndexID, m_gpuSplatToDataSourceIndex);

                commandBuffer.SetComputeTextureParam(m_pointRenderShader, m_renderPointsKernel,
                    ShaderIds.RenderResultID, m_renderTexture);
                commandBuffer.SetComputeBufferParam(m_pointRenderShader, m_renderPointsKernel, ShaderIds.DepthBufferID, m_renderDepthBuffer);
                var (threadGroupCountX, _, _) =
                    ComputeKernelUtils.CalculateThreadGroupCount(m_pointRenderShader, m_renderPointsKernel,
                        m_splatCount);

                commandBuffer.DispatchCompute(m_pointRenderShader, m_renderPointsKernel, threadGroupCountX, 1, 1);
            }

            // blit the result from the texture onto the temp RT
            // TODO modify this so it will work in single-pass rendering 
            //   since direct Blits are not supported.
            commandBuffer.Blit(m_renderTexture, ShaderIds.MirisAssetRT);

            commandBuffer.EndSample(s_pointRenderGpuMarker);
                
        }
    }
}