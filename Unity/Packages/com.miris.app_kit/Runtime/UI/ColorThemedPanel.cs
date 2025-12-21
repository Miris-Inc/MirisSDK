// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;
using UnityEngine.UI;

using TMPro;

using System.Collections;
using System.Collections.Generic;

namespace Miris.Runtime
{
    public class ColorThemedPanel : MonoBehaviour
    {
        [SerializeField]
        private List<GameObject> m_panels;

        [SerializeField]
        private Material m_lightModeMaterial;
        [SerializeField]
        private Material m_darkModeMaterial;

        [SerializeField]
        private Color m_lightModeTextColor;
        [SerializeField]
        private Color m_darkModeTextColor;

        public bool m_isDarkMode;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            SetTheme();
        }

        private void SetTheme()
        {
            Color colorToSet = (m_isDarkMode) ? m_darkModeTextColor : m_lightModeTextColor;
            Material materialToSet  = (m_isDarkMode) ? m_darkModeMaterial : m_lightModeMaterial;
            var textPanels = transform.GetComponentsInChildren<TMP_Text>(includeInactive:true);
            foreach(var text in textPanels)
            {
                text.color = colorToSet;
            }

            var panelImages = transform.GetComponentsInChildren<Image>(true);
            foreach(var image in panelImages)
            {
                image.material = materialToSet;
            }
        }

        // Update is called once per frame
        void Update()
        {
            
        }
    }
}
