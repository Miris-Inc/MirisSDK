// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Runtime.InteropServices;

using UnityEngine;

namespace Miris.Runtime
{

    /// <summary>
    /// Scene provides access to the C++ Object without exposing the native C bindings 
    /// </summary>
    public class SceneObject
    {
        // SceneObject handle
        private int m_sceneObjectId;
        private const int c_invalidIdOrIndex = -1;

        private float[] m_boundsData = new float[6];
        private float[] m_matrixData = new float[16];

        private Client m_client = null;

        internal SceneObject(Client client, int sceneObjectId)
        {
            m_client = client;
            m_sceneObjectId = sceneObjectId;
        }

        public void PrintHierarchy()
        {
            m_client.PrintSceneObjectHierarchy(m_sceneObjectId);
        }

        public int GetId()
        {
            return m_sceneObjectId;
        }

        public string GetName()
        {
            IntPtr namePtr = m_client.GetSceneObjectName(m_sceneObjectId);
            return Marshal.PtrToStringAnsi(namePtr);
        }

        public SceneObjectType GetSceneObjectType()
        {
            int objectTypeInt = m_client.GetSceneObjectType(m_sceneObjectId);
            DiagnosticUtils.ValidateEnum<SceneObjectType>(objectTypeInt);
            return (SceneObjectType)objectTypeInt;
        }

        public int GetParentId()
        {
            int parentObjectId = m_client.GetSceneObjectParent(m_sceneObjectId);
            return parentObjectId;
        }

        public bool IsAncestorOf(int sceneObjectId)
        {
            return m_client.IsSceneObjectAncestorOf(m_sceneObjectId, sceneObjectId);
        }

        public int GetAttributeCount()
        {
            return m_client.GetAttributeCount(m_sceneObjectId);
        }

        public bool HasAttribute(String attributeName)
        {
            return m_client.HasAttribute(m_sceneObjectId, attributeName);
        }

        // Wraps a NativeArray around a C void* for direct access into unmanaged memory. 
        // Use with caution!
        unsafe public AttributeInfo GetAttribute(String attributeName)
        {
            AttributeInfo attributeInfo = new();
            m_client.GetAttribute(m_sceneObjectId, attributeName, ref attributeInfo);
            return attributeInfo;
        }

        unsafe public Bounds GetBoundingBox()
        {
            m_client.GetBoundingBox(m_sceneObjectId, m_boundsData);
            return new Bounds(
                new Vector3(m_boundsData[0], m_boundsData[1], m_boundsData[2]),
                new Vector3(m_boundsData[3], m_boundsData[4], m_boundsData[5])
            );
        }

        unsafe public Matrix4x4 GetTransform()
        {
            m_client.GetTransform(m_sceneObjectId, m_matrixData);

            return new Matrix4x4(
                new Vector4(m_matrixData[0], m_matrixData[1], m_matrixData[2], m_matrixData[3]),
                new Vector4(m_matrixData[4], m_matrixData[5], m_matrixData[6], m_matrixData[7]),
                new Vector4(m_matrixData[8], m_matrixData[9], m_matrixData[10], m_matrixData[11]),
                new Vector4(m_matrixData[12], m_matrixData[13], m_matrixData[14], m_matrixData[15])
            );
        }

        public int GetLodIndex()
        {
            return m_client.GetLodIndex(m_sceneObjectId);
        }

        public void GetMetadata(out AssetMetadata metadata)
        {
            metadata = new AssetMetadata();
            m_client.GetMetadata(m_sceneObjectId, metadata);
        }

        public void SetTransform(Matrix4x4 transform)
        {
            float[] matrixArray = ValueConversion.MatrixToFloatArray(transform);
            m_client.SetSceneObjectTransform(m_sceneObjectId, matrixArray);
        }
    }
}
