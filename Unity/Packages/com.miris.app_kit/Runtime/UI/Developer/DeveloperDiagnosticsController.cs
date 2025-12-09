// Copyright (c) 2024 Miris. All rights reserved.

// Standard library
using System;

// Unity
using UnityEngine;
using UnityEngine.UI;

// Text mesh pro
using TMPro;

namespace Miris.Runtime
{
    public class DeveloperDiagnosticsController : DeveloperBaseController
    {
        // extraneous references
        [SerializeField]
        public UserInterfaceManager m_logConsoleInterfaceManager;

        [SerializeField]
        private GameObject m_profilerObject;

        // UI Toggles
        [SerializeField]
        private Toggle m_profilerToggle;

        [SerializeField]
        private Toggle m_showLogConsoleToggle;

        // --------------------------------------------------------------------
        // UI Update
        // --------------------------------------------------------------------

        // Update is called once per frame
        void Update()
        {
            SyncUI();
        }

        // --------------------------------------------------------------------
        // UI Initialization
        // --------------------------------------------------------------------

        private void InitializeProfilerToggle()
        {
            m_profilerToggle.isOn = m_profilerObject.activeSelf;
            m_profilerToggle.onValueChanged.AddListener(OnProfilerToggleValueChanged);
        }

        private void InitializeShowLogConsoleToggle()
        {
            m_showLogConsoleToggle.onValueChanged.AddListener(OnShowLogConsoleToggleChanged);
        }

        // Initialization of UI elements by populating dropdowns based on enums, etc
        public override void InitializeUI()
        {
            // initialize toggles
            InitializeProfilerToggle();
            InitializeShowLogConsoleToggle();
        }

        // --------------------------------------------------------------------
        // UI event handling
        // --------------------------------------------------------------------

        private void OnProfilerToggleValueChanged(bool value)
        {
            m_profilerObject.SetActive(value);
        }

        private void OnShowLogConsoleToggleChanged(bool value)
        {
            if (value)
            {
                m_logConsoleInterfaceManager.Maximize();
            }
            else
            {
                m_logConsoleInterfaceManager.Minimize();
            }
        }

        // --------------------------------------------------------------------
        // UI Teardown
        // --------------------------------------------------------------------

        public override void TeardownUI()
        {
            // Tear down Toggles
            m_profilerToggle.onValueChanged.RemoveListener(OnProfilerToggleValueChanged);
        }

        // --------------------------------------------------------------------
        // Synchronization of model data to UI
        // --------------------------------------------------------------------

        public override void SyncUI()
        {
           
        }
    }
}
