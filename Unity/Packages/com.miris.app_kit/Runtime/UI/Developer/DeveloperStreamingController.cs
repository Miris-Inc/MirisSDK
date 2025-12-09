// Copyright © 2025 Miris, Inc. All rights reserved.

// Unity
using UnityEngine;

// Text mesh pro
using TMPro;
using System;
using System.Linq;
using UnityEngine.UI;

namespace Miris.Runtime
{
    public class DeveloperStreamingController : DeveloperBaseController
    {
        // extraneous references
        [SerializeField]
        public MirisStreamController m_streamController;

        // UI Dropdown
        [SerializeField]
        private TMP_Dropdown m_environmentDropdown;

        [SerializeField]
        private TMP_InputField m_viewerKeyInputField;

        [SerializeField]
        private TMP_InputField m_tagsInputField;

        [SerializeField]
        private Button m_clearButton;

        // --------------------------------------------------------------------
        // UI Update
        // --------------------------------------------------------------------

        protected void Start()
        {
            InitializeUI();
        }

        protected void Update()
        {
            Debug.Assert(m_streamController != null && m_streamController.isActiveAndEnabled);
            SyncUI();
        }

        // --------------------------------------------------------------------
        // UI Initialization
        // --------------------------------------------------------------------

        private void OnEnable()
        {
            if (m_streamController.isActiveAndEnabled)
            {
                InitializeUI();
            }
        }

        private void OnDisable()
        {
            if (m_streamController.isActiveAndEnabled)
            {
                TeardownUI();
            }
        }

        private async void InitializeEnvironmentsDropdown()
        {
            string[] environments = await m_streamController.GetAssetManager().GetAvailableEnvironments();

            m_environmentDropdown.options.Clear();

            foreach (var environment in environments)
            {
                m_environmentDropdown.options.Add(new TMP_Dropdown.OptionData(environment));
            }

            m_environmentDropdown.onValueChanged.AddListener(OnEnvironmentDropdownChanged);
            m_environmentDropdown.RefreshShownValue();
        }

        private void InitializeViewerKeyInputField()
        {
            m_viewerKeyInputField.SetTextWithoutNotify(Preferences.instance.streaming.viewerKey);
            m_viewerKeyInputField.onValueChanged.AddListener(OnViewerKeyInputFieldChanged);
        }

        private void InitializeTagsDropdown()
        {
            SyncTagsInputField();
            m_tagsInputField.onValueChanged.AddListener(OnTagsInputFieldChanged);
        }

        // Initialization of UI elements by populating dropdowns based on enums, etc
        public override void InitializeUI()
        {
            TeardownUI();
            InitializeEnvironmentsDropdown();
            InitializeViewerKeyInputField();
            InitializeTagsDropdown();
            m_clearButton.onClick.AddListener(OnClearButtonClicked);
            m_streamController.GetAssetManager().ServerEnvironmentChanged += OnEnvironmentChanged;
        }

        // --------------------------------------------------------------------
        // UI event handling
        // --------------------------------------------------------------------

        private async void OnEnvironmentDropdownChanged(int selectedIndex)
        {
            string environment = m_environmentDropdown.options[selectedIndex].text;
            await m_streamController.GetAssetManager().SetServerEnvironment(environment);
        }

        private void OnViewerKeyInputFieldChanged(string viewerKeyValue)
        {
            string viewerKey = viewerKeyValue.Trim();

            // Store the exact user input in preferences.
            Preferences.instance.streaming.viewerKey = viewerKey;
            _ = Preferences.Save();

            // Get fallback value and apply to data model
            m_streamController.GetAssetManager().SetViewerKey(Preferences.instance.ResolveViewerKey(viewerKey));
        }

        private void OnTagsInputFieldChanged(string value)
        {
            string[] tags = value.Trim().Split(',', StringSplitOptions.RemoveEmptyEntries);

            // Store the exact user input in preferences.
            Preferences.instance.streaming.tags = tags;
            _ = Preferences.Save();

            // Concatnate fallback values and apply to data model.
            m_streamController.GetAssetManager().SetTags(Preferences.instance.ResolveTags(tags));
        }

        private void OnClearButtonClicked()
        {
            m_tagsInputField.text = "";
            m_viewerKeyInputField.text = "";
        }

        // --------------------------------------------------------------------
        // UI Teardown
        // --------------------------------------------------------------------

        public override void TeardownUI()
        {
            // Tear down dropdowns
            m_streamController.GetAssetManager().ServerEnvironmentChanged -= OnEnvironmentChanged;
            m_clearButton.onClick.RemoveListener(OnClearButtonClicked);
            m_environmentDropdown.onValueChanged.RemoveListener(OnEnvironmentDropdownChanged);
            m_viewerKeyInputField.onValueChanged.RemoveListener(OnViewerKeyInputFieldChanged);
            m_tagsInputField.onValueChanged.RemoveListener(OnTagsInputFieldChanged);
        }

        // --------------------------------------------------------------------
        // Synchronization of model data to UI
        // --------------------------------------------------------------------

        private void OnEnvironmentChanged(string environment)
        {
            SyncTagsInputField();
        }

        private void SyncTagsInputField()
        {
            string tagString = string.Join(",", Preferences.instance.streaming.tags);
            m_tagsInputField.SetTextWithoutNotify(tagString);
        }

        private void SyncEnvironmentDropdown()
        {
            int index = m_environmentDropdown.options.FindIndex(x => x.text == m_streamController.GetAssetManager().SelectedEnvironment);
            if (index == -1)
            {
                return;
            }
            m_environmentDropdown.SetValueWithoutNotify(index);
        }

        public override void SyncUI()
        {
            // sync dropdowns
            SyncEnvironmentDropdown();
        }
    }
}
