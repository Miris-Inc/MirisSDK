// Copyright © 2026 Miris, Inc. All rights reserved.

using System.Collections.Generic;

using UnityEngine;

namespace Miris.Runtime
{
    public class BoundsUtils
    {
        public static bool HasFiniteSize(Bounds bounds)
        {
            Vector3 size = bounds.size;
            return size != Vector3.zero
                && !float.IsInfinity(size.x) && !float.IsInfinity(size.y)
                && !float.IsInfinity(size.z)
                && !float.IsNaN(size.x) && !float.IsNaN(size.y) && !float.IsNaN(size.z);
        }

        // True diagonal magnitude, used to scale camera clip planes to content size. Distinct on
        // purpose from ViewportCameraController's CalculateCharacteristicSize() (mean edge, for
        // interaction speed) and CalculateFrameDistance() (max edge, for initial framing
        // distance) — each favors a different dimension of the bounds for a different piece of
        // camera math.
        public static float LargestMagnitude(IEnumerable<Bounds> boundsSet)
        {
            float largestMagnitude = 0f;

            foreach (Bounds bounds in boundsSet)
            {
                if (!HasFiniteSize(bounds))
                {
                    continue;
                }

                largestMagnitude = Mathf.Max(largestMagnitude, bounds.size.magnitude);
            }

            return largestMagnitude;
        }

        public static Vector3[] BoundsGetCorners(Bounds bounds)
        {
            return BoundsGetCorners(bounds, Matrix4x4.identity);
        }

        public static Vector3[] BoundsGetCorners(Bounds bounds, Matrix4x4 xform)
        {
            Vector3[] corners = new Vector3[8];

            Vector3 boundsMin = bounds.min;
            Vector3 boundsMax = bounds.max;

            corners[0].Set(boundsMin.x, boundsMin.y, boundsMin.z);
            corners[1].Set(boundsMin.x, boundsMin.y, boundsMax.z);
            corners[2].Set(boundsMin.x, boundsMax.y, boundsMin.z);
            corners[3].Set(boundsMin.x, boundsMax.y, boundsMax.z);
            corners[4].Set(boundsMax.x, boundsMin.y, boundsMin.z);
            corners[5].Set(boundsMax.x, boundsMin.y, boundsMax.z);
            corners[6].Set(boundsMax.x, boundsMax.y, boundsMin.z);
            corners[7].Set(boundsMax.x, boundsMax.y, boundsMax.z);

            for (int cornerIndex = 0; cornerIndex < corners.Length; ++cornerIndex)
            {
                corners[cornerIndex] = xform.MultiplyPoint3x4(corners[cornerIndex]);
            }

            return corners;
        }
    }
}
