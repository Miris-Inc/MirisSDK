// Copyright © 2026 Miris, Inc. All rights reserved.

using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;
using NUnit.Framework;
using System.Threading.Tasks;

using Miris.Runtime;

namespace Miris.Tests
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
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestStreamingHarmonics([ValueSource(nameof(s_SHBand))] int shBand)
        {
            // Setup scene
            GameObject controllerObject = new GameObject("MirisStreamController");
            MirisStreamController streamController = controllerObject.AddComponent<MirisStreamController>();
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;
            streamController.m_fadeDurationSeconds = 0;
            MirisInternalController internalController = controllerObject.AddComponent<MirisInternalController>();
            string contentUrl = Task.Run(() => MirisTestUtils.ResolveContentUrl(streamController, internalController, "arcade", MirisTestUtils.GetTestServerEnvironment())).Result;
            
            GameObject streamObject = new GameObject("Miris Stream");
            MirisStreamTest stream = streamObject.AddComponent<MirisStreamTest>();
            stream.m_streamController = streamController;
            stream.SetUrlFromContentPath(contentUrl);

            stream.gameObject.transform.position = new Vector3(0.0f, -1.0f, 1.0f);
            
            SetupCamera(streamObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);

            // Warm up the frames before querying the GaussianSplatRenderComponent
            for (int frameIndex = 0; frameIndex < 4; ++frameIndex)
            {
                yield return new WaitForEndOfFrame();
            }

            GaussianSplatRenderComponent renderComponent = stream.GetRenderComponents()[0];
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
