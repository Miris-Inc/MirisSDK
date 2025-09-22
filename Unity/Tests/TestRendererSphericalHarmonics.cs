
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;
using NUnit.Framework;

using Aqua.Runtime;

namespace Aqua.Tests
{
    public class TestRendererSphericalHarmonics : RendererTestBase
    {

        // Number of SH bands
        static int[] s_SHBand = { 1, 2, 3 };

        static private void SetSHCoeffParameters(GaussianSplatRenderComponent splatRenderComponent, int shBand)
        {
            // set up SH Parameters
            splatRenderComponent.m_drawMode = GeometryRenderer.GeometryDrawMode.SHOnly;
            splatRenderComponent.m_SHOrder = shBand;

            // other parameters can go here
        }

        [UnityTest]
        [Ignore("Current drop files do not have SH data.")]
        public IEnumerator TestStreamingHarmonics([ValueSource(nameof(s_SHBand))] int shBand)
        {
            // Setup scene
            GameObject controllerObject = new GameObject("MirisStreamController");
            MirisStreamController streamController = controllerObject.AddComponent<MirisStreamController>();
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;
            streamController.m_fadeDurationSeconds = 0;

            GameObject streamObject = new GameObject("Miris Stream");
            MirisStream stream = streamObject.AddComponent<MirisStream>();
            stream.SetUrlFromContentPath("garden/10x10");

            SetupCamera(streamObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);

            // Warm up the frames before querying the GaussianSplatRenderComponent
            for (int frameIndex = 0; frameIndex < 4; ++frameIndex)
            {
                yield return new WaitForEndOfFrame();
            }

            GaussianSplatRenderComponent renderComponent = streamObject.GetComponentInChildren<GaussianSplatRenderComponent>();
            Assert.IsNotNull(renderComponent);
            renderComponent.m_renderPipeline = GaussianSplatRenderComponent.Pipeline.Geometry;
            SetSHCoeffParameters(renderComponent, shBand);

            // Compare render to reference
            // Need to wait 4 frames to warm-up
            // 1: MirisStreamController Start
            // 2: MirisStreamController Update, triggering blocking scene execution
            // 3: MirisStreamController Update, syncing changes to Unity
            // 4: Create graphics resources and render
            yield return RenderComparison(camera, graphicsTestSettings, $"ReferenceImages/TestStreamingHarmonics_{shBand}_", warmUpFrames: 4);
        }
    }


}
