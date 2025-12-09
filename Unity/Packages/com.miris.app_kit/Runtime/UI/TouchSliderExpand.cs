// Copyright © 2025 Miris.All rights reserved.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Miris.Runtime
{
    public class TouchSliderExpand : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private bool m_isScrubbing = false;

        [SerializeField]
        private RectTransform m_rect;

        [SerializeField]
        private Slider m_slider;

        [SerializeField]
        private Image m_sliderBar;

        [SerializeField]
        public float m_expandedPortraitHeight = 0.18f;
        [SerializeField]
        public float m_condensedPortraitHeight = 0.16f;

        [SerializeField]
        public float m_expandedLandscapeHeight = 0.24f;
        [SerializeField]
        public float m_condensedLandscapeHeight = 0.21f;

        [SerializeField]
        public Color m_expandedColor;

        [SerializeField]
        public Color m_condensedColor;

        public bool IsScrubbing()
        {
            return m_isScrubbing;
        }

        private void AdjustSliderDimensions()
        {
            ScreenOrientation orientation = Screen.orientation;
            if (orientation == ScreenOrientation.LandscapeLeft || orientation == ScreenOrientation.LandscapeRight)
            {
               float maxAnchorY = m_isScrubbing ? m_expandedLandscapeHeight : m_condensedLandscapeHeight;
                m_rect.anchorMax = new Vector2(1.0f, maxAnchorY);
                m_rect.offsetMin = new Vector2(m_rect.offsetMin.x, 0f);
                m_rect.offsetMax = new Vector2(m_rect.offsetMax.x, 0f);
            } else {
                float maxAnchorY = m_isScrubbing ? m_expandedPortraitHeight : m_condensedPortraitHeight;
                m_rect.anchorMax = new Vector2(1.0f, maxAnchorY);
                m_rect.offsetMin = new Vector2(m_rect.offsetMin.x, 0f);
                m_rect.offsetMax = new Vector2(m_rect.offsetMax.x, 0f);
            }   

        }

        public void OnPointerDown(PointerEventData eventData)
        {
            m_isScrubbing = true;
            AdjustSliderDimensions();
            m_sliderBar.color = m_isScrubbing ? m_expandedColor : m_condensedColor;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            m_isScrubbing = false;
            AdjustSliderDimensions();
            m_sliderBar.color = m_isScrubbing ? m_expandedColor : m_condensedColor;
            m_slider.value += .001f;
        }
    }
}
