// Copyright © 2025 Miris, Inc. All rights reserved.

using System.Threading.Tasks;
using System.Collections;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

namespace Miris.Runtime
{
    public class MirisTouchController : MonoBehaviour
    {
        [Header("Scene Selection & Management")]
        [SerializeField]
        private string m_scenePath;
        [SerializeField]
        public SceneSelector m_sceneSelector;
        [SerializeField]
        private MirisPlayerSceneManager m_sceneManager;

        [Header("UI/UX Components")]
        [SerializeField]
        private MobileUserInterfaceManager m_uiManager;
        [SerializeField]
        private ButtonSetManager m_tabButtonManager;

        [Header("Miris Components")]
        [SerializeField]
        private MirisStreamController m_streamController;
        [SerializeField]
        private MirisStream m_stream;

        private TouchControls m_touchControls = new TouchControls();
        private PlayerInputActions m_playerTouchActions;

        private Vector3 m_objectFrameOffset = new Vector3(0f, -1f, 2f);

        // --------------------------------------------------------------------
        // Frame scene related functions
        // --------------------------------------------------------------------
        public void FrameObject()
        {
            Quaternion yawRotation = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
            Vector3 rotatedOffset = yawRotation * m_objectFrameOffset;

            m_stream.transform.position = Camera.main.transform.position + rotatedOffset;
            m_stream.transform.rotation = yawRotation;
        }

        // --------------------------------------------------------------------
        // Unity overrides
        // --------------------------------------------------------------------
        void Start()
        {
            if(m_stream != null)
            {
                m_stream.m_onLoadActions.Add(() => FrameObject());
            }
        }


        public void Awake()
        {
            m_touchControls.SetUIManager(m_uiManager);
        }

        private bool IsDeveloperMode()
        {
            return !XRSettings.isDeviceActive || Debug.isDebugBuild;
        }

        private void PrepareButtonPanel()
        {
            m_tabButtonManager.PreparePanel(IsDeveloperMode());
        }

        private void OnEnable()
        {
            m_streamController.GetAssetManager().ServerEnvironmentChanged += EnvironmentChanged;
            m_streamController.GetAssetManager().TagsChanged += TagsChanged;

            if(m_playerTouchActions == null)
            {
                m_playerTouchActions = new();
            }

            m_touchControls.Enable(m_playerTouchActions);

            m_playerTouchActions.Enable();

            PrepareButtonPanel();
        }

        private void OnDisable()
        {
            m_playerTouchActions.Disable();

            if (m_streamController != null && m_streamController.isActiveAndEnabled)
            {
                m_streamController.GetAssetManager().TagsChanged -= TagsChanged;
                m_streamController.GetAssetManager().ServerEnvironmentChanged -= EnvironmentChanged;
            }
        }

        // --------------------------------------------------------------------
        // UI handling
        // -------------------------------------------------------------------- 
        private async void EnvironmentChanged(string environment)
        {
            // See MirisPlayerController.EnvironmentChanged
            
            // In the Miris Player, we shouldn't need to call
            // m_streamController.GetClient().SetAssetViewerKey
            // DeveloperStreamingController/AssetManager already does that

            await m_sceneSelector.AssetSourceChanged();
        }

        private async void TagsChanged()
        {
            await m_sceneSelector.AssetSourceChanged();
        }
    }
}
