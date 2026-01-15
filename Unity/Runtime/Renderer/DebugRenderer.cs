// Copyright © 2025 Miris, Inc. All rights reserved.

using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Miris.Runtime
{
    public class DebugRenderer
    {
        public enum PrimitiveType
        {
            Box = 0,
            Locator,
        }

        private static class ShaderIds
        {
            public static readonly int Positions = Shader.PropertyToID("_Positions");
            public static readonly int BoxCenter = Shader.PropertyToID("_BoxCenter");
            public static readonly int BoxExtents = Shader.PropertyToID("_BoxExtents");
            public static readonly int Color = Shader.PropertyToID("_Color");
        }

        static Vector3[] m_boxPositions = new Vector3[]{
            // Bottom 4 vertices
            new Vector3(-1, -1, -1),
            new Vector3(1, -1, -1),
            new Vector3(1, -1, 1),
            new Vector3(-1, -1, 1),

            // Top 4 vertices
            new Vector3(-1, 1, -1),
            new Vector3(1, 1, -1),
            new Vector3(1, 1, 1),
            new Vector3(-1, 1, 1),
        };

        static uint[] m_boxIndices = new uint[] {
            // Bottom face
            0, 1, 1, 2, 2, 3, 3, 0,

            // Top face
            4, 5, 5, 6, 6, 7, 7, 4,

            // Columns
            0, 4, 1, 5, 2, 6, 3, 7
        };

        static Vector3[] m_locatorPositions = new Vector3[]{
            // Bottom 4 vertices
            new Vector3(-1, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, -1, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 0, -1),
            new Vector3(0, 0, 1),
        };

        static uint[] m_locatorIndices = new uint[] {
            0, 1, 2, 3, 4, 5
        };

        private GraphicsBuffer m_gpuBoxPositions;
        private GraphicsBuffer m_gpuBoxIndices;
        private GraphicsBuffer m_gpuLocatorPositions;
        private GraphicsBuffer m_gpuLocatorIndices;
        private Material m_material;
        private Shader m_shader;

        public DebugRenderer()
        {
            m_shader = (Shader)Resources.Load("Shaders/DebugRenderBounds");
            m_material = new Material(m_shader) { name = "BoundingBoxMaterial" };

            // Box buffers
            m_gpuBoxPositions = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                m_boxPositions.Length,
                Marshal.SizeOf(typeof(Vector3))
            );
            m_gpuBoxPositions.SetData(m_boxPositions);

            m_gpuBoxIndices = new GraphicsBuffer(
                GraphicsBuffer.Target.Index,
                m_boxIndices.Length,
                Marshal.SizeOf(typeof(uint))
            );
            m_gpuBoxIndices.SetData(m_boxIndices);

            // Locator buffers
            m_gpuLocatorPositions = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                m_locatorPositions.Length,
                Marshal.SizeOf(typeof(Vector3))
            );
            m_gpuLocatorPositions.SetData(m_locatorPositions);

            m_gpuLocatorIndices = new GraphicsBuffer(
                GraphicsBuffer.Target.Index,
                m_locatorIndices.Length,
                Marshal.SizeOf(typeof(uint))
            );
            m_gpuLocatorIndices.SetData(m_locatorIndices);
        }

        public void DrawPrimitive(PrimitiveType primitiveType, CommandBuffer commandBuffer, MirisTransform transform, Bounds bounds, float4 color)
        {
            switch (primitiveType)
            {
                case PrimitiveType.Box:
                    Draw(commandBuffer, transform, bounds, m_gpuBoxPositions, m_gpuBoxIndices, color);
                    break;

                case PrimitiveType.Locator:
                    Draw(commandBuffer, transform, bounds, m_gpuLocatorPositions, m_gpuLocatorIndices, color);
                    break;
            }
        }

        private void Draw(
            CommandBuffer commandBuffer,
            MirisTransform transform,
            Bounds bounds,
            GraphicsBuffer positions,
            GraphicsBuffer indices,
            float4 color
        )
        {
            // Creating one on-the-fly is in-efficient but this is debug drawing so meh :)
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetBuffer(ShaderIds.Positions, positions);
            propertyBlock.SetVector(ShaderIds.BoxCenter, bounds.center);
            propertyBlock.SetVector(ShaderIds.BoxExtents, bounds.extents);
            propertyBlock.SetVector(ShaderIds.Color, color);

            commandBuffer.DrawProcedural(
                indexBuffer: indices,
                matrix: transform.localToWorldMatrix,
                material: m_material,
                shaderPass: 0,
                topology: MeshTopology.Lines,
                indexCount: indices.count,
                instanceCount: 1,
                properties: propertyBlock
            );
        }

        public void Dispose()
        {
            GameObject.DestroyImmediate(m_material);
            m_gpuBoxIndices?.Dispose();
            m_gpuBoxPositions?.Dispose();
            m_gpuLocatorPositions?.Dispose();
            m_gpuLocatorIndices?.Dispose();
        }

    }

}
