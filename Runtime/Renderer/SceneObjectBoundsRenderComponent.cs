using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aqua.Runtime
{
    // For querying an Aqua scene object's bounding box
    // and visualizing it
    public class SceneObjectBoundsRenderComponent : MonoBehaviour
    {
        public AquaSceneObject m_sceneObject;
        private DebugRenderer m_renderer;
        private CommandBuffer m_commandBuffer;
        private float4 m_color = new float4(0, 1, 0, 1);

        protected void Start()
        {
            m_renderer = new();
            m_commandBuffer = new();
        }

        protected void OnDestroy()
        {
            m_renderer?.Dispose();
        }

        protected void OnRenderObject()
        {
            m_commandBuffer.Clear();
            m_renderer.DrawPrimitive(
                DebugRenderer.PrimitiveType.Box,
                m_commandBuffer,
                transform,
                m_sceneObject.GetBoundingBox(),
                m_color
            );
            Graphics.ExecuteCommandBuffer(m_commandBuffer);
        }
    }
}
