// Copyright © 2026 Miris, Inc. All rights reserved.

// Unity engine
using UnityEngine;

namespace Miris.Runtime
{
    public class ValueConversion
    {
        static public float[] MatrixToFloatArray(Matrix4x4 matrix)
        {
            return new float[]{
                matrix.m00, matrix.m01, matrix.m02, matrix.m03,
                matrix.m10, matrix.m11, matrix.m12, matrix.m13,
                matrix.m20, matrix.m21, matrix.m22, matrix.m23,
                matrix.m30, matrix.m31, matrix.m32, matrix.m33
            };
        }
    }
}
