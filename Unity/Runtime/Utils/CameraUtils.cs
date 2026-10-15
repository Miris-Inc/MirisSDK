// Copyright © 2026 Miris, Inc. All rights reserved.

using System.Collections.Generic;

using UnityEngine;

namespace Miris.Runtime
{
    public class CameraUtils
    {
        // Clip planes as a fraction of the streamed content's diagonal, matching the web player.
        private const float c_nearClipFactor = 0.001f;
        private const float c_farClipFactor = 100f;

        public static bool TryFitClipPlanesToContent(
            Camera camera, IEnumerable<Bounds> contentBounds)
        {
            if (camera == null)
            {
                return false;
            }

            float magnitude = BoundsUtils.LargestMagnitude(contentBounds);
            if (magnitude <= 0f)
            {
                return false;
            }

            camera.nearClipPlane = magnitude * c_nearClipFactor;
            camera.farClipPlane = magnitude * c_farClipFactor;

            return true;
        }
    }
}
