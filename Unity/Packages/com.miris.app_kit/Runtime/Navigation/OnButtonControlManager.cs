// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;
using UnityEngine.UI;

using System.Collections;

namespace Miris.Runtime
{
    public class OnButtonControlManager : MonoBehaviour
    {
        public GameObject m_buttonObject;
        [SerializeField]
        private GameObject m_buttonDisplayLeftAnchor;
        [SerializeField]
        private GameObject m_buttonDisplayRightAnchor;

        private LineRenderer m_lineRenderer;
        public bool leftAnchored = true;
        private Camera m_camera;

        private Coroutine m_fadeCoroutine;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            m_lineRenderer = GetComponent<LineRenderer>();
            m_camera = Camera.main;
        }

        private void StopCurrentRoutine(){
            if (m_fadeCoroutine != null)
            {
                StopCoroutine(m_fadeCoroutine);
                m_fadeCoroutine = null;
            }
        }

        public void HideDisplay(){
            StopCurrentRoutine();
            m_fadeCoroutine = StartCoroutine(FadeOutController());
        }

        public void ShowDisplay(){
            StopCurrentRoutine();
            m_fadeCoroutine = StartCoroutine(FadeInController());
        }

        private float GetCanvasGroupAlpha(){
            CanvasGroup[] groups = GetComponentsInChildren<CanvasGroup>();
            foreach (CanvasGroup canvasGroup in groups)
            {
                return canvasGroup.alpha;
            }
            return 0.0f;
        }

        private void SetLineRendererEnabled(bool enabled){
            if(enabled == m_lineRenderer.enabled){
                return;
            }
            m_lineRenderer.enabled = enabled;
        }

        private void AdjustCanvasGroup(float alpha){
            CanvasGroup[] groups = GetComponentsInChildren<CanvasGroup>();
            foreach (CanvasGroup canvasGroup in groups)
            {
                canvasGroup.alpha = alpha;
            }
           
        }

        private IEnumerator FadeInController()
        {
            // provides a buffer so that it has to be in your view for one second to start fading in
            float pre_fadeInTime = 0.5f;
            float elapsedTime = 0.0f;
            while (elapsedTime < pre_fadeInTime)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            float alpha = GetCanvasGroupAlpha();
            elapsedTime = 0.0f;
            float fadeInTime = 0.8f;
            while (elapsedTime < fadeInTime)
            {
                alpha = Mathf.Lerp(alpha, 1, elapsedTime / fadeInTime);
                elapsedTime += Time.deltaTime;
                AdjustCanvasGroup(alpha);
                if(alpha >= 0.8){
                    SetLineRendererEnabled(true);
                }
                yield return null;
            }
            // adjust transparency of canvas group for controller
            AdjustCanvasGroup(1.0f);

        }

        private IEnumerator FadeOutController()
        {   
            // provides a buffer so that it has to be out of your view for one second to start fading away
            float pre_fadeOutTime = 1.0f;
            float elapsedTime = 0.0f;
            while(elapsedTime < pre_fadeOutTime){
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            float alpha = GetCanvasGroupAlpha();
            elapsedTime = 0.0f;
            float fadeOutTime = 0.6f;
            while (elapsedTime < fadeOutTime)
            {
                alpha = Mathf.Lerp(alpha, 0, elapsedTime / fadeOutTime);
                elapsedTime += Time.deltaTime;
                AdjustCanvasGroup(alpha);
                if (alpha < 0.8)
                {
                    SetLineRendererEnabled(false);
                }
                yield return null;
            }
            // adjust transparency of canvas group for controller
            AdjustCanvasGroup(0.0f);
        }

        // Update is called once per frame
        void Update()
        {
            if(m_buttonObject == null || m_buttonDisplayLeftAnchor == null || m_buttonDisplayRightAnchor == null){
                return;
            }

            m_lineRenderer.SetPosition(0, m_buttonObject.transform.position);
            if(leftAnchored){
                m_lineRenderer.SetPosition(1, m_buttonDisplayLeftAnchor.transform.position);
            } else {
                m_lineRenderer.SetPosition(1, m_buttonDisplayRightAnchor.transform.position);
            }
        }
    }
}
