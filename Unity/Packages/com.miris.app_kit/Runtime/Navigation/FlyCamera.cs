// Copyright © 2024 Miris. All rights reserved.

using UnityEngine;
using UnityEngine.InputSystem;

namespace Miris.Runtime
{
    // Allows the Keyboard to control a camera
    // WSAD for movement.
    // Right click drag mouse to look around.
    public class FlyCamera : MonoBehaviour
    {
        [SerializeField]
        private float m_mainSpeed = 10.0f;

        [SerializeField]
        private float m_distanceChangeLimit = 0.2f;

        [SerializeField]
        private float m_lookAroundSensitivity = 2.5f;

        private PlayerInputActions m_playerInputActions;
        private Vector2 m_moveInput = new(0, 0);

        private bool m_enableLookAround = false;
        private Vector2 m_lookAroundInput = new(0, 0);
        private float m_rotationY = 0.0f;

        // --------------------------------------------------------------------
        // Look-around Handling
        // --------------------------------------------------------------------    

        private void OnEnable()
        {
            if (m_playerInputActions == null)
            {
                m_playerInputActions = new();
            }

            m_playerInputActions.Player.Move.performed += OnMovePerformed;
            m_playerInputActions.Player.Move.canceled += OnMoveCanceled;
            m_playerInputActions.Player.EnableLookAround.performed += OnEnableLookPerformed;
            m_playerInputActions.Player.EnableLookAround.canceled += OnEnableLookCancelled;
            m_playerInputActions.Player.LookAround.performed += OnLookAroundPerformed;
            m_playerInputActions.Player.LookAround.canceled += OnLookAroundCancelled;
            m_playerInputActions.Enable();
        }

        private void OnDisable()
        {
            m_playerInputActions?.Disable();
        }

        void Update()
        {
            // Compute distance change based on speed, time, and input magnitude.  Apply a limiter so we don't go flying
            Vector3 inputMoveVector = GetMoveVector();
            float inputMagnitude = inputMoveVector.magnitude;
            float distanceChange = Mathf.Min(inputMagnitude * m_mainSpeed * Time.deltaTime, m_distanceChangeLimit);

            // Apply translation.
            Vector3 translate = inputMoveVector.normalized * distanceChange;
            transform.Translate(translate);

            if (m_enableLookAround)
            {
                float rotationX = transform.localEulerAngles.y + m_lookAroundInput.x * m_lookAroundSensitivity;
                m_rotationY += m_lookAroundInput.y * m_lookAroundSensitivity;
                m_rotationY = Mathf.Clamp(m_rotationY, -90, 90);
                transform.localEulerAngles = new Vector3(-m_rotationY, rotationX, 0.0f);
            }
        }

        // --------------------------------------------------------------------
        // Look-around Handling
        // --------------------------------------------------------------------    

        private void OnLookAroundPerformed(InputAction.CallbackContext context)
        {
            m_lookAroundInput = context.ReadValue<Vector2>();
        }

        private void OnLookAroundCancelled(InputAction.CallbackContext context)
        {
            m_lookAroundInput.x = 0;
            m_lookAroundInput.y = 0;
        }

        private void OnEnableLookPerformed(InputAction.CallbackContext context)
        {
            m_enableLookAround = true;
        }

        private void OnEnableLookCancelled(InputAction.CallbackContext context)
        {
            m_enableLookAround = false;
        }

        // --------------------------------------------------------------------
        // Movement handling
        // --------------------------------------------------------------------    

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            m_moveInput = context.ReadValue<Vector2>();
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            m_moveInput.x = 0;
            m_moveInput.y = 0;
        }

        private Vector3 GetMoveVector()
        {
            return new Vector3(m_moveInput.x, 0, m_moveInput.y);
        }
    }
}
