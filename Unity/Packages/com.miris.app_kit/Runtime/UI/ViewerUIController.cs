// Copyright © 2025 Miris, Inc. All rights reserved.

// Standard library
using System;
using System.Collections.Generic;

// Unity
using UnityEngine;
using UnityEngine.UI;

// Text mesh pro
using TMPro;

namespace Miris.Runtime
{
    public class ViewerUIController : MonoBehaviour
    {
        [SerializeField]
        public MirisStreamController m_streamController;

        // --------------------------------------------------------------------
        // Unity event handling
        // --------------------------------------------------------------------

        void Start()
        {
            InitializeUI();
        }

        void Update()
        {

            SyncUI();
        }

        void OnDestroy()
        {
            TeardownUI();
        }

        // --------------------------------------------------------------------
        // UI Initialization
        // --------------------------------------------------------------------

        // Initialization of UI elements by populating dropdowns based on enums, etc
        private void InitializeUI()
        {
        }


        // --------------------------------------------------------------------
        // UI Teardown
        // --------------------------------------------------------------------

        private void TeardownUI()
        {
        }

        // --------------------------------------------------------------------
        // Synchronization of model data to UI
        // --------------------------------------------------------------------

        private void SyncUI()
        {

        }

        // --------------------------------------------------------------------
        // UI event handling
        // --------------------------------------------------------------------

        void OnPanelButtonClicked(int buttonIndex)
        {
        }

        private void OnRestoreDefaultSettingsClicked()
        {
        }
    }
}
