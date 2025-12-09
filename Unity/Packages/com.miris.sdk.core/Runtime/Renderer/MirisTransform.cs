// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;

namespace Miris.Runtime
{
    public class MirisTransform
    {
        private Matrix4x4 m_worldToLocalMatrix = Matrix4x4.identity;
        public Matrix4x4 worldToLocalMatrix => m_worldToLocalMatrix;

        private Matrix4x4 m_localToWorldMatrix = Matrix4x4.identity;
        public Matrix4x4 localToWorldMatrix => m_localToWorldMatrix;

        public MirisTransform()
        {
        }

        public MirisTransform(Transform transform)
        {
            m_worldToLocalMatrix = transform.worldToLocalMatrix;
            m_localToWorldMatrix = transform.localToWorldMatrix;
        }

        public Vector3 TransformPoint(Vector3 localPos) => m_localToWorldMatrix.MultiplyPoint3x4(localPos);

        public Vector3 InverseTransformPoint(Vector3 worldPos) => m_worldToLocalMatrix.MultiplyPoint3x4(worldPos);
        public Vector3 InverseTransformDirection(Vector3 worldDirNormalized) => m_worldToLocalMatrix.MultiplyVector(worldDirNormalized);
        public Vector3 InverseTransformVector(Vector3 worldVec) => InverseTransformDirection(worldVec);

        static public MirisTransform operator *(MirisTransform transform, Matrix4x4 matrix)
        {
            MirisTransform result = new();
            result.m_localToWorldMatrix = transform.m_localToWorldMatrix * matrix;
            Matrix4x4.Inverse3DAffine(result.m_localToWorldMatrix, ref result.m_worldToLocalMatrix);

            return result;
        }

        static public MirisTransform operator *(Matrix4x4 matrix, MirisTransform transform)
        {
            MirisTransform result = new();
            result.m_localToWorldMatrix = matrix * transform.m_localToWorldMatrix;
            Matrix4x4.Inverse3DAffine(result.m_localToWorldMatrix, ref result.m_worldToLocalMatrix);

            return result;
        }
    }
}
