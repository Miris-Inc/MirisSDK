// Copyright © 2026 Miris, Inc. All rights reserved.
// This assembly only compiles when URP is available (see asmdef defineConstraints)

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Miris.Tests
{
    public class UrpTestSetup : IPrebuildSetup, IPostBuildCleanup
    {
        const string DEFAULT_URP_PATH = "Assets/URP_Performant.asset";
        private static string prevRPPath;
        private static string prevQualityRPPath;

        public void Setup()
        {
            // Only setup URP if explicitly requested via environment variable
            string useUrp = Environment.GetEnvironmentVariable("AQUA_USE_URP");
            if (string.IsNullOrEmpty(useUrp) || useUrp != "1")
            {
                Debug.Log("[UrpTestSetup] Skipping URP setup - AQUA_USE_URP not set. Using default BIRP pipeline");
                return;
            }

            // Save current pipeline
            var prevRP = GraphicsSettings.defaultRenderPipeline;
            var prevQualityRP = QualitySettings.renderPipeline;

            if (prevRP)
                prevRPPath = AssetDatabase.GetAssetPath(prevRP);
            if (prevQualityRP)
                prevQualityRPPath = AssetDatabase.GetAssetPath(prevQualityRP);

            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                DEFAULT_URP_PATH
            );

            if (urpAsset == null)
                throw new Exception($"URP asset not found at: {DEFAULT_URP_PATH}");

            GraphicsSettings.defaultRenderPipeline = urpAsset;
            QualitySettings.renderPipeline = urpAsset;

            UnityEngine.Debug.Log($"[UrpTestSetup] Applied URP asset: {DEFAULT_URP_PATH}");
        }

        public void Cleanup()
        {
            // Only restore if URP was requested (check environment variable)
            string useUrp = Environment.GetEnvironmentVariable("AQUA_USE_URP");
            if (string.IsNullOrEmpty(useUrp) || useUrp != "1")
            {
                Debug.Log("[UrpTestSetup] Skipping cleanup - AQUA_USE_URP not set. BIRP Pipeline was used.");
                return;
            }

            // Restore previous pipeline
            var prevRP = string.IsNullOrEmpty(prevRPPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(prevRPPath);

            var prevQualityRP = string.IsNullOrEmpty(prevQualityRPPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(prevQualityRPPath);

            GraphicsSettings.defaultRenderPipeline = prevRP;
            QualitySettings.renderPipeline = prevQualityRP;

            Debug.Log("[UrpTestSetup] Restored previous render pipeline.");
        }
    }
}
#endif
