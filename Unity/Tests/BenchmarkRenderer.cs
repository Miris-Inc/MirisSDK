// Copyright © 2026 Miris, Inc. All rights reserved.

// Standard libary
using System.Collections;
using System.Linq;
using System.Threading.Tasks;

// Unity engine
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;

// Unity packages
using Unity.PerformanceTesting;
using NUnit.Framework;

using Miris.Runtime;

namespace Miris.Tests
{
    public class BenchmarkRenderer : TestRenderer
    {
        static string[] s_contentNames = {
            "garden",
            "moana",
            "tokyo",
        };

        private void CreateMirisStream(string assetName, out MirisStreamController outStreamController, out MirisStream stream)
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
            ((MirisStreamTest)stream).SetUrlFromContentPath(contentUrl);

            outStreamController = streamController;
        }

        [UnityTest, Performance]
        [Ignore("This test is skipped temporarily until we can figure out what is causing test to fail")]
        public IEnumerator BenchmarkAsset([ValueSource(nameof(s_contentNames))] string assetName)
        {

            // Add a warm-up for the start of the first test
            for (int i = 0; i < 100; i++) {
                yield return null;
            }
            
            CreateMirisStream(assetName, out MirisStreamController streamController, out MirisStream stream);
            SetupCamera(stream.gameObject, out Camera _, out GraphicsTestSettings _);
            SetStreamRenderPipeline(stream);
            
            // Record per frame timings.
            int framesToPlayback = 100;
            yield return Measure.Frames()
                .MeasurementCount(framesToPlayback)
                .Run();
        }
    }
}
