// Copyright © 2026 Miris, Inc. All rights reserved.

using System.Collections;

using UnityEngine;
using UnityEngine.TestTools;

using Miris.Runtime;
using NUnit.Framework;

namespace Miris.Tests
{
    // End to end through the native per-view path: a stream on the default (Native) controller
    // has to reach the main camera through SharkCameraViews, and its composite has to draw there.
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
            // Black, so any pixel with colour is one the composite drew. Colour rather than alpha:
            // URP's HDR intermediate has no alpha channel, so its copy into the target can leave
            // every pixel opaque.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;

            m_controllerObject = new GameObject("Miris Stream Controller");
            MirisStreamController streamController = m_controllerObject.AddComponent<MirisStreamController>();
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;

            m_streamObject = new GameObject("Miris Stream");
            MirisStreamTest stream = m_streamObject.AddComponent<MirisStreamTest>();
            stream.m_streamController = streamController;
            stream.m_url = MirisTestUtils.BuildDirectContentUrl(contentPath);

            int compositedPixels = 0;
            for (int frame = 0; frame < MaxFrames; ++frame)
            {
                yield return null;
                FrameBounds(camera, stream.GetWorldBounds());
                camera.Render();
                if (SharkSplatRenderSystem.m_instance.CameraRenderCount(camera) > 0
                    && (compositedPixels = CountCompositedPixels(m_target)) > 0)
                {
                    break;
                }
            }

            Assert.IsTrue(streamController.IsSharkActive, "Shark is unavailable to the controller");
            Assert.Greater(SharkSplatRenderSystem.m_instance.CameraRenderCount(camera), 0,
                           $"Shark never rendered into the main camera in {MaxFrames} frames");
            Assert.Greater(compositedPixels, 0,
                           $"Shark rendered, but nothing was composited over the camera in {MaxFrames} frames");
        }

        // Places the camera on the -Z side of the bounds, looking along +Z, far enough back that they
        // fit its vertical field of view. The content's placement comes from the manifest's first
        // asset, so it is not known up front.
        static void FrameBounds(Camera camera, Bounds bounds)
        {
            if (bounds.size == Vector3.zero)
            {
                return;
            }
            float radius = bounds.extents.magnitude;
            float distance = radius / Mathf.Sin(0.5f * camera.fieldOfView * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(bounds.center - distance * Vector3.forward, Quaternion.identity);
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius);
            camera.farClipPlane = distance + radius;
        }

        static int CountCompositedPixels(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;

            int count = 0;
            foreach (Color32 pixel in readback.GetPixels32())
            {
                if (pixel.r > 0 || pixel.g > 0 || pixel.b > 0)
                {
                    ++count;
                }
            }
            Object.DestroyImmediate(readback);
            return count;
        }
    }
}
