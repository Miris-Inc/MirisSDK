// Copyright © 2025 Miris, Inc. All rights reserved.

using System.Threading.Tasks;
using System.Collections;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace Miris.Runtime
{
    public class MirisTouchController : MirisController
    {
        [Header("UI/UX Components")]
        [SerializeField]
        private MobileUserInterfaceManager m_uiManager;
        [SerializeField]
        private ButtonSetManager m_tabButtonManager;

        private TouchControls m_touchControls = new TouchControls();

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

        private void PrepareButtonPanel()
        {
            m_tabButtonManager.PreparePanel(IsDeveloperMode());
        }

        private void OnEnable()
        {
            base.OnEnable();

            m_touchControls.Enable(m_inputActions);

            PrepareButtonPanel();
        }

        private void OnDisable()
        {
            base.OnDisable();
        }
    }
}
