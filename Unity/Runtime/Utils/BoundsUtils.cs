// Copyright © 2026 Miris, Inc. All rights reserved.

using UnityEngine;

namespace Miris.Runtime
{
    public class BoundsUtils
    {
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
