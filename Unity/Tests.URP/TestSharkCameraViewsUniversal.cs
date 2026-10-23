// Copyright © 2026 Miris, Inc. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

using NUnit.Framework;

namespace Miris.Tests
{
    // TestSharkCameraViews again, with URP as the active pipeline: the per-camera render then hangs
    // off RenderPipelineManager, and the composite is SharkUniversalCompositePass.
    public class TestSharkCameraViewsUniversal : TestSharkCameraViews
    {
        RenderPipelineAsset m_previousDefaultPipeline;
        RenderPipelineAsset m_previousQualityPipeline;
        UniversalRenderPipelineAsset m_pipeline;

        [SetUp]
        public void UseUniversalPipeline()
        {
            m_previousDefaultPipeline = GraphicsSettings.defaultRenderPipeline;
            m_previousQualityPipeline = QualitySettings.renderPipeline;
            m_pipeline = UniversalRenderPipelineAsset.Create();
            GraphicsSettings.defaultRenderPipeline = m_pipeline;
            QualitySettings.renderPipeline = m_pipeline;

#if !UNITY_6000_4_OR_NEWER
            // Compatibility Mode never runs the composite pass, so the test would only see a blank
            // camera. Restored by hand: NUnit skips TearDown when SetUp fails.
            RenderGraphSettings renderGraph = GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
            if (renderGraph != null && renderGraph.enableRenderCompatibilityMode)
            {
                RestorePipeline();
                Assert.Fail("URP's Compatibility Mode is on in this project");
            }
#endif
        }

        [TearDown]
        public void RestorePipeline()
        {
            GraphicsSettings.defaultRenderPipeline = m_previousDefaultPipeline;
            QualitySettings.renderPipeline = m_previousQualityPipeline;
            Object.DestroyImmediate(m_pipeline);
        }
    }
}
