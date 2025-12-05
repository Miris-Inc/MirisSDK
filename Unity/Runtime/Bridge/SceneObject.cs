// Copyright © 2025 Miris. All rights reserved.

using System;
using System.Runtime.InteropServices;

using UnityEngine;

namespace Miris.Runtime
{

    /// <summary>
    /// Scene provides access to the C++ Aqua Object without exposing the native C bindings 
    /// </summary>
    public class SceneObject
    {
        // SceneObject handle
        private int m_sceneObjectId;
        private const int c_invalidIdOrIndex = -1;

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
        // TODO: Aqua will supply the actual format value instead of the caller passing it as an argument.
        unsafe public AttributeInfo GetAttribute(String attributeName)
        {
            AttributeInfo attributeInfo = new();
            m_client.GetAttribute(m_sceneObjectId, attributeName, ref attributeInfo);
            return attributeInfo;
        }

        unsafe public Bounds GetBoundingBox()
        {
            float[] boundsData = new float[6];
            m_client.GetBoundingBox(m_sceneObjectId, boundsData);
            return new Bounds(
                new Vector3(boundsData[0], boundsData[1], boundsData[2]),
                new Vector3(boundsData[3], boundsData[4], boundsData[5])
            );
        }

        unsafe public Matrix4x4 GetTransform()
        {
            float[] matrixData = new float[16];
            m_client.GetTransform(m_sceneObjectId, matrixData);

            return new Matrix4x4(
                new Vector4(matrixData[0], matrixData[1], matrixData[2], matrixData[3]),
                new Vector4(matrixData[4], matrixData[5], matrixData[6], matrixData[7]),
                new Vector4(matrixData[8], matrixData[9], matrixData[10], matrixData[11]),
                new Vector4(matrixData[12], matrixData[13], matrixData[14], matrixData[15])
            );
        }

        public int GetLodIndex()
        {
            return m_client.GetLodIndex(m_sceneObjectId);
        }

        public void GetMetadata(out AssetMetadata metadata)
        {
            metadata = new AssetMetadata();
            m_client.GetMetadata(m_sceneObjectId, ref metadata);
        }

        public void SetTransform(Matrix4x4 transform)
        {
            float[] matrixArray = ValueConversion.MatrixToFloatArray(transform);
            m_client.SetSceneObjectTransform(m_sceneObjectId, matrixArray);
        }
    }
}
