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
        struct ButtonPanelPair
        {
            public Button button;
            public GameObject panel;
            public Action listener;
        }


        // extraneous references
        [SerializeField]
        private Button m_restoreDefaultSettingsButton;


        [SerializeField]
        public TMP_Text m_buildVersionText;

        [SerializeField]
        public MirisStreamController m_streamController;

        [SerializeField]
        private MirisPlayerPreferences m_mirisPlayerPrefs;


        [Header("Menu Tabs")]
        [SerializeField]
        private List<GameObject> m_tabPanels;
        
        [SerializeField]
        private List<Button> m_tabButtons;

        private List<ButtonPanelPair> m_panelButtonSets = new List<ButtonPanelPair>();

      
        // --------------------------------------------------------------------
        // Unity event handling
        // --------------------------------------------------------------------

        void Start()
        {
            InitializeUI();

            // On start-up, let the scene tab be the active one.
            m_panelButtonSets[0].button.onClick.Invoke();
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
            for(int tabIndex =0;tabIndex < m_tabPanels.Count;tabIndex++)
            {
                int index = tabIndex;
                m_panelButtonSets.Add(new ButtonPanelPair 
                {
                    button = m_tabButtons[tabIndex],
                    panel = m_tabPanels[tabIndex],
                    listener = () => OnPanelButtonClicked(index)
                });
            }

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
                m_panelButtonSets[i].button.onClick.RemoveListener(m_panelButtonSets[i].listener.Invoke);
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
