// Copyright © 2025 Miris, Inc. All rights reserved.

// Standard library
using System;
using System.Collections.Generic;

// Unity
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Text mesh pro
using TMPro;

namespace Miris.Runtime
{

    // DeveloperUIController synchronizes state between the data model (MirisStreamController, and other global state) 
    // to-and-from UI elements.
    public class DeveloperUIController : MonoBehaviour
    {
        struct ButtonPanelPair
        {
            public Button button;
            public GameObject panel;
            public Action listener;
        }

        private List<ButtonPanelPair> m_panelButtonSets = new List<ButtonPanelPair>();

        // extraneous references
        [SerializeField]
        private Button m_restoreDefaultSettingsButton;


        [SerializeField]
        public TMP_Text m_buildVersionText;


        [SerializeField]
        public MirisStreamController m_streamController;

        [SerializeField]
        private MirisPlayerPreferences m_mirisPlayerPrefs;

        [SerializeField]
        private GameObject m_scenePanel;


        [SerializeField]
        private GameObject m_streamingPanel;

        [SerializeField]
        private GameObject m_diagnosticsPanel;

        [SerializeField]
        private GameObject m_lodPanel;

        [SerializeField]
        private GameObject m_graphicsPanel;


        // UI Buttons
        [SerializeField]
        private Button m_scenePanelButton;

        [SerializeField]
        private Button m_streamingPanelButton;

        [SerializeField]
        private Button m_diagnosticsPanelButton;

        [SerializeField]
        private Button m_lodPanelButton;

        [SerializeField]
        private Button m_graphicsPanelButton;

        // --------------------------------------------------------------------
        // Unity event handling
        // --------------------------------------------------------------------

        void Start()
        {
            InitializeUI();

            // On start-up, let the scene tab be the active one.
            m_scenePanelButton.onClick.Invoke();
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
            InitializeBuildVersionText();
            m_restoreDefaultSettingsButton.onClick.AddListener(OnRestoreDefaultSettingsClicked);

            // initalize panel button tabs
            m_panelButtonSets.Add(new ButtonPanelPair
            {
                button = m_scenePanelButton,
                panel = m_scenePanel,
                listener = () => OnPanelButtonClicked(0)
            });

            m_panelButtonSets.Add(new ButtonPanelPair
            {
                button = m_streamingPanelButton,
                panel = m_streamingPanel,
                listener = () => OnPanelButtonClicked(1)
            });

            m_panelButtonSets.Add(new ButtonPanelPair
            {
                button = m_diagnosticsPanelButton,
                panel = m_diagnosticsPanel,
                listener = () => OnPanelButtonClicked(2)
            });

            m_panelButtonSets.Add(new ButtonPanelPair
            {
                button = m_lodPanelButton,
                panel = m_lodPanel,
                listener = () => OnPanelButtonClicked(3)
            });

            m_panelButtonSets.Add(new ButtonPanelPair
            {
                button = m_graphicsPanelButton,
                panel = m_graphicsPanel,
                listener = () => OnPanelButtonClicked(4)
            });

            for (var i = 0; i < m_panelButtonSets.Count; i++)
            {
                m_panelButtonSets[i].button.onClick.AddListener(m_panelButtonSets[i].listener.Invoke);
            }
        }


        private void InitializeBuildVersionText()
        {
            m_buildVersionText.text = $"Build {Application.version} ({VersionInfoReader.Info.m_androidBundleVersion})";
        }

        // --------------------------------------------------------------------
        // UI Teardown
        // --------------------------------------------------------------------

        private void TeardownUI()
        {
            for (var i = 0; i < m_panelButtonSets.Count; i++)
            {
                m_panelButtonSets[i].button.onClick.AddListener(m_panelButtonSets[i].listener.Invoke);
            }
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
            for (var i = 0; i < m_panelButtonSets.Count; i++)
            {
                if (i != buttonIndex)
                {
                    m_panelButtonSets[i].panel.SetActive(false);

                    // When tab is de-activated, make its tab button background transparent
                    ColorBlock colorBlock = m_panelButtonSets[i].button.colors;
                    colorBlock.normalColor = new Color(255, 255, 255, 0);
                    m_panelButtonSets[i].button.colors = colorBlock;
                }
            }

            {
                // When tab is activated, make its tab button background highlighted
                m_panelButtonSets[buttonIndex].panel.SetActive(true);
                ColorBlock colorBlock = m_panelButtonSets[buttonIndex].button.colors;
                colorBlock.normalColor = colorBlock.highlightedColor;
                m_panelButtonSets[buttonIndex].button.colors = colorBlock;
            }
        }

        private void OnRestoreDefaultSettingsClicked()
        {
            m_mirisPlayerPrefs.RestoreDefaultPreferences();
        }

    }
}
