// Copyright © 2026 Miris, Inc. All rights reserved.

using System.Collections;

using UnityEngine;
using UnityEngine.TestTools;

using Miris.Runtime;
using NUnit.Framework;

namespace Miris.Tests
{
    // End to end through the native per-view path: a stream on the default (Native) controller
    // has to reach the main camera through SharkCameraViews.
    public class TestSharkCameraViews
    {
        // Long enough for the client to be adopted, the stream to load and its first splats to land.
        const int MaxFrames = 600;

        GameObject m_cameraObject;
        GameObject m_controllerObject;
        GameObject m_streamObject;
        RenderTexture m_target;

        [TearDown]
        public void TearDown()
        {
            // The stream and controller first, so Shark tears down while its camera still exists.
            Object.DestroyImmediate(m_streamObject);
            Object.DestroyImmediate(m_controllerObject);
            Object.DestroyImmediate(m_cameraObject);
            if (m_target != null)
            {
                m_target.Release();
                Object.DestroyImmediate(m_target);
            }
        }

        [UnityTest]
        public IEnumerator SharkRendersIntoTheMainCamera()
        {
            if (!Application.isEditor || !SharkSplatRenderSystem.IsSupported)
            {
                Assert.Ignore("The per-camera Shark path only runs in an editor with the native renderer");
            }
            string contentPath = MirisTestUtils.GetFirstAssetContentPath();
            Assert.IsNotNull(contentPath, "No conditioned content available");

            // An explicit target and explicit Render calls, so the camera renders whether or not a
            // Game view is showing it. Left enabled: Camera.main skips disabled cameras.
            m_target = new RenderTexture(256, 256, 24);
            m_cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = m_cameraObject.AddComponent<Camera>();
            camera.targetTexture = m_target;

            m_controllerObject = new GameObject("Miris Stream Controller");
            MirisStreamController streamController = m_controllerObject.AddComponent<MirisStreamController>();
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;

            m_streamObject = new GameObject("Miris Stream");
            MirisStreamTest stream = m_streamObject.AddComponent<MirisStreamTest>();
            stream.m_streamController = streamController;
            stream.m_url = MirisTestUtils.BuildDirectContentUrl(contentPath);

            for (int frame = 0; frame < MaxFrames; ++frame)
            {
                yield return null;
                camera.Render();
                if (SharkSplatRenderSystem.m_instance.CameraRenderCount(camera) > 0)
                {
                    break;
                }
            }

            Assert.AreEqual(MirisStreamController.RenderBackend.Native, streamController.Backend,
                            "the controller fell back from Shark");
            Assert.Greater(SharkSplatRenderSystem.m_instance.CameraRenderCount(camera), 0,
                           $"Shark never rendered into the main camera in {MaxFrames} frames");
        }
    }
}
