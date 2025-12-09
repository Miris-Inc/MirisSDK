// Copyright © 2024 Miris.All rights reserved.

using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.UI;

using UnityEngine.XR.Interaction.Toolkit;

namespace Miris.Runtime
{

    public class MirisPlayerController : MonoBehaviour, IPreferences
    {
        [SerializeField]
        private InputActionReference m_leftActivateAction;

        [SerializeField]
        private InputActionReference m_rightActivateAction;

        [SerializeField]
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor m_leftRayInteractor;

        [SerializeField]
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor m_rightRayInteractor;

        [SerializeField]
        private UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider m_teleportationProvider;

        [SerializeField]
        private string m_scenePath;

        [SerializeField]
        public SceneSelector m_sceneSelector;

        [SerializeField]
        public GameObject m_xrUserInterface;

        [SerializeField]
        public GameObject m_xrDeveloperInterface;

        [SerializeField]
        public GameObject m_xrTimelineInterface;

        [SerializeField]
        private MirisStreamController m_streamController;

        [SerializeField]
        private MirisStream m_stream;

        [SerializeField]
        private MirisPlayerSceneManager m_sceneManager;
        private PlayerInputActions m_playerInputActions;

        // --------------------------------------------------------------------
        // Reset Scene related functions
        // --------------------------------------------------------------------

        [SerializeField]
        private GameObject m_resetInterface;
        private float? m_clearSceneTime = null;
        // time required, in seconds, to fill the visual reset slider and clear the scene contents
        private float m_timeToClear = 1.0f;

        // --------------------------------------------------------------------
        // Unity overrides
        // --------------------------------------------------------------------

        public void Awake()
        {
            SaveDefaultPreferences();

            if (Debug.isDebugBuild || SystemInfo.deviceType == DeviceType.Desktop)
            {
                gameObject.AddComponent<FlyCamera>();
            }

        }

        private bool IsDeveloperMode()
        {
            return !XRSettings.isDeviceActive || Debug.isDebugBuild;
        }

        private async void Start()
        {
            m_streamController.GetAssetManager().ServerEnvironmentChanged += OnEnvironmentChanged;
            m_streamController.GetAssetManager().TagsChanged += OnTagsChanged;

            await LoadPreferences();

            if (!XRUtils.IsXR())
            {
                m_rightRayInteractor.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>().lineLength = 0.0f;
            }

            m_xrDeveloperInterface.SetActive(IsDeveloperMode());

            // Bind input actions
            if (m_playerInputActions == null)
            {
                m_playerInputActions = new();
            }

            if (IsDeveloperMode())
            {
                m_playerInputActions.Player.MinMaxDeveloperInterface.performed += OnMinMaxDeveloperInterfacePerformed;
            }
            m_playerInputActions.Player.MinMaxUserInterface.performed += OnMinMaxUserInterfacePerformed;
            m_playerInputActions.Player.MinMaxTimelineInterface.performed += OnMinMaxTimelineInterfacePerformed;
            m_playerInputActions.Player.AttemptClearScene.performed += OnClearScenePerformed;
            m_playerInputActions.Player.AttemptClearScene.canceled += OnClearSceneCancelled;
            m_playerInputActions.Enable();

        }

        private void OnDisable()
        {
            m_playerInputActions?.Disable();

            if (m_streamController != null && m_streamController.isActiveAndEnabled)
            {
                m_streamController.GetAssetManager().TagsChanged -= OnTagsChanged;
                m_streamController.GetAssetManager().ServerEnvironmentChanged -= OnEnvironmentChanged;
            }
        }

        void Update() 
        {
            CheckClearSceneProgress();
        }

        // --------------------------------------------------------------------
        // IPreferences interface
        // --------------------------------------------------------------------

        public void SavePreferences()
        {
            _ = Preferences.Save();
        }

        public async Task LoadPreferences()
        {
            await Preferences.completedLoading;
        }

        public void ClearPreferences()
        {
            Preferences.instance.playerController = new();
        }

        public void SaveDefaultPreferences()
        {
        }

        public void RestoreDefaultPreferences()
        {
            ClearPreferences();
        }

        // --------------------------------------------------------------------
        // UI handling
        // -------------------------------------------------------------------- 

        private async void OnEnvironmentChanged(string environment)
        {
#if MIRIS_INTERNAL
            // Change viewer key if possible
            {
                var config = ClientConfig.Load();
                if (!config.asset_viewer_keys.ContainsKey(environment))
                {
                    Debug.LogError($"No asset viewer key configured for ENV '{environment}'");
                }
                else
                {
                    string viewerKey = config.asset_viewer_keys[environment];
                    if (string.IsNullOrWhiteSpace(viewerKey))
                    {
                        Debug.LogError($"Asset viewer key for ENV '{environment}' is empty/whitespace");
                    }
                    else
                    {
                        m_streamController.GetClient().SetAssetViewerKey(viewerKey);
                    }
                }
            }
#endif

            await m_sceneSelector.AssetSourceChanged();
        }
        
        private async void OnTagsChanged()
        {
            await m_sceneSelector.AssetSourceChanged();
        }

        public float GetClearSceneProgress()
        {
            if (m_clearSceneTime.HasValue)
            {
                float percentage = (Time.time - m_clearSceneTime.Value) / m_timeToClear;
                percentage = (percentage >= 1.0) ? 1.0f : percentage;

                Slider[] sliders = m_resetInterface.GetComponentsInChildren<Slider>();
                sliders[0].value = percentage;


                return percentage;
            }
            return 0.0f;
        }

        private void CancelClearSceneTime()
        {
            m_clearSceneTime = null;
            m_resetInterface.GetComponent<UserInterfaceManager>().ForceMinimize();
        }

        private void CheckClearSceneProgress()
        {
            if (GetClearSceneProgress() >= 1.0f)
            {
                CancelClearSceneTime();
                m_sceneManager.ClearScene();
            }
        }

        private void OnClearScenePerformed(InputAction.CallbackContext context)
        {
            if (!m_stream.IsLoaded())
            {
                return;
            }
            m_clearSceneTime = Time.time;
            m_resetInterface.GetComponent<UserInterfaceManager>().ForceMaximize();
        }

        private void OnClearSceneCancelled(InputAction.CallbackContext context)
        {
            CancelClearSceneTime();
        }

        private void OnMinMaxUserInterfacePerformed(InputAction.CallbackContext context)
        {
            m_xrUserInterface.GetComponent<UserInterfaceManager>().ToggleInterfaceSize();
        }

        private void OnMinMaxDeveloperInterfacePerformed(InputAction.CallbackContext context)
        {
            m_xrDeveloperInterface.GetComponent<UserInterfaceManager>().ToggleInterfaceSize();
        }

        private void OnMinMaxTimelineInterfacePerformed(InputAction.CallbackContext context)
        {
            m_xrTimelineInterface.GetComponent<UserInterfaceManager>().ToggleInterfaceSize();
        }
    }
}
