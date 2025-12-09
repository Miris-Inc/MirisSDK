using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using System.Collections;

namespace Miris.Runtime
{
    public class MirisViewerController : MonoBehaviour
    {
        [SerializeField]
        private MirisStream m_stream;

        [SerializeField]
        public MirisStreamController m_streamController;

        [SerializeField]
        private ViewportCameraController m_cameraController;
        

        private ViewportInputActions m_inputActions;

        [SerializeField]
        private UserInterfaceManager m_toggleMenuButton;

        [SerializeField]
        private UserInterfaceManager m_menu;

        [SerializeField]
        private SceneSelector m_sceneSelector;
        
        private Coroutine m_frameCoroutine;

        private bool m_framed = false;

        protected void OnEnable()
        {
            m_inputActions = new();
            m_inputActions.Viewport.Frame.performed += OnFramePerformed;
            #if UNITY_EDITOR
            m_inputActions.Viewport.ToggleCulling.performed += OnToggleCullingPerformed;
            #endif
            m_inputActions.Enable();

            m_toggleMenuButton.GetComponentInChildren<Button>().onClick.AddListener(OnToggleMenuButtonClick);
        }

        protected void OnDisable()
        {
            m_inputActions.Disable();
            m_inputActions.Viewport.Frame.performed -= OnFramePerformed;
            #if UNITY_EDITOR
            m_inputActions.Viewport.ToggleCulling.performed -= OnToggleCullingPerformed;
            #endif
            m_inputActions = null;
        }

        private void OnToggleMenuButtonClick()
        {
            m_menu.ToggleInterfaceSize();
        }

        void Start()
        {
            m_streamController.m_onMetadataLoadedActions.Add(() => FrameObject());
            m_streamController.GetAssetManager().ServerEnvironmentChanged += OnEnvironmentChanged;
            m_streamController.GetAssetManager().TagsChanged += OnTagsChanged;
            m_stream.m_onUnloadedActions.Add(() => ResetFrame());

            ResetFrame();
        }

        private void ResetFrame()
        {
            m_framed = false;
            if(m_frameCoroutine == null){
                m_frameCoroutine = StartCoroutine(CheckAndFrameView());
            }
        }

        private IEnumerator CheckAndFrameView()
        {
            while(!m_framed)
            {
                m_streamController.m_loadedMetadata = false;
                m_streamController.GetAssetMetadata();
                yield return null; // wait a single frame
            }
            m_frameCoroutine = null;
        }

        void Update()
        {
        }

        private bool HasInitializedBounds(Bounds bounds){
            if(bounds.size == Vector3.zero || float.IsInfinity(Mathf.Abs(bounds.size.x))){
                return false;
            }
            return true;
        }

        private void FrameObject()
        {
            Bounds bounds = m_stream.GetWorldBounds();
            if(!HasInitializedBounds(bounds)){
                return;
            }
            m_cameraController.Frame(bounds);
            m_framed = true;
        }

        private void OnFramePerformed(InputAction.CallbackContext context)
        {
            FrameObject();
        }

        private void OnToggleCullingPerformed(InputAction.CallbackContext context)
        {
            m_streamController.ToggleRenderComponentCulling();
        }

        private async void OnEnvironmentChanged(string environment)
        {
            await m_sceneSelector.AssetSourceChanged();
        }

        private async void OnTagsChanged()
        {
            await m_sceneSelector.AssetSourceChanged();
        }
    }
}