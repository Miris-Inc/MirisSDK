// Copyright © 2025 Miris. All rights reserved.

using System.Collections;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;

using Aqua.Runtime;
using NUnit.Framework;

namespace Aqua.Tests
{
    public class TestRenderer : RendererTestBase {
        private int m_warmupFrames = 48;
        
        private void CreateMirisStream(
            out MirisStreamController streamController,
            out MirisStream stream,
            string assetName = "garden",
            bool useDrop = true
        )
        {
            string contentUrl = Task.Run(() => AquaTestUtils.ResolveContentUrl(assetName, "Local_UNSECURE")).Result;

            GameObject controllerObject = new GameObject("Miris Stream Controller");
            streamController = controllerObject.AddComponent<MirisStreamController>();
            streamController.m_useDropFiles = useDrop;
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;
            streamController.m_fadeDurationSeconds = 0;

            GameObject streamObject = new GameObject("Miris Stream");
            stream = streamObject.AddComponent<MirisStream>();
            stream.SetUrlFromContentPath(contentUrl);
        }

        protected IEnumerator SetStreamRenderPipeline(MirisStream stream, GaussianSplatRenderComponent.Pipeline pipeline = GaussianSplatRenderComponent.Pipeline.Geometry, int numWarmupFrames=4)
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

            GaussianSplatRenderComponent renderComponent = stream.GetComponentInChildren<GaussianSplatRenderComponent>();
            Assert.IsNotNull(renderComponent);
            renderComponent.m_renderPipeline = pipeline;
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestGeometryPath()
        {
            // Test that we can stream directly from a geometry file (.drop)
            CreateMirisStream(out MirisStreamController _, out MirisStream stream);

            SetStreamRenderPipeline(stream);

            stream.gameObject.transform.localScale = new Vector3(3, 3, 3);
            stream.m_url = "http://{devlocalhost}:3003/content/conditioned/tokyo/1x1/0_0_0/12/0_0_0-12.drop";
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestGeometryPath", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestStreamingGarden()
        {
            // Setup scene
            CreateMirisStream(out MirisStreamController _, out MirisStream stream);
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);

            camera.gameObject.transform.LookAt(stream.gameObject.transform.position);
        
            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestStreamingGarden", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [Ignore("Disabling Failing tests and creating ticket so they are re-enabled")]
        public IEnumerator TestStreamingTokyoUSDA()
        {
            // Setup scene
            CreateMirisStream(out MirisStreamController _, out MirisStream stream, assetName: "tokyoUSDA");
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);

            stream.gameObject.transform.position = new Vector3(-0.4f, 2f, 0.85f);

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestStreamingTokyoUSDA", warmUpFrames: 48);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestStreamingCamera()
        {
            // Setup scene
            CreateMirisStream(out MirisStreamController streamController, out MirisStream stream, assetName: "market");
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream, GaussianSplatRenderComponent.Pipeline.Geometry, 24);

            stream.gameObject.transform.position = new Vector3(-4.5f, -2.0f, 2.0f);
            BaseObjectAdapter adapter = streamController.GetAdapter(SceneObjectType.Camera);
            if (adapter != null)
            {
                CameraAdapter cameraAdapter = (CameraAdapter)adapter;
                Camera currentCamera = cameraAdapter.GetCameraByName("testCamera");
                camera.transform.position = currentCamera.transform.position;
                camera.transform.rotation = currentCamera.transform.rotation;
            }

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestStreamingCamera", warmUpFrames: 24);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestStreamingCameraFOV()
        {
            // Setup scene
            CreateMirisStream(out MirisStreamController streamController, out MirisStream stream, assetName: "market");
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream, GaussianSplatRenderComponent.Pipeline.Geometry, m_warmupFrames);
            
            stream.gameObject.transform.position = new Vector3(-4.5f, -2.0f, 2.0f);
            BaseObjectAdapter adapter = streamController.GetAdapter(SceneObjectType.Camera);
            if (adapter != null)
            {
                CameraAdapter cameraAdapter = (CameraAdapter)adapter;
                Camera currentCamera = cameraAdapter.GetCameraByName("testCamera");
                camera.transform.position = currentCamera.transform.position;
                camera.transform.rotation = currentCamera.transform.rotation;
                camera.fieldOfView = currentCamera.fieldOfView;
            }

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestStreamingCameraFOV", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestGaussianSigmaThreshold()
        {
            CreateMirisStream(out MirisStreamController _, out MirisStream stream);
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);
            
            camera.gameObject.transform.LookAt(stream.gameObject.transform.position); 
            
            for (int waitFrame = 0; waitFrame < m_warmupFrames; ++waitFrame)
            { 
                yield return new WaitForEndOfFrame();
            }
            
            GaussianSplatRenderComponent renderComponent = stream.GetComponentInChildren<GaussianSplatRenderComponent>();
            Assert.IsNotNull(renderComponent);
            renderComponent.m_gaussianSigmaThreshold = 2.0f;

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestGaussianSigmaThreshold", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestLodHeatMap()
        {
            // Setup scene & camera.
            CreateMirisStream(out MirisStreamController _, out MirisStream stream);
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);
            
            camera.gameObject.transform.LookAt(stream.gameObject.transform.position); 
                        
            for (int waitFrame = 0; waitFrame < m_warmupFrames; ++waitFrame)
            { 
                yield return new WaitForEndOfFrame();
            }

            // Find renderer component and set draw mode to LodHeatMap
            GaussianSplatRenderComponent renderComponent = stream.GetComponentInChildren<GaussianSplatRenderComponent>();
            Assert.IsNotNull(renderComponent);
            
            renderComponent.m_drawMode = GeometryRenderer.GeometryDrawMode.LodHeatMap;

            // Perform comparison render.
            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestLodHeatMap", warmUpFrames: m_warmupFrames);
        }
        
        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestNearClipThreshold()
        {
            CreateMirisStream(out MirisStreamController _, out MirisStream stream);
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);

