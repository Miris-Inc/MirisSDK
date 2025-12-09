// Copyright © 2025 Miris, Inc. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Miris.Runtime
{
    public class ButtonSetManager : MonoBehaviour
    {
        [Header("Button Panel Pair Lists")]
        [SerializeField]
        private List<Button> m_buttons;
        [SerializeField]
        private List<GameObject> m_buttonActivatedPanels;

        [Header("Button Sprite Lists")]
        [SerializeField]
        private List<Sprite> m_buttonImageSpriteActive;
        [SerializeField]
        private List<Sprite> m_buttonImageSpriteInactive;

        [Header("Button Attributes")]
        private int m_buttonIndex = 0;
        [SerializeField]
        private Color m_buttonInactiveColor;
        [SerializeField]
        private Color m_buttonActiveColor;
        [SerializeField]
        private Color m_buttonDisabledColor;

        private MobileUserInterfaceManager m_uiManager;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            for (int buttonIndex = 0; buttonIndex < m_buttons.Count; buttonIndex++)
            {
                string name = m_buttons[buttonIndex].name;
                m_buttons[buttonIndex].onClick.AddListener(() => RefreshButtonsState(name));
                m_buttonActivatedPanels[buttonIndex].SetActive(false);
            }
            SetActiveButton(m_buttonIndex);
        }
        
        public void SetUIManager(MobileUserInterfaceManager uiManager)
        {
            m_uiManager = uiManager;
        }

        public void SetActiveButton(int index)
        {
            string buttonName = m_buttons[index].name;
            RefreshButtonsState(buttonName);
        }

        public void SetDefaultState()
        {
            SetActiveButton(1);
        }

        public int GetActiveButtonIndex()
        {
            return m_buttonIndex;
        }

        public void PreparePanel(bool isDeveloperMode)
        {
            int buttonCount = isDeveloperMode ? m_buttons.Count : m_buttons.Count - 1;
            float horizontalButtonPortion = 1.0f / buttonCount;
            float currentButtonOffset = 0f;
            for (int buttonIndex = 0; buttonIndex < m_buttons.Count; buttonIndex++)
            {
                if (buttonIndex <= m_buttons.Count)
                {
                    RectTransform buttonRect = m_buttons[buttonIndex]
                        .gameObject.GetComponent<RectTransform>();
                    buttonRect.anchorMin = new Vector2(currentButtonOffset, 0);
                    buttonRect.anchorMax = new Vector2(
                        currentButtonOffset + horizontalButtonPortion,
                        1
                    );
                    currentButtonOffset += horizontalButtonPortion;
                }
                else
                {
                    m_buttons[buttonIndex].gameObject.SetActive(false);
                }
            }
        }

        private void RefreshButtonsState(string selectedName)
        {
            for (int buttonIndex = 0; buttonIndex < m_buttons.Count; buttonIndex++)
            {
                string buttonName = m_buttons[buttonIndex].name;
                Image imageComponent = m_buttons[buttonIndex].GetComponentsInChildren<Image>()[1];
                if (selectedName == buttonName)
                {
                    m_uiManager?.SwitchPanelRefresh();

                    // Activate the selected button
                    m_buttonIndex = buttonIndex;
                    m_buttonActivatedPanels[buttonIndex].SetActive(true);
                    if (m_buttonActivatedPanels[buttonIndex].GetComponent<SlidingPanel>() != null)
                    {
                        m_buttonActivatedPanels[buttonIndex].GetComponent<SlidingPanel>().SlideOpen();
                    }
                    imageComponent.sprite = m_buttonImageSpriteActive[buttonIndex];
                    imageComponent.color = m_buttonActiveColor;
                }
                else
                {
                    // De-activate all other buttons
                    if (m_buttonActivatedPanels[buttonIndex].GetComponent<SlidingPanel>() != null)
                    {
                        if (m_buttonActivatedPanels[buttonIndex].activeSelf == true)
                        {
                            m_buttonActivatedPanels[buttonIndex]
                                .GetComponent<SlidingPanel>()
                                .SlideClose();
                        }
                    }
                    else
                    {
                        m_buttonActivatedPanels[buttonIndex].SetActive(false);
                    }
                    imageComponent.sprite = m_buttonImageSpriteInactive[buttonIndex];
                    imageComponent.color = m_buttonInactiveColor;
                }
            }
        }
    }
}