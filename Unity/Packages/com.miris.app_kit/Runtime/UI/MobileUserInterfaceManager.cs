// Copyright © 2025 Miris.All rights reserved.

using UnityEngine;
using UnityEngine.UI;

using System;
using System.Collections;
using System.Threading.Tasks;

namespace Miris.Runtime
{
    public class MobileUserInterfaceManager : MonoBehaviour
    {
        [Header("Inspector Object References")]
        [SerializeField]
        private ButtonSetManager m_buttonSetManager;
        [SerializeField]
        private TimelineTouchUIController m_timelinecontroller;
        [SerializeField]
        private GameObject m_focusObjectButton;
        [SerializeField]
        private CanvasGroup m_canvasGroup;

        [Header("Fade User Interface Variables")]
        private float m_timeToFadeInterface = 1.5f;
        private float m_interfaceFadeTime = 0.7f;
        private Coroutine m_fadeCoroutine;

        private int PLAY_SCREEN_INDEX = 1;
        private string m_assetInfoID = string.Empty;

        // --------------------------------------------------------------------
        // Unity Object Lifetime Functions
        // --------------------------------------------------------------------
        void Start()
        {
            m_buttonSetManager.SetUIManager(this);
        }

        void Update()
        {
            CheckAndCancelFade();
            m_focusObjectButton.SetActive(m_buttonSetManager.GetActiveButtonIndex() == PLAY_SCREEN_INDEX);
        }

        // --------------------------------------------------------------------
        // Interface Fade Functions
        // --------------------------------------------------------------------
        private IEnumerator FadeInterface(float timeToFadeInterface, Action onComplete = null)
        {
            float elapsedTime = 0.0f;
            while(elapsedTime <= timeToFadeInterface){
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            m_canvasGroup.interactable = false;
            m_canvasGroup.blocksRaycasts = false;

            elapsedTime = 0.0f;
            while(elapsedTime <= m_interfaceFadeTime){
                m_canvasGroup.alpha = Mathf.Lerp(1.0f, 0, elapsedTime / m_interfaceFadeTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            m_canvasGroup.alpha = 0.0f;
            m_fadeCoroutine = null;
            onComplete?.Invoke();
        }

        private void CheckAndCancelFade()
        {
            if(IsPlayScreen() || !HasActiveAsset())
            {
                return;
            }
            if(m_fadeCoroutine != null)
            {
                StopCoroutine(m_fadeCoroutine);
                RestoreCanvas();
            }
        }

        private void StartInterfaceFade()
        {
            if(!HasActiveAsset())
            {
                return;
            }
            if(m_fadeCoroutine != null)
            {
                StopCoroutine(m_fadeCoroutine);
            }
            m_fadeCoroutine = StartCoroutine(FadeInterface(m_timeToFadeInterface));
        }

        public void ClearUserInterfaceState()
        {
            m_buttonSetManager.SetDefaultState();
            m_timelinecontroller.ResumePlayBack();
        }

        private void RestoreCanvas()
        {
            m_canvasGroup.alpha = 1.0f;
            m_canvasGroup.interactable = true;
            m_canvasGroup.blocksRaycasts = true;
        }

        private bool IsPlayScreen()
        {
            return m_buttonSetManager.GetActiveButtonIndex() == PLAY_SCREEN_INDEX;
        }

        private bool HasActiveAsset()
        {
            return (!string.IsNullOrEmpty(m_assetInfoID));
        }

        public void SetCurrentAssetInfo(SceneAssetInfo assetInfo)
        {
            m_assetInfoID = assetInfo.m_assetName;
        }

        public void SwitchPanelRefresh()
        {
            if(IsPlayScreen())
            {
                StartInterfaceFade();
            }
        }

        // --------------------------------------------------------------------
        // Touch event functions
        // --------------------------------------------------------------------
        public void TouchStart()
        {
            if(!HasActiveAsset() || !IsPlayScreen()){
                return;
            }
            if(m_fadeCoroutine != null)
            {
                StopCoroutine(m_fadeCoroutine);
            }
            RestoreCanvas();
        }

        public void TouchEnd()
        {
            if(!HasActiveAsset() || !IsPlayScreen())
            {
                return;
            }
            StartInterfaceFade();
        }
    }
}