            camera.gameObject.transform.LookAt(stream.gameObject.transform.position);
            
            for (int waitFrame = 0; waitFrame < m_warmupFrames; ++waitFrame)
            { 
                yield return new WaitForEndOfFrame();
            }
            
            GaussianSplatRenderComponent renderComponent = stream.GetComponentInChildren<GaussianSplatRenderComponent>();
            Assert.IsNotNull(renderComponent);
            renderComponent.m_nearClipThreshold = 5.0f;

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestNearClipThreshold", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestDepthBuffer()
        {
            CreateMirisStream(out MirisStreamController _, out MirisStream stream);
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);

            // add geometry in front of and behind test splats asset
            GameObject cubeFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeFront.transform.localScale = new Vector3(0.5f, 3.0f, 0.5f);
            cubeFront.transform.position = new Vector3(-1.0f, 0.0f, 0.0f);
            GameObject cubeBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeBack.transform.localScale = new Vector3(1.0f, 10.0f, 1.0f);
            cubeBack.transform.position = new Vector3(-10.0f, 0.0f, 10.0f);

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestDepthBuffer", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestFadeLargeSplats()
        {
            CreateMirisStream(out MirisStreamController _, out MirisStream stream);
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);

            camera.gameObject.transform.LookAt(stream.gameObject.transform.position); 
            
            for (int waitFrame = 0; waitFrame < m_warmupFrames; ++waitFrame)
            { 
                yield return new WaitForEndOfFrame();
            } 
           
            GaussianSplatRenderComponent renderComponent = stream.GetComponentInChildren<GaussianSplatRenderComponent>();
            Assert.IsNotNull(renderComponent);
            renderComponent.m_fadeLargeSplats = true;

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestFadeLargeSplats", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestGenerateJSON()
        {
            string assetName = "statue";
            string assetPath = $"content/conditioned/{assetName}/1x1";
            RunPythonCommand("build.env", $"scripts/dataprep/generate_asset_structure.py --dir-path {assetPath}");

            CreateMirisStream(out MirisStreamController _, out MirisStream stream, useDrop: false);
            stream.m_url = $"http://{{devlocalhost}}:3003/{assetPath}/structure.json";
            SetupCamera(stream.gameObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStreamRenderPipeline(stream);

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestFormattedJSON", warmUpFrames: m_warmupFrames);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestMultipleStreams()
        {
            // Setup scene (and delete the original stream)
            CreateMirisStream(out MirisStreamController _, out MirisStream stream, assetName: "tokyo");
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
                    MirisStream newStream = newStreamObject.AddComponent<MirisStream>();
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
    }
}
