using System.Collections;

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace Miris.Runtime
{
    public class GrabUIHandler : MonoBehaviour
    {
        [SerializeField]
        private XRGrabInteractable m_grabInteractable;

        [SerializeField]
        private GameObject m_grabHandleVisual;

        [SerializeField]
        private AudioSource m_audioSource;

        [SerializeField]
        private AudioClip m_grabBeginAudioClip;

        [SerializeField]
        private AudioClip m_grabEndAudioClip;

        private bool m_grabbing = false;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void OnEnable()
        {
            m_grabInteractable.hoverEntered.AddListener(OnHoverEntered);
            m_grabInteractable.hoverExited.AddListener(OnHoverExit);
            m_grabInteractable.selectEntered.AddListener(OnGrabBegin);
            m_grabInteractable.selectExited.AddListener(OnGrabEnd);
        }

        private void OnDisable()
        {
            m_grabInteractable.hoverEntered.RemoveListener(OnHoverEntered);
            m_grabInteractable.hoverExited.RemoveListener(OnHoverExit);
            m_grabInteractable.selectEntered.RemoveListener(OnGrabBegin);
            m_grabInteractable.selectExited.RemoveListener(OnGrabEnd);
        }

        private void LateUpdate()
        {
            // Lock the Z rotation while keeping the X and Y rotations
            Vector3 currentEulerAngles = transform.rotation.eulerAngles;
            transform.rotation = Quaternion.Euler(currentEulerAngles.x, currentEulerAngles.y, 0f);
        }

        private void OnHoverEntered(HoverEnterEventArgs eventArgs)
        {
            if (m_grabbing)
            {
                return;
            }

            HapticsUtility.SendHapticImpulse(0.2f, 0.025f, HapticsUtility.Controller.Both);
            m_grabHandleVisual.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
        }

        private void OnHoverExit(HoverExitEventArgs eventArgs)
        {
            if (m_grabbing)
            {
                return;
            }

            HapticsUtility.SendHapticImpulse(0.1f, 0.025f, HapticsUtility.Controller.Both);
            m_grabHandleVisual.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        }

        private void OnGrabBegin(SelectEnterEventArgs eventArgs)
        {
            m_grabbing = true;

            if (m_audioSource && m_grabBeginAudioClip)
            {
                m_audioSource.PlayOneShot(m_grabBeginAudioClip);
            }

            HapticsUtility.SendHapticImpulse(0.3f, 0.025f, HapticsUtility.Controller.Both);
            StartCoroutine(AnimateScale(m_grabHandleVisual, new Vector3(0.9f, 0.9f, 0.9f)));
        }

        private void OnGrabEnd(SelectExitEventArgs eventArgs)
        {
            m_grabbing = false;

            if (m_audioSource && m_grabEndAudioClip)
            {
                m_audioSource.PlayOneShot(m_grabEndAudioClip);
            }

            HapticsUtility.SendHapticImpulse(0.2f, 0.025f, HapticsUtility.Controller.Both);
            StartCoroutine(AnimateScale(m_grabHandleVisual, new Vector3(1.0f, 1.0f, 1.0f)));
        }

        private IEnumerator AnimateScale(GameObject targetGameObj, Vector3 targetScale, float duration = 0.1f)
        {
            float currentTime = 0;
            Vector3 originalScale = targetGameObj.transform.localScale;
            while (currentTime < duration)
            {
                targetGameObj.transform.localScale = Vector3.Lerp(originalScale, targetScale, currentTime / duration);
                currentTime += Time.deltaTime;
                yield return null;
            }

            targetGameObj.transform.localScale = targetScale;
        }
    }
}
