using System;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

namespace Miris.Runtime
{
    [RequireComponent(typeof(Camera))]
    public class ViewportCameraController : MonoBehaviour
    {
        public Vector3 m_targetPosition = new Vector3(0, 0, 0);

        [Header("Orbit")]
        public float m_orbitSpeed = 5f;

        private float m_orbitYaw = 0f;
        private float m_orbitPitch = 0f;

        [Header("Pan")]
        public float m_panSpeed = 0.3f;

        [Header("Zoom")]
        public float m_zoomSpeed = 2f;
        private float m_zoomDistance = 5f;      
        public float m_minZoomDistance = 0.1f; 

        private ViewportInputActions m_inputActions;

        [SerializeField]
        private XROrigin m_xrOrigin;

        private enum InteractionMode
        {
            None,
            Pan,
            Orbit,
            Zoom
        }

        private InteractionMode m_interactionMode = InteractionMode.None;

        private Vector2 m_panInput;
        private Vector2 m_orbitInput;
        private float m_zoomInput;

        [Header("Frame")]
        private float m_lastFrameDistance = 0;
        private bool m_frameDirectionFlipped = false;

        // --------------------------------------------------------------------
        // Public
        // --------------------------------------------------------------------

        public void ResetCamera()
        {
            if(m_xrOrigin != null)
            {
                m_xrOrigin.MoveCameraToWorldLocation(Vector3.zero);
                m_xrOrigin.RotateAroundCameraUsingOriginUp(0f);
            }
        }

        public void Frame(Bounds bounds)
        {
            m_targetPosition = bounds.center;
            m_zoomDistance = CalculateFrameDistance(bounds);
            if(!Mathf.Approximately(m_zoomDistance, m_lastFrameDistance))
            {
                m_lastFrameDistance = m_zoomDistance;
            } else {
                m_frameDirectionFlipped = !m_frameDirectionFlipped;
            }
            if(m_xrOrigin != null)
            {
                if(m_frameDirectionFlipped)
                {
                    m_xrOrigin.MoveCameraToWorldLocation(Vector3.zero);
                    m_xrOrigin.RotateAroundCameraUsingOriginUp(180f);
                }
                Quaternion rotation = transform.rotation;
                Vector3 dir = rotation * Vector3.back;
                Vector3 pos = m_targetPosition + dir * m_zoomDistance;
                pos = new Vector3(pos.x, m_targetPosition.y, pos.z);
                m_xrOrigin.MoveCameraToWorldLocation(pos);

            }
        }

        // --------------------------------------------------------------------
        // Unity overrides
        // --------------------------------------------------------------------

        protected void OnEnable()
        {
            m_inputActions = new();
            m_inputActions.Viewport.Pan.performed += OnPanPerformed;
            m_inputActions.Viewport.Pan.canceled += OnActionCanceled;
            m_inputActions.Viewport.Orbit.performed += OnOrbitPerformed;
            m_inputActions.Viewport.Orbit.canceled += OnActionCanceled;
            m_inputActions.Viewport.Zoom.performed += OnZoomPerformed;
            m_inputActions.Viewport.Zoom.canceled += OnActionCanceled;
            m_inputActions.Enable();
        }

        protected void OnDisable()
        {
            m_inputActions.Disable();
            m_inputActions.Viewport.Pan.performed -= OnPanPerformed;
            m_inputActions.Viewport.Pan.canceled -= OnActionCanceled;
            m_inputActions.Viewport.Orbit.performed -= OnOrbitPerformed;
            m_inputActions.Viewport.Orbit.canceled -= OnActionCanceled;
            m_inputActions.Viewport.Zoom.performed -= OnZoomPerformed;
            m_inputActions.Viewport.Zoom.canceled -= OnActionCanceled;
            m_inputActions = null;
        }

        protected void Start()
        {
            Vector3 angles = transform.eulerAngles;
            m_orbitYaw = angles.y;
            m_orbitPitch = angles.x;
            m_zoomDistance = Vector3.Distance(m_targetPosition, transform.position);
            UpdateCameraPosition();
        }

        protected void Update()
        {
            switch (m_interactionMode) 
            {
                case InteractionMode.Pan:
                    {
                        Vector3 right = transform.right;
                        Vector3 up = transform.up;
                        Vector3 pan = (-right * m_panInput.x - up * m_panInput.y) * m_panSpeed * Time.deltaTime;
                        m_targetPosition += pan * m_zoomDistance;
                        break;
                    }
                case InteractionMode.Orbit:
                    {
                        m_orbitYaw += m_orbitInput.x * m_orbitSpeed * Time.deltaTime;
                        m_orbitPitch -= m_orbitInput.y * m_orbitSpeed * Time.deltaTime;
                        m_orbitPitch = Mathf.Clamp(m_orbitPitch, -85f, 85f);
                        break;
                    }
                case InteractionMode.Zoom:
                    {
                        m_zoomDistance = Mathf.Max(m_zoomDistance + (-m_zoomInput * Mathf.Pow(m_zoomDistance, 0.25f) * m_zoomSpeed), m_minZoomDistance);
                        break;
                    }
            }

            UpdateCameraPosition();
        }
        
        // --------------------------------------------------------------------
        // Private
        // --------------------------------------------------------------------

        private void OnPanPerformed(InputAction.CallbackContext context)
        {
            m_panInput = context.ReadValue<Vector2>();
            m_interactionMode = InteractionMode.Pan;
        }

        private void OnOrbitPerformed(InputAction.CallbackContext context)
        {
            m_orbitInput = context.ReadValue<Vector2>();
            m_interactionMode = InteractionMode.Orbit;
        }

        private void OnZoomPerformed(InputAction.CallbackContext context)
        {
            Vector2 mouseDelta = context.ReadValue<Vector2>();
            float magnitude = Math.Max(Mathf.Abs(mouseDelta.x), Mathf.Abs(mouseDelta.y));
            float sign = Mathf.Sign(mouseDelta.x + mouseDelta.y);
            m_zoomInput = magnitude * sign;
            m_interactionMode = InteractionMode.Zoom;
        }

        private void OnActionCanceled(InputAction.CallbackContext context)
        {
            m_interactionMode = InteractionMode.None;
        }

        private void UpdateCameraPosition()
        {
            if(m_xrOrigin == null){
                Quaternion rotation = Quaternion.Euler(m_orbitPitch, m_orbitYaw, 0f);
                rotation = (!m_frameDirectionFlipped) ? rotation : Quaternion.Euler(0f, 180f, 0f) * rotation;
                Vector3 dir = rotation * Vector3.back;
                transform.position = m_targetPosition + dir * m_zoomDistance;
                transform.rotation = rotation;
            }
        }

        private float CalculateFrameDistance(Bounds bounds)
        {
            // We can consider the camera frustum as well but this is reasonable enough for now.
            Vector3 corner = bounds.size * 2;
            return Mathf.Max(corner.x, corner.y, corner.z);
        }
    }
}
