// Copyright © 2025 Miris, Inc. All rights reserved.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using System.Collections;

namespace Miris.Runtime
{
    public class MirisDesktopController : MirisController
    {
        [SerializeField]
        private ViewportCameraController m_cameraController;
        
        [SerializeField]
        private UserInterfaceManager m_toggleMenuButton;

        [SerializeField]
        private UserInterfaceManager m_menu;

        private Coroutine m_frameCoroutine;

        private bool m_framed = false;

        protected void OnEnable()
        {
            base.OnEnable();
            m_inputActions.Desktop.Frame.performed += OnFramePerformed;
            #if UNITY_EDITOR
            m_inputActions.Desktop.ToggleCulling.performed += OnToggleCullingPerformed;
            #endif

            m_toggleMenuButton.GetComponentInChildren<Button>().onClick.AddListener(OnToggleMenuButtonClick);
        }

        protected void OnDisable()
        {
            base.OnEnable();
            m_inputActions.Desktop.Frame.performed -= OnFramePerformed;
            #if UNITY_EDITOR
            m_inputActions.Desktop.ToggleCulling.performed -= OnToggleCullingPerformed;
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
    }
}