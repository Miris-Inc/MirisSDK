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
    public class DeveloperLODController : DeveloperBaseController
    {
        // extraneous references
        [SerializeField]
        public MirisStreamController m_streamController;

        [SerializeField]
        public MirisPlayerPreferences m_mirisPlayerPrefs;

        // UI Dropdowns
        [SerializeField]
        private TMP_Dropdown m_lodSelectionModeDropdown;

        [SerializeField]
        private Slider m_lowestLodSlider;

        [SerializeField]
        private Slider m_highestLodSlider;

        [SerializeField]
        private Slider m_lodMaxDistanceSlider;

        [SerializeField]
        private Slider m_fixedLodIndexSlider;
        [SerializeField]
        private Slider m_splatCountBudgetSlider;
        // UI Input fields
        [SerializeField]
        private TMP_InputField m_splatCountInputField;

        private TMP_InputField m_lowestLodInputField;
        private TMP_InputField m_highestLodInputField;
        private TMP_InputField m_lodMaxDistanceInputField;
        private TMP_InputField m_fixedLodIndexInputField;
        private TMP_InputField m_splatCountBudgetInputField;

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

        // Initialization of UI elements by populating dropdowns based on enums, etc
        public override void InitializeUI()
        {
            // initialize dropdowns
            UiUtils.InitializeEnumDropdown(m_lodSelectionModeDropdown, typeof(LodSelectionMode), OnLodSelectionModeDropdownChanged);

            // initialize sliders 
            InitializeSlider(
                m_lowestLodSlider,
                ref m_lowestLodInputField,
                typeof(LodRefinementParameters),
                "m_lowestLodLimit",
                OnLowestLodSliderValueChanged,
                OnLowestLodInputFieldValueChanged
            );
            InitializeSlider(
                m_highestLodSlider,
                ref m_highestLodInputField,
                typeof(LodRefinementParameters),
                "m_highestLodLimit",
                OnHighestLodSliderValueChanged,
                OnHighestLodInputFieldValueChanged
            );
            InitializeSlider(
                m_lodMaxDistanceSlider,
                ref m_lodMaxDistanceInputField,
                typeof(LodRefinementParameters),
                "m_lodMaxDistance",
                OnLodMaxDistanceSliderValueChanged,
                OnLodMaxDistanceInputFieldValueChanged
            );

            InitializeSlider(
              m_fixedLodIndexSlider,
              ref m_fixedLodIndexInputField,
              typeof(LodRefinementParameters),
              "m_fixedLodIndex",
              OnFixedLodIndexSliderValueChanged,
              OnFixedLodIndexInputFieldValueChanged
          );
            InitializeSlider(
              m_splatCountBudgetSlider,
              ref m_splatCountBudgetInputField,
              typeof(LodRefinementParameters),
              "m_splatCountBudget",
              OnSplatCountBudgetSliderValueChanged,
              OnSplatCountBudgetInputFieldValueChanged
          );
        }

        // --------------------------------------------------------------------
        // UI event handling
        // --------------------------------------------------------------------

        private void OnLodSelectionModeDropdownChanged(int selectedIndex)
        {
            m_streamController.m_lodRefinementParameters.m_lodSelectionMode = (LodSelectionMode)selectedIndex;
            m_mirisPlayerPrefs.SavePreferences();
        }

        private void OnLowestLodSliderValueChanged(float value)
        {
            m_streamController.m_lodRefinementParameters.m_lowestLodLimit = value;
            m_mirisPlayerPrefs.SavePreferences();
        }

        private void OnLowestLodInputFieldValueChanged(string value)
        {
            m_streamController.m_lodRefinementParameters.m_lowestLodLimit = float.Parse(value);
        }

        private void OnHighestLodSliderValueChanged(float value)
        {
            m_streamController.m_lodRefinementParameters.m_highestLodLimit = value;
            m_mirisPlayerPrefs.SavePreferences();
        }

        private void OnHighestLodInputFieldValueChanged(string value)
        {
            m_streamController.m_lodRefinementParameters.m_highestLodLimit = float.Parse(value);
        }

        private void OnLodMaxDistanceSliderValueChanged(float value)
        {
            m_streamController.m_lodRefinementParameters.m_lodMaxDistance = value;
            m_mirisPlayerPrefs.SavePreferences();
        }

        private void OnLodMaxDistanceInputFieldValueChanged(string value)
        {
            m_streamController.m_lodRefinementParameters.m_lodMaxDistance = float.Parse(value);
        }

        private void OnFixedLodIndexSliderValueChanged(float value)
        {
            m_streamController.m_lodRefinementParameters.m_fixedLodIndex = (int)value;
            m_mirisPlayerPrefs.SavePreferences();
        }

        private void OnFixedLodIndexInputFieldValueChanged(string value)
        {
            m_streamController.m_lodRefinementParameters.m_fixedLodIndex = int.Parse(value);
        }


        private void OnSplatCountBudgetSliderValueChanged(float value)
        {
            m_streamController.m_lodRefinementParameters.m_splatCountBudget = (int)value;
            m_mirisPlayerPrefs.SavePreferences();
        }

        private void OnSplatCountBudgetInputFieldValueChanged(string value)
        {
            m_streamController.m_lodRefinementParameters.m_splatCountBudget = int.Parse(value);
        }


        // --------------------------------------------------------------------
        // UI Teardown
        // --------------------------------------------------------------------

        public override void TeardownUI()
        {
            // Tear down Toggles

            // Tear down dropdowns
            m_lodSelectionModeDropdown.onValueChanged.RemoveListener(OnLodSelectionModeDropdownChanged);

            // Tear down sliders
            m_lowestLodSlider.onValueChanged.RemoveListener(OnLowestLodSliderValueChanged);
            m_lowestLodInputField.onValueChanged.RemoveListener(OnLowestLodInputFieldValueChanged);
            m_highestLodSlider.onValueChanged.RemoveListener(OnHighestLodSliderValueChanged);
            m_highestLodInputField.onValueChanged.RemoveListener(OnHighestLodInputFieldValueChanged);
            m_lodMaxDistanceSlider.onValueChanged.RemoveListener(OnLodMaxDistanceSliderValueChanged);
            m_lodMaxDistanceInputField.onValueChanged.RemoveListener(OnLodMaxDistanceInputFieldValueChanged);
        }

        // --------------------------------------------------------------------
        // Synchronization of model data to UI
        // --------------------------------------------------------------------

        public override void SyncUI()
        {
            SyncLodSelectionModeDropdown();

            SyncLowestLodSlider();
            SyncHighestLodSlider();
            SyncLodMaxDistanceSlider();
            SyncFixedLodIndexSlider();
            SyncSplatCountBudgetSlider();
            SyncSplatCountInputField();
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

        private void SyncLodSelectionModeDropdown()
        {
            m_lodSelectionModeDropdown.SetValueWithoutNotify((int)m_streamController.m_lodRefinementParameters.m_lodSelectionMode);
        }

        private void SyncLowestLodSlider()
        {
            SyncSliderValue(m_lowestLodSlider, m_lowestLodInputField, m_streamController.m_lodRefinementParameters.m_lowestLodLimit);
        }

        private void SyncHighestLodSlider()
        {
            SyncSliderValue(m_highestLodSlider, m_highestLodInputField, m_streamController.m_lodRefinementParameters.m_highestLodLimit);
        }

        private void SyncLodMaxDistanceSlider()
        {
            SyncSliderValue(m_lodMaxDistanceSlider, m_lodMaxDistanceInputField, m_streamController.m_lodRefinementParameters.m_lodMaxDistance);
        }

        private void SyncFixedLodIndexSlider()
        {
            SyncSliderValue(m_fixedLodIndexSlider, m_fixedLodIndexInputField, m_streamController.m_lodRefinementParameters.m_fixedLodIndex);
        }
        
        private void SyncSplatCountBudgetSlider()
        {
            SyncSliderValue(m_splatCountBudgetSlider, m_splatCountBudgetInputField, m_streamController.m_lodRefinementParameters.m_splatCountBudget);
        }

        private void SyncSplatCountInputField()
        {
            int splatCount = 0;
            foreach (GaussianSplatRenderComponent rendererComponent in m_streamController.GetRenderComponents())
            {
                splatCount += rendererComponent.GetSplatCount();
            }

            m_splatCountInputField.SetTextWithoutNotify($"{splatCount}");
        }
    }
}
