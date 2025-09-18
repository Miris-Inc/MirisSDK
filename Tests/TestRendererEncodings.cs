// Copyright © 2025 Miris. All rights reserved.

using System.Collections;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;

using NUnit.Framework;

using Aqua.Runtime;

namespace Aqua.Tests
{
    public class TestRendererEncodings : RendererTestBase
    {
        private static string ResolveContentUrl(string assetName)
        {
            return Task.Run(() => AquaTestUtils.ResolveContentUrl(assetName, "LocalTest_UNSECURE")).Result;
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator TestTreeEncoding()
        {
            string contentUrl = ResolveContentUrl("tree");

            GameObject controllerObject = new GameObject("Miris Stream Controller");

            MirisStreamController streamController = controllerObject.AddComponent<MirisStreamController>();
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;
            streamController.m_fadeDurationSeconds = 0;

            GameObject streamObject = new GameObject("Miris Stream");
            MirisStream stream = streamObject.AddComponent<MirisStream>();
            stream.SetUrlFromContentPath(contentUrl);

            // Update background to solid black.
            SetupCamera(streamObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestTreeEncoding", warmUpFrames: 10);
        }

        [UnityTest, Ignore("4DGS render tests are broken - fix in EN-827 and then un-ignore this test")]
        public IEnumerator TestDancerEncoding()
        {
            string contentUrl = ResolveContentUrl("dancer");

            GameObject controllerObject = new GameObject("Miris Stream Controller");

            MirisStreamController streamController = controllerObject.AddComponent<MirisStreamController>();
            streamController.m_useDropFiles = false;
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;
            streamController.m_fadeDurationSeconds = 0;

            GameObject streamObject = new GameObject("Miris Stream");
            MirisStream stream = streamObject.AddComponent<MirisStream>();
            stream.SetUrlFromContentPath(contentUrl);

            // Update background to solid black.
            SetupCamera(streamObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings);

            yield return RenderComparison(camera, graphicsTestSettings, "ReferenceImages/TestDancerEncoding", warmUpFrames: 10);
        }
    }
}
