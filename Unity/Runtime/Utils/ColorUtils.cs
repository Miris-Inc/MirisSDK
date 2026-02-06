// Copyright © 2026 Miris, Inc. All rights reserved.

// Unity engine
using UnityEngine;
using Unity.Mathematics;

namespace Miris.Runtime
{
    public class ColorUtils
    {
        static public float4 HueToRgba(float hue)
        {
            float r = Mathf.Abs(hue * 6f - 3f) - 1f;
            float g = 2f - Mathf.Abs(hue * 6f - 2f);
            float b = 2f - Mathf.Abs(hue * 6f - 4f);

            return new float4(
                Mathf.Clamp01(r),
                Mathf.Clamp01(g),
                Mathf.Clamp01(b),
                1.0f
            );
        }
    }
}
