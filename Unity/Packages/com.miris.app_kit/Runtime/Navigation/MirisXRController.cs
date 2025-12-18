// Copyright © 2025 Miris, Inc. All rights reserved.

using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using UnityEngine.XR.Interaction.Toolkit;

namespace Miris.Runtime
{
    public class MirisXRController : MirisController
    {
        [Header("XR Hand Related Variables")]
        [SerializeField]
        private InputActionReference m_leftActivateAction;
        [SerializeField]
        private InputActionReference m_rightActivateAction;
        [SerializeField]
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor m_rightRayInteractor;

        [SerializeField]
        public GameObject m_xrUserInterface;
        [SerializeField]
        public GameObject m_xrDeveloperInterface;

        private Vector3 m_objectFrameOffset = new Vector3(0f, -1f, 2f);

        [SerializeField]
        private GameObject m_resetInterface;
        private float? m_clearSceneTime = null;
        // time required, in seconds, to fill the visual reset slider and clear the scene contents
        private float m_timeToClear = 1.0f;

        // --------------------------------------------------------------------
        // Unity overrides
        // --------------------------------------------------------------------

        public void Awake()
        {
            if (Debug.isDebugBuild || SystemInfo.deviceType == DeviceType.Desktop)
            {
                gameObject.AddComponent<FlyCamera>();
            }
        }

        private void Start()
        {
            if (!XRUtils.IsXR())
            {
                m_rightRayInteractor.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>().lineLength = 0.0f;
            }

            m_xrDeveloperInterface.SetActive(IsDeveloperMode());
        }

        private void OnEnable()
        {
            base.OnEnable();
            if (IsDeveloperMode())
            {
                m_inputActions.XR.MinMaxDeveloperInterface.performed += OnMinMaxDeveloperInterfacePerformed;
            }
            m_inputActions.XR.MinMaxUserInterface.performed += OnMinMaxUserInterfacePerformed;
            m_inputActions.XR.AttemptClearScene.performed += OnClearScenePerformed;
            m_inputActions.XR.AttemptClearScene.canceled += OnClearSceneCancelled;
            if(m_stream != null)
            {
                m_stream.m_onLoadActions.Add(() => FrameObject());
            }
        }

        private void OnDisable()
        {
            base.OnDisable();
        }

        void Update() 
        {
            CheckClearSceneProgress();
        }

        // --------------------------------------------------------------------
        // UI handling
        // -------------------------------------------------------------------- 
        public float GetClearSceneProgress()
        {
            if (m_clearSceneTime.HasValue)
            {
                float percentage = (Time.time - m_clearSceneTime.Value) / m_timeToClear;
                percentage = (percentage >= 1.0) ? 1.0f : percentage;

                Slider[] sliders = m_resetInterface.GetComponentsInChildren<Slider>();
                sliders[0].value = percentage;


                return percentage;
            }
            return 0.0f;
        }

        private void CancelClearSceneTime()
        {
            m_clearSceneTime = null;
            m_resetInterface.GetComponent<UserInterfaceManager>().ForceMinimize();
        }

        private void CheckClearSceneProgress()
        {
            if (GetClearSceneProgress() >= 1.0f)
            {
                CancelClearSceneTime();
                m_stream.m_assetId = string.Empty;
            }
        }

        private void OnClearScenePerformed(InputAction.CallbackContext context)
        {
            if (!m_stream.IsLoaded())
            {
                return;
            }
            m_clearSceneTime = Time.time;
            m_resetInterface.GetComponent<UserInterfaceManager>().ForceMaximize();
        }

        private void OnClearSceneCancelled(InputAction.CallbackContext context)
        {
            CancelClearSceneTime();
        }

        private void OnMinMaxUserInterfacePerformed(InputAction.CallbackContext context)
        {
            m_xrUserInterface.GetComponent<UserInterfaceManager>().ToggleInterfaceSize();
        }

        private void OnMinMaxDeveloperInterfacePerformed(InputAction.CallbackContext context)
        {
            m_xrDeveloperInterface.GetComponent<UserInterfaceManager>().ToggleInterfaceSize();
        }
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
    }
}
