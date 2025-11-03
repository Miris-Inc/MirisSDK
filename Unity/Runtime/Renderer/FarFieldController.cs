// Copyright © 2025 Miris.All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace Aqua.Runtime
{
    // Contains a little logic to control how we render a far field
    //
    // The far field is a set of images that cache the rendering of certain depth ranges of gaussian splats
    public class FarFieldController : MonoBehaviour
    {
        // All the planes in the cache fields, ordered from closest to farthest
        [SerializeField]
        private List<MeshRenderer> m_farFieldPlanes;
        
        // Whether the cache field system should be active
        public bool enableFarField = false;
        
        // Size of the far field render target, as a percentage of the render resolution
        [Range(0, 1)]
        public float farFieldFirstCacheRTProportion = 1;
        
        // Size of the far field render target, as a percentage of the render resolution
        [Range(0, 1)]
        public float farFieldSecondCacheRTProportion = 0.75f;

        // Proportion of splats to render in the far field's first cache
        [Range(0, 1)]
        public float proportionSplatsInFarFieldFirstCache = 0.33f;
        
        // Proportion of splats to render in the far field's second cache
        [Range(0, 1)]
        public float proportionSplatsInFarFieldSecondCache = 0.16f;

        [Range(0, 100)]
        public float farFieldFirstPlaneConstantDistance = 4.0f;
        
        [Range(0, 100)]
        public float farFieldSecondPlaneConstantDistance = 8.0f;
        
        void Update() {
            UpdateCacheFields();
        }

        // Update the GaussianSplatRenderSystem with the far field parameters, and ensure that the far field plane is
        // at the correct distance from the camera
        private void UpdateCacheFields() {
            var renderSystem = GaussianSplatRenderSystem.m_instance;

            renderSystem.farFieldEnabled = enableFarField;

            if (enableFarField) {
                var cachePlaneDefinitions = renderSystem.GetCachePlanes();

                if (cachePlaneDefinitions.Count > 0) {
                    cachePlaneDefinitions[0].splatsInCacheProportion = proportionSplatsInFarFieldFirstCache;
                    cachePlaneDefinitions[0].renderTargetResolutionProportion = farFieldFirstCacheRTProportion;
                    cachePlaneDefinitions[0].planeDistance = farFieldFirstPlaneConstantDistance;
                }

                if (cachePlaneDefinitions.Count > 1) {
                    cachePlaneDefinitions[1].splatsInCacheProportion = proportionSplatsInFarFieldSecondCache;
                    cachePlaneDefinitions[1].renderTargetResolutionProportion = farFieldSecondCacheRTProportion;
                    cachePlaneDefinitions[1].planeDistance = farFieldSecondPlaneConstantDistance;
                }

                for (var i = 0; i < m_farFieldPlanes.Count; i++) {
                    var cachePlaneRenderer = m_farFieldPlanes[i];
                    var cachePlaneDefinition = cachePlaneDefinitions[i];
                    cachePlaneRenderer.material = renderSystem.GetCachePlaneMaterial(i);
                    cachePlaneRenderer.enabled = renderSystem.IsFarFieldActive() && cachePlaneDefinition.splatsInCacheProportion > 0;
                }
            }
        }
    }
}
