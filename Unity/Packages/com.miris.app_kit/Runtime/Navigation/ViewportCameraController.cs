// Copyright © 2025 Miris, Inc. All rights reserved.

using System;

using UnityEngine;
using UnityEngine.InputSystem;

namespace Miris.Runtime
{
    [RequireComponent(typeof(Camera))]
    public class ViewportCameraController : MonoBehaviour
    {
        public Vector3 m_targetPosition = new Vector3(0, 0, 0);

        [Header("Orbit")]
        public float m_orbitSpeed = 200f;
        private float m_orbitYaw = 0f;
        private float m_orbitPitch = 0f;

        [Header("Pan")]
        public float m_panSpeed = 2f;

        [Header("Zoom")]
        public float m_zoomSpeed = 2f;
        private float m_zoomDistance = 5f;
        public float m_minZoomDistance = 0.1f;

        [Header("Move")]
        public float m_moveSpeed = 10.0f;

        [Header("Look Around")]
        public float m_lookAroundSpeed = 200.0f;

        private PlayerInputActions m_inputActions;

        [Flags]
        private enum InteractionMode
        {
            None = 0,
            Pan = 1 << 0,
            Orbit = 1 << 1,
            Zoom = 1 << 2,
            Move = 1 << 3,
            LookAround = 1 << 4
        }

        private InteractionMode m_interactionMode = InteractionMode.None;

        private Vector2 m_panInput;
        private Vector2 m_orbitInput;
        private float m_zoomInput;
        private Vector2 m_moveInput;
        private Vector2 m_lookAroundInput;

        // DPI Value used to compute final sensitivity of the various inputs.
        const float c_referenceDpi = 96;
        private float m_mouseInputSensitivity = 1.0f;

        // --------------------------------------------------------------------
        // Public
        // --------------------------------------------------------------------

        public void Frame(Bounds bounds)
        {
            m_targetPosition = bounds.center;
            m_zoomDistance = CalculateFrameDistance(bounds);
        }

        // --------------------------------------------------------------------
        // Unity overrides
        // --------------------------------------------------------------------

        protected void OnEnable()
        {
            m_inputActions = new();

            m_inputActions.Desktop.Pan.performed += OnPanPerformed;
            m_inputActions.Desktop.Pan.canceled += OnPanCancelled;

            m_inputActions.Desktop.Orbit.performed += OnOrbitPerformed;
            m_inputActions.Desktop.Orbit.canceled += OnOrbitCancelled;

            m_inputActions.Desktop.Zoom.performed += OnZoomPerformed;
            m_inputActions.Desktop.Zoom.canceled += OnZoomCancelled;

            m_inputActions.Desktop.Move.performed += OnMovePerformed;
            m_inputActions.Desktop.Move.canceled += OnMoveCancelled;

            m_inputActions.Desktop.LookAround.performed += OnLookAroundPerformed;
            m_inputActions.Desktop.LookAround.canceled += OnLookAroundCancelled;

            m_inputActions.Enable();
        }

        protected void OnDisable()
        {
            m_inputActions.Disable();

            m_inputActions.Desktop.Pan.performed -= OnPanPerformed;
            m_inputActions.Desktop.Pan.canceled -= OnPanCancelled;

            m_inputActions.Desktop.Orbit.performed -= OnOrbitPerformed;
            m_inputActions.Desktop.Orbit.canceled -= OnOrbitCancelled;

            m_inputActions.Desktop.Zoom.performed -= OnZoomPerformed;
            m_inputActions.Desktop.Zoom.canceled -= OnZoomCancelled;

            m_inputActions.Desktop.Move.performed -= OnMovePerformed;
            m_inputActions.Desktop.Move.canceled -= OnMoveCancelled;

            m_inputActions.Desktop.LookAround.performed -= OnLookAroundPerformed;
            m_inputActions.Desktop.LookAround.canceled -= OnLookAroundCancelled;

            m_inputActions = null;
        }

        protected void Start()
        {
            CalculateMouseInputSensitivity(); 
            m_orbitYaw = transform.eulerAngles.y;
            m_orbitPitch = transform.eulerAngles.x;
            m_zoomDistance = Vector3.Distance(m_targetPosition, transform.position);
            UpdateCameraPosition();
        }

        protected void Update()
        {
            ProcessInputs();
            UpdateCameraPosition();
        }
        
        // --------------------------------------------------------------------
        // Private
        // --------------------------------------------------------------------

        private void OnPanPerformed(InputAction.CallbackContext context)
        {
            m_panInput = context.ReadValue<Vector2>() * m_mouseInputSensitivity;
            m_interactionMode |= InteractionMode.Pan;
        }

        private void OnOrbitPerformed(InputAction.CallbackContext context)
        {
            m_orbitInput = context.ReadValue<Vector2>() * m_mouseInputSensitivity;
            m_interactionMode |= InteractionMode.Orbit;
        }

