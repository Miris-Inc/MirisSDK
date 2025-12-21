// Copyright © 2025 Miris, Inc. All rights reserved.

// Standard library
using System;

// Unity
using UnityEngine;
using UnityEngine.UI;

// Text mesh pro
using TMPro;

namespace Miris.Runtime
{
    public class DeveloperSceneController : DeveloperBaseController
    {
        // extraneous references
        [SerializeField]
        public MirisStreamController m_streamController;

        // UI Buttons
        [SerializeField]
        private Button m_cancelAllExecutionButton;

        [SerializeField]
        private Button m_clearSceneButton;


        // UI Input fields
        [SerializeField]
        private TMP_InputField m_sceneOperatorsCountInputField;

        // --------------------------------------------------------------------
        // UI Update
        // --------------------------------------------------------------------

        // Update is called once per frame
        void Update()
        {
            if (m_streamController == null)
            {
                return;
            }

            SyncUI();
        }

        // --------------------------------------------------------------------
        // UI Initialization
        // --------------------------------------------------------------------

        // Initialization of UI elements by populating dropdowns based on enums, etc
        public override void InitializeUI()
        {
            m_cancelAllExecutionButton.onClick.AddListener(OnCancelAllExecutionButtonClicked);
        }

        // --------------------------------------------------------------------
        // UI event handling
        // --------------------------------------------------------------------

        private void OnCancelAllExecutionButtonClicked()
        {
            m_streamController.GetClient().CancelAllSceneExecution();
        }

        // --------------------------------------------------------------------
        // UI Teardown
        // --------------------------------------------------------------------

        public override void TeardownUI()
        {
            // Tear down Buttons
            m_cancelAllExecutionButton.onClick.RemoveListener(OnCancelAllExecutionButtonClicked);
        }

        // --------------------------------------------------------------------
        // Synchronization of model data to UI
        // --------------------------------------------------------------------

        public override void SyncUI()
        {
            SyncSceneOperatorCountInputField();
        }

        private void SyncSceneOperatorCountInputField()
        {
            m_sceneOperatorsCountInputField.SetTextWithoutNotify($"{m_streamController.GetClient().GetSceneOperatorCount()}");
        }
    }
}
