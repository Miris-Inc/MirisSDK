// Copyright © 2026 Miris, Inc. All rights reserved.

using System.Collections;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;

using NUnit.Framework;

using Miris.Runtime;

namespace Miris.Tests
{
    public class TestRendererEncodings : RendererTestBase
    {
        private static string ResolveContentUrl(MirisStreamController streamController, MirisInternalController internalController, string assetName)
        {
            return Task.Run(() => MirisTestUtils.ResolveContentUrl(streamController, internalController, assetName, MirisTestUtils.GetTestServerEnvironment())).Result;
        }

        [UnityTest, Ignore("Encoding tests are broken")]
        public IEnumerator TestTreeEncoding()
        {
            GameObject controllerObject = new GameObject("Miris Stream Controller");

            MirisStreamController streamController = controllerObject.AddComponent<MirisStreamController>();
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;
            streamController.m_fadeDurationSeconds = 0;

            MirisInternalController internalController = controllerObject.AddComponent<MirisInternalController>();
            string contentUrl = ResolveContentUrl(streamController, internalController, "tree");

            GameObject streamObject = new GameObject("Miris Stream");
            MirisStreamTest stream = streamObject.AddComponent<MirisStreamTest>();
            stream.m_streamController = streamController;
            stream.SetUrlFromContentPath(contentUrl);

            // Update background to solid black.
            SetupCamera(streamObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestTreeEncoding", warmUpFrames: 10);
        }

        [UnityTest, Ignore("4DGS render tests are broken - fix in EN-827 and then un-ignore this test")]
        public IEnumerator TestDancerEncoding()
        {
            GameObject controllerObject = new GameObject("Miris Stream Controller");

            MirisStreamController streamController = controllerObject.AddComponent<MirisStreamController>();
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;
            streamController.m_fadeDurationSeconds = 0;

            MirisInternalController internalController = controllerObject.AddComponent<MirisInternalController>();
            string contentUrl = ResolveContentUrl(streamController, internalController, "dancer");

            GameObject streamObject = new GameObject("Miris Stream");
            MirisStreamTest stream = streamObject.AddComponent<MirisStreamTest>();
            stream.m_streamController = streamController;
            stream.SetUrlFromContentPath(contentUrl);

            // Update background to solid black.
            SetupCamera(streamObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestDancerEncoding", warmUpFrames: 10);
        }
    }
}
