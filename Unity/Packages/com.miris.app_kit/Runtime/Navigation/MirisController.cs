// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;
using UnityEngine.XR;

namespace Miris.Runtime
{
    public class MirisController : MonoBehaviour
    {
        [Header("Miris Controller Components")]
        [SerializeField]
        protected MirisStream m_stream;
        [SerializeField]
        public MirisStreamController m_streamController;
        [SerializeField]
        private SceneSelector m_sceneSelector;
        protected PlayerInputActions m_inputActions;

        protected bool IsDeveloperMode()
        {
            return !XRSettings.isDeviceActive || Debug.isDebugBuild;
        }

        // --------------------------------------------------------------------
        // Unity overrides
        // --------------------------------------------------------------------
        protected void OnEnable() {
            m_streamController.GetAssetManager().ServerEnvironmentChanged += EnvironmentChanged;
            m_streamController.GetAssetManager().TagsChanged += TagsChanged;
        
            if(m_inputActions == null)
            {
                m_inputActions = new();
            }

            m_inputActions.Enable();
        }

        protected void OnDisable()
        {
            if (m_streamController != null && m_streamController.isActiveAndEnabled)
            {
                m_streamController.GetAssetManager().TagsChanged -= TagsChanged;
                m_streamController.GetAssetManager().ServerEnvironmentChanged -= EnvironmentChanged;
            }
            m_inputActions.Disable();
        }

        // --------------------------------------------------------------------
        // Scene Management functions
        // --------------------------------------------------------------------
        private async void EnvironmentChanged(string environment)
        {
            await m_sceneSelector.AssetSourceChanged();
        }

        private async void TagsChanged()
        {
            await m_sceneSelector.AssetSourceChanged();
        }
    }
}
