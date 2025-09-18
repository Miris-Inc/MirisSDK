// Copyright © 2024 Miris. All rights reserved.

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

// Aqua
using Aqua.Runtime;

namespace Aqua.Tests
{
    public class BenchmarkRenderer : TestRenderer
    {
        static string[] s_contentNames = {
            "garden",
            "moana",
            "tokyo",
        };

        private void CreateMirisStream(string assetName, out MirisStreamController streamController, out MirisStream stream)
        {
            string contentUrl = Task.Run(() => AquaTestUtils.ResolveContentUrl(assetName, "Local_UNSECURE")).Result;

            GameObject controllerObject = new GameObject("Miris Stream Controller");
            streamController = controllerObject.AddComponent<MirisStreamController>();
            streamController.m_executionMode = MirisStreamController.ExecutionMode.Synchronous;
            streamController.m_fadeDurationSeconds = 0;
            GameObject streamObject = new GameObject("Miris Stream");
            stream = streamObject.AddComponent<MirisStream>();
            stream.SetUrlFromContentPath(contentUrl);
        }

        [UnityTest, Performance]
        [ConditionalIgnore("IgnoreInWindowsCI", "Graphics tests require interactive window station on Windows CI")]
        public IEnumerator BenchmarkAsset([ValueSource(nameof(s_contentNames))] string assetName)
        {

            // Add a warm-up for the start of the first test
            for (int i = 0; i < 100; i++) {
                yield return null;
            }
            
            CreateMirisStream(assetName, out MirisStreamController _, out MirisStream stream);
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
