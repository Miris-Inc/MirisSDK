// Copyright © 2024 Miris.All rights reserved.

// Standard library
using System;
using System.Collections;

// Unity engine
using UnityEngine;

namespace Miris.Runtime
{

    // UserInterfaceManager manages the minimized/maximized state of UI elements
    public class UserInterfaceManager : MonoBehaviour
    {
        private enum StartBehavior : int
        {
            StartMinimized = 0,
            StartMaximized,
            TransitionToMaximized
        }

        [SerializeField]
        private StartBehavior m_startBehavior = StartBehavior.StartMaximized;

        private enum UIState : int
        {
            Minimized = 0,
            Maximized
        }

        private UIState m_state = UIState.Maximized;

        // The UI object to manage the minimized / maximized state of. 
        [SerializeField]
        private GameObject m_uiObject;

        // The XR origin object to spawn the UI relative to.
        [SerializeField]
        public GameObject m_xrOrigin;

        [SerializeField]
        private AudioSource m_audioSource;

        [SerializeField]
        private AudioClip m_minimizeAudioClip;

        [SerializeField]
        private AudioClip m_maximizeAudioClip;

        // the time in seconds to minimize / maximize the user interface
        private float m_minMaxTransitionSeconds = 0.3f;

        // Rotation attributes
        private Quaternion m_maximizedRotation;

        // position vector attributes
        private Vector3 m_minimizedPositionDelta = new Vector3(0, -0.05f, 0);
        private Vector3 m_maximizedPosition;
        private Vector3 m_currentPosition;
        private Vector3 m_goalPosition;

        // Opacity attributes
        private float m_currentOpacity;
        private float m_goalOpacity;

        private CanvasGroup m_canvas;

        public void Start()
        {
            // Extract current position & rotation as maximized / canonical states.
            m_maximizedPosition = m_uiObject.transform.localPosition;
            m_maximizedRotation = m_uiObject.transform.localRotation;

            m_canvas = GetComponent<CanvasGroup>();

            switch (m_startBehavior)
            {
                case StartBehavior.StartMaximized:
                    // No-op
                    break;

                case StartBehavior.StartMinimized:
                    m_state = UIState.Minimized;
                    SetActiveState(false);
                    break;

                case StartBehavior.TransitionToMaximized:
                    m_state = UIState.Minimized;
                    SetActiveState(false);
                    StartCoroutine(DelayedAction(0.5f, Maximize));
                    break;
            }
        }

        private IEnumerator DelayedAction(float delaySeconds, Action action)
        {
             yield return new WaitForSeconds(delaySeconds);
             action?.Invoke();
        }

        public void ToggleInterfaceSize()
        {
            if(m_uiObject==null){
                return;
            }
            if (m_state == UIState.Minimized)
            {
                Maximize();
            }
            else
            {
                Minimize();
            }
        }

        public bool IsMaximized()
        {
            return m_state == UIState.Maximized;
        }

        public void ForceMinimize(){

            StopAllCoroutines();

            m_currentOpacity = m_canvas.alpha;
            m_currentPosition = m_uiObject.transform.localPosition;
            m_goalPosition = m_currentPosition + m_minimizedPositionDelta;
            m_goalOpacity = 0.0f;

            if (m_audioSource && m_minimizeAudioClip)
            {
                m_audioSource.PlayOneShot(m_minimizeAudioClip);
            }

            StartCoroutine(TransitionUI(UIState.Minimized, Mathf.Max(m_maximizeAudioClip.length, m_minMaxTransitionSeconds)));
        }

        public void ForceMaximize(){

            StopAllCoroutines();

            // Activate children 
            SetActiveState(true);

            // Mute x and z rotation from XR Origin transform before applying it to position.  This is so that the
            // menu always spawns at the same elevation as its rest state, and also does not "roll".
            m_xrOrigin.transform.GetPositionAndRotation(out Vector3 xrOriginPosition, out Quaternion xrOriginRotation);
            Vector3 xrOriginEuler = xrOriginRotation.eulerAngles;
            Quaternion xrOriginYRotation = Quaternion.Euler(0, xrOriginEuler.y, 0);

            Matrix4x4 xrOriginTransform = Matrix4x4.TRS(xrOriginPosition, xrOriginYRotation, Vector3.one);
            m_currentPosition = xrOriginTransform.MultiplyPoint(m_maximizedPosition + m_minimizedPositionDelta);
            m_goalPosition = xrOriginTransform.MultiplyPoint(m_maximizedPosition);

            m_currentOpacity = m_canvas.alpha;
            m_goalOpacity = 1.0f;

            // Orient UI before transitioning.
            Matrix4x4 rotationMatrix = Matrix4x4.Rotate(m_maximizedRotation);
            Matrix4x4 transformedMatrix = xrOriginTransform * rotationMatrix;
            m_uiObject.transform.localEulerAngles = transformedMatrix.rotation.eulerAngles;

            if (m_audioSource && m_maximizeAudioClip)
            {
                m_audioSource.PlayOneShot(m_maximizeAudioClip);
            }

            StartCoroutine(TransitionUI(UIState.Maximized));
        }

        public void Minimize()
        {
            if (m_state == UIState.Minimized)
            {
                return;
            }

           ForceMinimize();
        }

        public void Maximize()
        {
            if (m_state == UIState.Maximized)
            {
                return;
            }

           ForceMaximize();
        }

        private IEnumerator TransitionUI(UIState goalState, float deactivationSeconds = 0.0f)
        {
            float currentTime = 0;
            while (currentTime <= m_minMaxTransitionSeconds)
            {
                // While transitioning, linearly interpolate position & opacity
                float timeRatio = currentTime / m_minMaxTransitionSeconds;
                float easedTimeRatio = timeRatio * timeRatio * (3f - 2f * timeRatio);
                m_uiObject.transform.localPosition = Vector3.Lerp(m_currentPosition, m_goalPosition, easedTimeRatio);
                m_canvas.alpha = Mathf.Lerp(m_currentOpacity, m_goalOpacity, easedTimeRatio);

                currentTime += Time.deltaTime;
                yield return null;
            }

            // Finalize transition
            m_uiObject.transform.localPosition = m_goalPosition;
            m_canvas.alpha = m_goalOpacity;
            m_state = goalState;

            // Disable children once we finish minimizing.
            if (m_state == UIState.Minimized)
            {
                while (currentTime <= deactivationSeconds)
                {
                    currentTime += Time.deltaTime;
                    yield return null;
                }

                SetActiveState(false);
            }
        }

        private void SetActiveState(bool active)
        {
            m_uiObject.SetActive(active);
        }
    }
}