        private void OnZoomPerformed(InputAction.CallbackContext context)
        {
            Vector2 mouseDelta = context.ReadValue<Vector2>() * m_mouseInputSensitivity;
            float magnitude = Math.Max(Mathf.Abs(mouseDelta.x), Mathf.Abs(mouseDelta.y));
            float sign = Mathf.Sign(mouseDelta.x + mouseDelta.y);
            m_zoomInput = magnitude * sign;
            m_interactionMode |= InteractionMode.Zoom;
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            m_moveInput = context.ReadValue<Vector2>();
            m_interactionMode |= InteractionMode.Move;
        }

        private void OnLookAroundPerformed(InputAction.CallbackContext context)
        {
            m_lookAroundInput = context.ReadValue<Vector2>() * m_mouseInputSensitivity;
            m_interactionMode |= InteractionMode.LookAround;
        }

        private void OnOrbitCancelled(InputAction.CallbackContext context)
        {
            m_orbitInput = new Vector2(0, 0);
            m_interactionMode &= ~InteractionMode.Orbit;
        }

        private void OnPanCancelled(InputAction.CallbackContext context)
        {
            m_panInput = new Vector2(0, 0);
            m_interactionMode &= ~InteractionMode.Pan;
        }

        private void OnZoomCancelled(InputAction.CallbackContext context)
        {
            m_zoomInput = 0;
            m_interactionMode &= ~InteractionMode.Zoom;
        }

        private void OnMoveCancelled(InputAction.CallbackContext context)
        {
            m_moveInput = new Vector2(0, 0);
            m_interactionMode &= ~InteractionMode.Move;
        }

        private void OnLookAroundCancelled(InputAction.CallbackContext context)
        {
            m_lookAroundInput = new Vector2(0, 0);
            m_interactionMode &= ~InteractionMode.LookAround;
        }

        private void ProcessInputs()
        {
            if ((m_interactionMode & InteractionMode.Pan) != 0)
            {
                Vector3 right = transform.right;
                Vector3 up = transform.up;
                Vector3 pan = (-right * m_panInput.x - up * m_panInput.y) * m_panSpeed * Time.deltaTime;
                m_targetPosition += pan * m_zoomDistance;
            }

            if ((m_interactionMode & InteractionMode.Orbit) != 0)
            {
                m_orbitYaw += m_orbitInput.x * m_orbitSpeed * Time.deltaTime;
                m_orbitPitch -= m_orbitInput.y * m_orbitSpeed * Time.deltaTime;
                m_orbitPitch = Mathf.Clamp(m_orbitPitch, -85f, 85f);
            }

            if ((m_interactionMode & InteractionMode.Zoom) != 0)
            {
                m_zoomDistance = Mathf.Max(m_zoomDistance + (-m_zoomInput * Mathf.Pow(m_zoomDistance, 0.25f) * m_zoomSpeed), m_minZoomDistance);
            }

            if ((m_interactionMode & InteractionMode.LookAround) != 0)
            {
                // Rotate the forward vector based on input
                float yawDelta = m_lookAroundInput.x * Time.deltaTime * m_lookAroundSpeed;
                float pitchDelta = -m_lookAroundInput.y * Time.deltaTime * m_lookAroundSpeed;

                // Apply the deltas directly to orbit angles
                m_orbitYaw += yawDelta;
                m_orbitPitch += pitchDelta;
                m_orbitPitch = Mathf.Clamp(m_orbitPitch, -85f, 85f);

                // Update target position to maintain distance
                Quaternion rotation = Quaternion.Euler(m_orbitPitch, m_orbitYaw, 0f);
                Vector3 direction = rotation * Vector3.forward;
                m_targetPosition = transform.position + direction * m_zoomDistance;
            }

            if ((m_interactionMode & InteractionMode.Move) != 0)
            {
                // Handle move
                Vector3 forward = transform.forward;
                Vector3 right = transform.right;

                forward *= m_moveInput.y * Time.deltaTime * m_moveSpeed;
                right *= m_moveInput.x * Time.deltaTime * m_moveSpeed;

                m_targetPosition += forward;
                m_targetPosition += right;
            }
        }
        
        private void CalculateMouseInputSensitivity()
        {
            // The higher the DPI, the LESS sentitive we need to make the inputs.
            // As the input delta values are in pixels, the same physical
            // movement of a mouse on higher DPI will yield higher values.
            float screenDpi = Screen.dpi != 0 ? Screen.dpi : c_referenceDpi;
            m_mouseInputSensitivity = c_referenceDpi / screenDpi;
        }

        private void UpdateCameraPosition()
        {
            Quaternion rotation = Quaternion.Euler(m_orbitPitch, m_orbitYaw, 0f);
            Vector3 direction = rotation * Vector3.back;
            transform.position = m_targetPosition + direction * m_zoomDistance;
            transform.rotation = rotation;
        }

        private float CalculateFrameDistance(Bounds bounds)
        {
            // We can consider the camera frustum as well but this is reasonable enough for now.
            Vector3 corner = bounds.size * 2;
            return Mathf.Max(corner.x, corner.y, corner.z);
        }
    }
}
