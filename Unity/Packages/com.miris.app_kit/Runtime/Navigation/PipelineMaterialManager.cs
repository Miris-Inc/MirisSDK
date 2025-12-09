// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;

namespace Miris.Runtime
{
    public class PipelineMaterialManager: MonoBehaviour
    {
        public Material m_Material_builtIn;
        public Material m_Material_urp;

        void Awake() {

            Renderer renderer = GetComponent<Renderer>();
            
            if (renderer == null) 
            {
                return;
            }
            
            bool isURP = GraphicsSettings.currentRenderPipeline != null;
            renderer.material = isURP ? m_Material_urp:m_Material_builtIn;
        }
    }
}
