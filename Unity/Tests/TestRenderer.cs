// Copyright © 2026 Miris, Inc. All rights reserved.

using System.Collections;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;

using Miris.Runtime;
using NUnit.Framework;
using UnityEngine.Rendering;

namespace Miris.Tests
{
    public class TestRenderer : RendererTestBase {
        private int m_warmupFrames = 48;
        
        private void CreateMirisStream(
            out MirisStreamController outStreamController,
            out MirisStreamTest stream,
            string assetName = "garden"
        )
        {
            GameObject controllerObject = new GameObject("Miris Stream Controller");
            MirisStreamController streamController = controllerObject.AddComponent<MirisStreamController>();
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;
            streamController.m_fadeDurationSeconds = 0;

            MirisInternalController internalController = controllerObject.AddComponent<MirisInternalController>();
            string contentUrl = Task.Run(() => MirisTestUtils.ResolveContentUrl(streamController, internalController, assetName, MirisTestUtils.GetTestServerEnvironment())).Result;

            GameObject streamObject = new GameObject("Miris Stream");
            stream = streamObject.AddComponent<MirisStreamTest>();
            stream.m_streamController = streamController;
            stream.SetUrlFromContentPath(contentUrl);

            outStreamController = streamController;
        }
        
        protected void CreateMirisStreamById(
            out MirisStreamController outStreamController,
            out MirisStreamTest stream,
            string assetId = "4717d7ac-20e6-4a70-975c-f86da4d43256" // tokyo asset id
        )
        {
            GameObject controllerObject = new GameObject("Miris Stream Controller");
            MirisStreamController streamController = controllerObject.AddComponent<MirisStreamController>();
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;
            streamController.m_fadeDurationSeconds = 0;

            // Set server environment to Production to resolve asset IDs from remote server
            // Using the key from Config/aqua-config for test access - This is my key (Harold)
            // TODO: Should probably use Develop or some other non-Prod ENV, and should probably acquire viewer key from environment variable
            MirisInternalController internalController = controllerObject.AddComponent<MirisInternalController>();
            internalController.GetEnvManager().SetEnv("Prod", "541e02df-0225-4e89-8e19-a0938db52d9b");

            GameObject streamObject = new GameObject("Miris Stream");
            stream = streamObject.AddComponent<MirisStreamTest>();
            stream.m_streamController = streamController;
            stream.m_assetId = assetId;
            outStreamController = streamController;
        }

