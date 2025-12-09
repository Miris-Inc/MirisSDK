// Copyright (c) 2024 Miris. All rights reserved.

// Standard library
using System;

// Text mesh pro
using TMPro;


// Unity
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Miris.Runtime
{
    public class DeveloperGraphicsController : DeveloperBaseController
    {
        // extraneous references
        [SerializeField]
        public MirisStreamController m_streamController;

        [SerializeField]
        private MirisPlayerPreferences m_mirisPlayerPrefs;

        [SerializeField]
        private MirisPlayerController m_playerController;

        // UI Toggles
        [SerializeField]
        private Toggle m_fadeLargeSplatsToggle;

        // UI Dropdowns

        [SerializeField]
        private TMP_Dropdown m_renderPipelineDropdown;

        [SerializeField]
        private TMP_Dropdown m_displayFrequencyDropdown;

        [SerializeField]
        private TMP_Dropdown m_drawModeDropdown;

        [SerializeField]
        private TMP_Dropdown m_pointsDrawModeDropdown;

        [SerializeField]
        private TMP_Dropdown m_pointsSHAxisDropdown;

        [SerializeField]
        private TMP_Dropdown m_pointsSHChannelDropdown;

        // UI Sliders 
        [SerializeField]
        private Slider m_gaussianSigmaThresholdSlider;

        [SerializeField]
        private Slider m_alphaCullingThresholdSlider;

        [SerializeField]
        private Slider m_pointsFlatnessSlider;

        [SerializeField]
        private Slider m_shOrderSlider;

        // UI Input Fields
        private TMP_InputField m_gaussianSigmaThresholdInputField;
        private TMP_InputField m_alphaCullingThresholdInputField;
        private TMP_InputField m_pointsFlatnessInputField;
        private TMP_InputField m_shOrderInputField;

        private float[] m_refreshRates = { 72.0f, 90.0f, 120.0f }; // common refresh rates.

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

        private void InitializeDisplayFrequencyDropdown()
        {
#if UNITY_ANDROID && UNITY_XR_PROVIDERS_OPENXR
            var xrDisplay = UnityEngine.XR.XRSettings.displaySubsystem;
            if (xrDisplay == null)
            {
                m_displayFrequencyDropdown.interactable = false;
                return;
            }

            foreach (float value in m_refreshRates)
            {
                m_displayFrequencyDropdown.options.Add(new TMP_Dropdown.OptionData(value.ToString()));
            }

            m_displayFrequencyDropdown.onValueChanged.AddListener(OnDisplayFrequencyDropdownChanged);
            m_displayFrequencyDropdown.RefreshShownValue();
#endif
        }

        private void InitializeFadeLargeSplatsToggle()
        {
            m_fadeLargeSplatsToggle.onValueChanged.AddListener(OnFadeLargeSplatsValueChanged);
        }

        // Initialization of UI elements by populating dropdowns based on enums, etc
        public override void InitializeUI()
        {
            // initialize Toggles
            InitializeFadeLargeSplatsToggle();

            // initialize sliders
            InitializeSlider(
               m_gaussianSigmaThresholdSlider,
               ref m_gaussianSigmaThresholdInputField,
               typeof(GaussianSplatRenderOptions),
               "m_gaussianSigmaThreshold",
               OnGaussianSigmaThresholdSliderValueChanged,
               OnGaussianSigmaThresholdInputFieldValueChanged
            );
            InitializeSlider(
                m_alphaCullingThresholdSlider,
                ref m_alphaCullingThresholdInputField,
                typeof(GaussianSplatRenderOptions),
                "m_alphaCullingThreshold",
                OnAlphaCullingThresholdSliderValueChanged,
                OnAlphaCullingThresholdInputFieldValueChanged
            );
            InitializeSlider(
                m_pointsFlatnessSlider,
                ref m_pointsFlatnessInputField,
                typeof(GaussianSplatRenderOptions),
                "m_pointsFlatnessPercent",
                OnPointsFlatnessSliderValueChanged,
                OnPointsFlatnessInputFieldValueChanged
            );
            InitializeSlider(
                m_shOrderSlider,
                ref m_shOrderInputField,
                typeof(GaussianSplatRenderOptions),
                "m_SHOrder",
                OnSHOrderSliderValueChanged,
                OnSHOrderInputFieldValueChanged
            );

            // initialize dropdowns
            InitializeDisplayFrequencyDropdown();
            UiUtils.InitializeEnumDropdown(m_drawModeDropdown, typeof(GeometryRenderer.GeometryDrawMode), OnDrawModeDropdownChanged);
            UiUtils.InitializeEnumDropdown(m_pointsDrawModeDropdown, typeof(PointRenderer.PointDrawMode), OnPointsDrawModeDropdownChanged);
            UiUtils.InitializeEnumDropdown(m_pointsSHAxisDropdown, typeof(PointRenderer.SHAxis), OnPointsSHAxisDropdownChanged);
            UiUtils.InitializeEnumDropdown(m_pointsSHChannelDropdown, typeof(PointRenderer.SHChannel), OnPointsSHChannelDropdownChanged);
            UiUtils.InitializeEnumDropdown(m_renderPipelineDropdown, typeof(GaussianSplatRenderComponent.Pipeline), OnRenderPipelineDropdownChanged);

            // And ensure the UI looks decent

            SyncUI();
        }

        private void InitializeSlider(
          Slider slider,
          ref TMP_InputField inputField,
          Type classType,
          string fieldName,
          UnityEngine.Events.UnityAction<float> sliderListenerFunc,
          UnityEngine.Events.UnityAction<string> inputFieldListenerFunc
      )
        {
            ReflectionUtils.GetFloatFieldRange(classType, fieldName, out float min, out float max);
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(sliderListenerFunc);

            inputField = slider.transform.parent.GetComponentInChildren<TMP_InputField>();
            Debug.Assert(inputField != null, "Must find corresponding text field for slider");
            inputField.onValueChanged.AddListener(inputFieldListenerFunc);
        }

        // --------------------------------------------------------------------
        // UI event handling
        // --------------------------------------------------------------------

        private void OnRenderPipelineDropdownChanged(int selectedIndex)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_renderPipeline = (GaussianSplatRenderComponent.Pipeline)selectedIndex;
            }
        }

        private void OnDisplayFrequencyDropdownChanged(int selectedIndex)
        {
#if UNITY_ANDROID && UNITY_XR_PROVIDERS_OPENXR
            string displayFrequencyString = m_displayFrequencyDropdown.options[selectedIndex].text;
            float targetRate = float.Parse(displayFrequencyString);

            try
            {
                UnityEngine.Debug.Log($"Attempting to set display frequency to {targetRate}Hz");

                var xrDisplay = UnityEngine.XR.XRSettings.displaySubsystem;
                if (xrDisplay != null && xrDisplay.running)
                {
                    bool success = xrDisplay.TrySetDisplayRefreshRate(targetRate);
                    if (success)
                    {
                        UnityEngine.Debug.Log($"Successfully set display frequency to {targetRate}Hz");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"Failed to set display frequency to {targetRate}Hz - rate may not be supported");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogError("XR Display subsystem is not available or not running");
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to set display frequency: {e.Message}");
            }
#endif
        }

        private void OnDrawModeDropdownChanged(int selectedIndex)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_drawMode = (GeometryRenderer.GeometryDrawMode)selectedIndex;
            }
        }

        private void OnPointsDrawModeDropdownChanged(int selectedIndex)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_pointsDrawMode = (PointRenderer.PointDrawMode)selectedIndex;
            }
        }

        private void OnPointsSHAxisDropdownChanged(int selectedIndex)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_pointsSHAxis = (PointRenderer.SHAxis)selectedIndex;
            }
        }

        private void OnPointsSHChannelDropdownChanged(int selectedIndex)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_pointsSHChannel = (PointRenderer.SHChannel)selectedIndex;
            }
        }

        private void OnGaussianSigmaThresholdSliderValueChanged(float value)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_gaussianSigmaThreshold = value;
            }
        }
        private void OnGaussianSigmaThresholdInputFieldValueChanged(string value)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_gaussianSigmaThreshold = float.Parse(value);
            }
        }

        private void OnAlphaCullingThresholdSliderValueChanged(float value)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_alphaCullingThreshold = value;
            }
        }
        private void OnAlphaCullingThresholdInputFieldValueChanged(string value)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_alphaCullingThreshold = float.Parse(value);
            }
        }

        private void OnPointsFlatnessSliderValueChanged(float value)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_pointsFlatnessPercent = (int)value;
            }
        }
        private void OnPointsFlatnessInputFieldValueChanged(string value)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_pointsFlatnessPercent = int.Parse(value);
            }
        }

        private void OnSHOrderSliderValueChanged(float value)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_SHOrder = (int)value;
            }
        }

        private void OnSHOrderInputFieldValueChanged(string value)
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                rendererComponent.m_SHOrder = int.Parse(value);
            }
        }

        private void OnFadeLargeSplatsValueChanged(bool value)
        {
            m_streamController.fadeLargeSplats = value;
            m_mirisPlayerPrefs.SavePreferences();
        }

        // --------------------------------------------------------------------
        // UI Teardown
        // --------------------------------------------------------------------

        public override void TeardownUI()
        {
            // Tear down Toggles
            m_fadeLargeSplatsToggle.onValueChanged.RemoveListener(OnFadeLargeSplatsValueChanged);

            // Tear down Dropdowns
            m_displayFrequencyDropdown.onValueChanged.RemoveListener(OnDisplayFrequencyDropdownChanged);
            m_drawModeDropdown.onValueChanged.RemoveListener(OnDrawModeDropdownChanged);
            m_pointsDrawModeDropdown.onValueChanged.RemoveListener(OnPointsDrawModeDropdownChanged);
            m_pointsSHAxisDropdown.onValueChanged.RemoveListener(OnPointsSHAxisDropdownChanged);
            m_pointsSHChannelDropdown.onValueChanged.RemoveListener(OnPointsSHChannelDropdownChanged);
            m_renderPipelineDropdown.onValueChanged.RemoveListener(OnRenderPipelineDropdownChanged);

            // Tear down sliders
            m_gaussianSigmaThresholdSlider.onValueChanged.RemoveListener(OnGaussianSigmaThresholdSliderValueChanged);
            m_gaussianSigmaThresholdInputField.onValueChanged.RemoveListener(OnGaussianSigmaThresholdInputFieldValueChanged);
            m_alphaCullingThresholdSlider.onValueChanged.RemoveListener(OnAlphaCullingThresholdSliderValueChanged);
            m_alphaCullingThresholdInputField.onValueChanged.RemoveListener(OnAlphaCullingThresholdInputFieldValueChanged);
            m_pointsFlatnessSlider.onValueChanged.RemoveListener(OnPointsFlatnessSliderValueChanged);
            m_pointsFlatnessInputField.onValueChanged.RemoveListener(OnPointsFlatnessInputFieldValueChanged);
            m_shOrderSlider.onValueChanged.RemoveListener(OnSHOrderSliderValueChanged);
            m_shOrderInputField.onValueChanged.RemoveListener(OnSHOrderInputFieldValueChanged);
        }

        // --------------------------------------------------------------------
        // Synchronization of model data to UI
        // --------------------------------------------------------------------

        public override void SyncUI()
        {
            // sync toggles
            SyncFadeLargeSplatsToggle();

            // sync dropdowns
            SyncDisplayFrequencyDropdown();
            SyncDrawModeDropdown();
            SyncPointsDrawModeDropdown();
            SyncPointsSHAxisDropdown();
            SyncPointsSHChannelDropdown();
            SyncRenderPipelineDropdown();

            // sync sliders
            SyncGaussianSigmaThresholdSlider();
            SyncAlphaCullingThresholdSlider();
            SyncPointsFlatnessSlider();
            SyncSHOrderSlider();
        }

        private void SyncFadeLargeSplatsToggle()
        {
            m_fadeLargeSplatsToggle.SetIsOnWithoutNotify(m_streamController.fadeLargeSplats);
        }

        private void SyncDisplayFrequencyDropdown()
        {
#if UNITY_ANDROID && UNITY_XR_PROVIDERS_OPENXR
            // Get current refresh rate from XR display subsystem
            var xrDisplay = UnityEngine.XR.XRSettings.displaySubsystem;
            float currentRate = 72.0f; // Default fallback

            if (xrDisplay != null && xrDisplay.running)
            {
                if (xrDisplay.TryGetDisplayRefreshRate(out float actualRate))
                {
                    currentRate = actualRate;
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Could not retrieve current display refresh rate, using default 90Hz");
                }
            }

            // Find the closest rate in dropdown options
            int index = -1;
            float minDifference = float.MaxValue;
            for (int i = 0; i < m_refreshRates.Length; i++)
            {
                float difference = Mathf.Abs(m_refreshRates[i] - currentRate);
                if (difference < minDifference)
                {
                    minDifference = difference;
                    index = i;
                }
            }

            if (index >= 0)
            {
                m_displayFrequencyDropdown.SetValueWithoutNotify(index);
            }
#endif
        }

        private void SyncDrawModeDropdown()
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                m_drawModeDropdown.SetValueWithoutNotify((int)rendererComponent.m_drawMode);
            }
        }

        private void SyncPointsDrawModeDropdown()
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                m_pointsDrawModeDropdown.SetValueWithoutNotify((int)rendererComponent.m_pointsDrawMode);
            }
        }

        private void SyncPointsSHAxisDropdown()
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                m_pointsSHAxisDropdown.SetValueWithoutNotify((int)rendererComponent.m_pointsSHAxis);
            }
        }

        private void SyncPointsSHChannelDropdown()
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                m_pointsSHChannelDropdown.SetValueWithoutNotify((int)rendererComponent.m_pointsSHChannel);
            }
        }

        private void SyncRenderPipelineDropdown()
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                m_renderPipelineDropdown.SetValueWithoutNotify((int)rendererComponent.m_renderPipeline);
            }
        }

        private void SyncGaussianSigmaThresholdSlider()
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                SyncSliderValue(m_gaussianSigmaThresholdSlider, m_gaussianSigmaThresholdInputField, rendererComponent.m_gaussianSigmaThreshold);
            }
        }
        private void SyncAlphaCullingThresholdSlider()
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                SyncSliderValue(m_alphaCullingThresholdSlider, m_alphaCullingThresholdInputField, rendererComponent.m_alphaCullingThreshold);
            }
        }
        private void SyncSHOrderSlider()
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                SyncSliderValue(m_shOrderSlider, m_shOrderInputField, rendererComponent.m_SHOrder);
            }
        }

        private void SyncPointsFlatnessSlider()
        {
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                SyncSliderValue(m_pointsFlatnessSlider, m_pointsFlatnessInputField, rendererComponent.m_pointsFlatnessPercent);
            }
        }

        private void SyncSliderValue(Slider slider, TMP_InputField inputField, float value)
        {
            slider.SetValueWithoutNotify(value);

            if (slider.wholeNumbers)
            {
                inputField.SetTextWithoutNotify(value.ToString("F0"));
            }
            else
            {
                inputField.SetTextWithoutNotify(value.ToString("F2"));
            }
        }
    }
}
