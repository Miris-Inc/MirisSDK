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
    public class AssetsPanelController : DeveloperBaseController
    {
        [SerializeField]
        public MirisStreamController m_streamController;

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

        // Initialization of UI elements by populating dropdowns based on enums, etc
        public override void InitializeUI()
        {
        }

        // --------------------------------------------------------------------
        // UI event handling
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        // UI Teardown
        // --------------------------------------------------------------------

        public override void TeardownUI()
        {
        }

        // --------------------------------------------------------------------
        // Synchronization of model data to UI
        // --------------------------------------------------------------------

        public override void SyncUI()
        {
        }

        private void SyncAssetViewerKeyInputField()
        {
        }
    }
}