        protected IEnumerator SetStreamRenderPipeline(MirisStream stream, MirisAssetRenderComponent.Pipeline pipeline = MirisAssetRenderComponent.Pipeline.Geometry, int numWarmupFrames=4)
        {
            // Need to wait at least 4 frames to warm-up
            // 1: MirisStreamController Start
            // 2: MirisStreamController Update, triggering blocking scene execution
            // 3: MirisStreamController Update, syncing changes to Unity
            // 4: Create graphics resources and render
            for (int waitFrame = 0; waitFrame < numWarmupFrames; ++waitFrame)
            {
                yield return new WaitForEndOfFrame();
            }

            MirisAssetRenderComponent renderComponent = stream.GetRenderComponents()[0];
            Assert.IsNotNull(renderComponent);
            renderComponent.m_renderPipeline = pipeline;
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestGeometryPath()
        {
            // Test that we can stream directly from a geometry file (.drop)
            CreateMirisStream(out MirisStreamController _, out MirisStreamTest stream, assetName: "tokyo");

            SetStreamRenderPipeline(stream);

            stream.gameObject.transform.localScale = new Vector3(3, 3, 3);
            stream.m_url = MirisTestUtils.BuildDirectContentUrl("conditioned/tokyo/1x1/0_0_0/12/0_0_0-12.drop");
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            
            camera.gameObject.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
            camera.gameObject.transform.LookAt(stream.gameObject.transform.position + new Vector3(0, 0, 1));
            
            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestGeometryPath", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestStreamingGarden()
        {
            // Setup scene
            CreateMirisStream(out MirisStreamController _, out MirisStreamTest stream);
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);
            
            camera.gameObject.transform.position = new Vector3(0.0f, 1.0f, 0.5f);
            camera.gameObject.transform.LookAt(stream.gameObject.transform.position + new Vector3(0, 0, 2));
        
            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestStreamingGarden", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestStreamingTokyoUSDA()
        {
            // Setup scene
            CreateMirisStream(out MirisStreamController _, out MirisStreamTest stream, assetName: "tokyo");
            
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            camera.gameObject.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
            camera.gameObject.transform.LookAt(stream.gameObject.transform.position + new Vector3(0, 0, 1));
            
            SetStreamRenderPipeline(stream);

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestStreamingTokyoUSDA", warmUpFrames: 48);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestStreamingCamera()
        {
            // Setup scene
            CreateMirisStream(out MirisStreamController streamController, out MirisStreamTest stream, assetName: "market");
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream, MirisAssetRenderComponent.Pipeline.Geometry, 24);

            stream.gameObject.transform.position = new Vector3(-4.5f, -2.0f, 2.0f);
            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestStreamingCamera", warmUpFrames: 24);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestStreamingCameraFOV()
        {
            // Setup scene
            CreateMirisStream(out MirisStreamController streamController, out MirisStreamTest stream, assetName: "market");
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream, MirisAssetRenderComponent.Pipeline.Geometry, m_warmupFrames);
            
            stream.gameObject.transform.position = new Vector3(-4.5f, -2.0f, 2.0f);
            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestStreamingCameraFOV", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestGaussianSigmaThreshold()
        {
            CreateMirisStream(out MirisStreamController _, out MirisStreamTest stream);
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);
           
            camera.gameObject.transform.position = new Vector3(0.0f, 1.0f, 0.5f);
            camera.gameObject.transform.LookAt(stream.gameObject.transform.position + new Vector3(0, 0, 2)); 
            
            for (int waitFrame = 0; waitFrame < m_warmupFrames; ++waitFrame)
            { 
                yield return new WaitForEndOfFrame();
            }
            
            MirisAssetRenderComponent renderComponent = stream.GetRenderComponents()[0];
            Assert.IsNotNull(renderComponent);
            renderComponent.m_gaussianSigmaThreshold = 2.0f;

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestGaussianSigmaThreshold", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestLodHeatMap()
        {
            // Setup scene & camera.
            CreateMirisStream(out MirisStreamController _, out MirisStreamTest stream);
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);
           
            camera.gameObject.transform.position = new Vector3(0.0f, 1.0f, 0.5f);
            camera.gameObject.transform.LookAt(stream.gameObject.transform.position + new Vector3(0, 0, 2)); 
                        
            for (int waitFrame = 0; waitFrame < m_warmupFrames; ++waitFrame)
            { 
                yield return new WaitForEndOfFrame();
            }

            // Find renderer component and set draw mode to LodHeatMap
            MirisAssetRenderComponent renderComponent = stream.GetRenderComponents()[0];
            Assert.IsNotNull(renderComponent);
            
            renderComponent.m_drawMode = GeometryRenderer.GeometryDrawMode.LodHeatMap;

            // Perform comparison render.
            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestLodHeatMap", warmUpFrames: m_warmupFrames);
        }
        
        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestNearClipThreshold()
        {
            CreateMirisStream(out MirisStreamController _, out MirisStreamTest stream);
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);
            
            camera.gameObject.transform.position = new Vector3(0.0f, 1.0f, 0.5f);
            camera.gameObject.transform.LookAt(stream.gameObject.transform.position + new Vector3(0, 0, 2));
            
            for (int waitFrame = 0; waitFrame < m_warmupFrames; ++waitFrame)
            { 
                yield return new WaitForEndOfFrame();
            }
            
            MirisAssetRenderComponent renderComponent = stream.GetRenderComponents()[0];
            Assert.IsNotNull(renderComponent);
            renderComponent.m_nearClipThreshold = 5.0f;

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestNearClipThreshold", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestDepthBuffer()
        {
            CreateMirisStream(out MirisStreamController _, out MirisStreamTest stream);
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);

            // add geometry in front of and behind test splats asset
            GameObject cubeFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeFront.transform.localScale = new Vector3(0.5f, 3.0f, 0.5f);
            cubeFront.transform.position = new Vector3(-1.0f, 0.0f, 0.0f);
            GameObject cubeBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeBack.transform.localScale = new Vector3(1.0f, 10.0f, 1.0f);
            cubeBack.transform.position = new Vector3(-0.5f, 0.0f, 10.0f);

            // If we are testing using URP Pipeline, we need to set the Game Object's to use the
            // correct URP Lit material that resembles the BIRP materials
            if (GraphicsSettings.currentRenderPipeline != null) {
                var lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                lit.SetColor("_BaseColor", Color.white);
                lit.SetFloat("_Metallic", 0.0f);
                lit.SetFloat("_Smoothness", 0.5f);
                cubeFront.GetComponent<Renderer>().sharedMaterial = lit;
                cubeBack.GetComponent<Renderer>().sharedMaterial = lit;
            }
            
            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestDepthBuffer", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestFadeLargeSplats()
        {
            CreateMirisStream(out MirisStreamController _, out MirisStreamTest stream);
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);

            camera.gameObject.transform.position = new Vector3(0.0f, 1.0f, 0.5f);
            camera.gameObject.transform.LookAt(stream.gameObject.transform.position+ new Vector3(0, 0, 2)); 
            
            for (int waitFrame = 0; waitFrame < m_warmupFrames; ++waitFrame)
            { 
                yield return new WaitForEndOfFrame();
            } 
           
            MirisAssetRenderComponent renderComponent = stream.GetRenderComponents()[0];
            Assert.IsNotNull(renderComponent);
            renderComponent.m_fadeLargeSplats = true;

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestFadeLargeSplats", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestMultipleStreams()
        {
            // Setup scene (and delete the original stream)
            CreateMirisStream(out MirisStreamController _, out MirisStreamTest stream, assetName: "tokyo");
            GameObject.Destroy(stream.gameObject);
            // Create a 3x3 grid of streams 
            const int rowCount = 3;
            const int colCount = 3;
            const float spacing = 2.0f;
            for (int row = 0; row < rowCount; ++row)
            {
                for (int col = 0; col < colCount; ++col)
                {
                    GameObject newStreamObject = new GameObject($"Miris Stream ({row}, {col})");
                    MirisStreamTest newStream = newStreamObject.AddComponent<MirisStreamTest>();
                    newStream.m_url = stream.m_url;
                    newStream.transform.localPosition = new Vector3(
                        -((rowCount - 1) * spacing / 2.0f) + row * spacing,
                        -((colCount - 1) * spacing / 2.0f) + col * spacing,
                        1.0f
                    );
                    
                    SetStreamRenderPipeline(newStream);
                    
                }
            }

            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);

            camera.transform.localPosition = new Vector3(0, 0, -3);
            camera.transform.localEulerAngles = new Vector3(0, 0, 0);

            for (int waitFrame = 0; waitFrame < m_warmupFrames; ++waitFrame)
            { 
                yield return new WaitForEndOfFrame();
            }
            
            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestMultipleStreams", warmUpFrames: 128);
        }
        
        [UnityTest]
        [Ignore("This test is skipped temporarily until we can figure out how to stream using asset id")]
        public IEnumerator TestStreamingTokyoById()
        {
            // Setup scene
            CreateMirisStreamById(out MirisStreamController _, out MirisStreamTest stream);
            
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            camera.gameObject.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
            camera.gameObject.transform.LookAt(stream.gameObject.transform.position + new Vector3(0, 0, 1));
            
            SetStreamRenderPipeline(stream);

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestStreamingTokyoById", warmUpFrames: 48);
        }
    }
}
