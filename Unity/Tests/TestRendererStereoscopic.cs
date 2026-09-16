// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Collections;
using System.Linq;

using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;

using Miris.Runtime;
using NUnit.Framework;

namespace Miris.Tests
{
    public class TestRendererStereoscopic : RendererTestBase
    {
        static private RenderTexture renderTexture;

        static private Camera.StereoscopicEye[] s_stereoEyes = Enum.GetValues(typeof(Camera.StereoscopicEye)).Cast<Camera.StereoscopicEye>().ToArray();

        static private void SetStereoscopicProperty(Camera camera)
        {
            camera.stereoTargetEye = StereoTargetEyeMask.Both;
            renderTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
        }

        static private Texture2D RenderStereoEye(Camera camera, Camera.StereoscopicEye eye)
        {

            Matrix4x4 viewMatrix = camera.GetStereoViewMatrix(eye);
            Matrix4x4 projectionMatrix = camera.GetStereoProjectionMatrix(eye);

            camera.worldToCameraMatrix = viewMatrix;
            camera.projectionMatrix = projectionMatrix;

            camera.targetTexture = renderTexture;
            camera.Render();

            return RenderTextureToTexture2D(camera, renderTexture);

        }

        static private Texture2D RenderTextureToTexture2D(Camera camera, RenderTexture renderTexture)
        {
            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, UnityEngine.TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();

            RenderTexture.active = null;
            camera.targetTexture = null;
            return texture;
        }

        static private void CompareEyeRendering(Camera camera, GraphicsTestSettings graphicsTestSettings, Camera.StereoscopicEye eye, string referenceImagePath)
        {

            Texture2D eyeTexture = RenderStereoEye(camera, eye);
            Texture2D referenceEyeImage = Resources.Load<Texture2D>(referenceImagePath);

            ImageAssert.AreEqual(
                referenceEyeImage,
                eyeTexture,
                settings: graphicsTestSettings.ImageComparisonSettings,
                saveFailedImageToDisk: true
            );

        }

        static private IEnumerator RenderStereoComparison(Camera camera, Camera.StereoscopicEye eye, GraphicsTestSettings graphicsTestSettings, string referenceImagePath, int warmUpFrames = 2)
        {
            // Warm up
            for (int frameIndex = 0; frameIndex < warmUpFrames; ++frameIndex)
            {
                yield return new WaitForEndOfFrame();
            }

            CompareEyeRendering(camera, graphicsTestSettings, eye, referenceImagePath);

            camera.targetTexture = null;
            RenderTexture.active = null;
            renderTexture.Release();
            GameObject.DestroyImmediate(renderTexture);

            yield return null;
        }

        [UnityTest]
        [Ignore("This test is skipped temporarily until we can figure out how to optionally enable XR mode in Unity 6.")]
        public IEnumerator TestStereoNonStreamingGarden([ValueSource(nameof(s_stereoEyes))] Camera.StereoscopicEye eye)
        {
            // Setup scene
            GameObject gardenPrefab = Resources.Load<GameObject>("GaussianSplatAssets/garden-10x10");
            GameObject gardenObject = GameObject.Instantiate(gardenPrefab);
            SetupCamera(gardenObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStereoscopicProperty(camera);

            // Compare render to reference
            yield return RenderStereoComparison(camera, eye, graphicsTestSettings, $"ReferenceImages/TestNonStreamingGarden_{eye.ToString()}");
        }

        [UnityTest]
        [Ignore("This test is skipped temporarily until we can figure out how to optionally enable XR mode in Unity 6.")]
        public IEnumerator TestStereoStreamingGarden([ValueSource(nameof(s_stereoEyes))] Camera.StereoscopicEye eye)
        {
            // Setup scene
            GameObject controllerObject = new GameObject("MirisStreamController");
            MirisStreamController streamController = controllerObject.AddComponent<MirisStreamController>();
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;
            streamController.m_fadeDurationSeconds = 0;

            GameObject streamObject = new GameObject("Miris Stream");
            MirisStreamTest stream = streamObject.AddComponent<MirisStreamTest>();
            stream.m_streamController = streamController;
            stream.SetUrlFromContentPath("garden/10x10");

            SetupCamera(streamObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);
            SetStereoscopicProperty(camera);

            // Compare render to reference
            // Need to wait 4 frames to warm-up
            // 1: MirisStreamController Start
            // 2: MirisStreamController Update, triggering blocking scene execution
            // 3: MirisStreamController Update, syncing changes to Unity
            // 4: Create graphics resources and render
            yield return RenderStereoComparison(camera, eye, graphicsTestSettings, $"ReferenceImages/TestStreamingGarden_{eye.ToString()}", warmUpFrames: 4);
        }

    }
}
