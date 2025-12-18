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

        [SerializeField]
        public MirisPlayerSceneManager m_sceneManager;

        [SerializeField]
        GameObject m_xrFloorObject;

        // UI Toggles
        [SerializeField]
        private Toggle m_xrFloorToggle;

        // UI Buttons
        [SerializeField]
        private Button m_cancelAllExecutionButton;

        [SerializeField]
        private Button m_clearSceneButton;


        // UI Input fields
        [SerializeField]
        private TMP_InputField m_scenePathInputField;

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

        private void InitializeXrFloorToggle()
        {
            m_xrFloorToggle.isOn = m_xrFloorObject.activeSelf;
            m_xrFloorToggle.onValueChanged.AddListener(OnXrFloorToggleValueChanged);
        }

        // Initialization of UI elements by populating dropdowns based on enums, etc
        public override void InitializeUI()
        {
            // initialize toggles
            InitializeXrFloorToggle();

            m_cancelAllExecutionButton.onClick.AddListener(OnCancelAllExecutionButtonClicked);
            m_clearSceneButton.onClick.AddListener(OnClearSceneButtonClicked);
        }

        // --------------------------------------------------------------------
        // UI event handling
        // --------------------------------------------------------------------

        private void OnXrFloorToggleValueChanged(bool value)
        {
            m_xrFloorObject.SetActive(value);
        }

        private void OnCancelAllExecutionButtonClicked()
        {
            m_streamController.GetClient().CancelAllSceneExecution();
        }

        private void OnClearSceneButtonClicked()
        {
            m_sceneManager.ClearScene();
        }

        // --------------------------------------------------------------------
        // UI Teardown
        // --------------------------------------------------------------------

        public override void TeardownUI()
        {
            // Tear down Toggles
            m_xrFloorToggle.onValueChanged.RemoveListener(OnXrFloorToggleValueChanged);

            // Tear down Buttons
            m_cancelAllExecutionButton.onClick.RemoveListener(OnCancelAllExecutionButtonClicked);
            m_clearSceneButton.onClick.RemoveListener(OnClearSceneButtonClicked);
        }

        // --------------------------------------------------------------------
        // Synchronization of model data to UI
        // --------------------------------------------------------------------

        public override void SyncUI()
        {
            SyncScenePathInputField();
            SyncSceneOperatorCountInputField();
        }

        private void SyncScenePathInputField()
        {
            m_scenePathInputField.SetTextWithoutNotify(m_sceneManager.GetAssetId());
        }


        private void SyncSceneOperatorCountInputField()
        {
            m_sceneOperatorsCountInputField.SetTextWithoutNotify($"{m_streamController.GetClient().GetSceneOperatorCount()}");
        }
    }
}
