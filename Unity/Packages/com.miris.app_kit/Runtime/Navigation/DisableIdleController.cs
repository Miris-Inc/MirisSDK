// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

namespace Miris.Runtime
{
    public class DisableIdleController : MonoBehaviour
    {
        private ActionBasedController m_controller;
        private float m_lastInteractionTime = 0.0f;
        public float m_deactivationTime = 5.0f;
        private Vector3 m_lastPosition = Vector3.zero;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            m_controller = gameObject.GetComponent<ActionBasedController>();
            m_controller.positionAction.action.performed += OnMoveOccured;
        }

        void EventOccured(){
            m_lastInteractionTime = Time.time;
            m_controller.gameObject.SetActive(true);
        }

        void OnDestroy(){
            m_controller.positionAction.action.performed -= OnMoveOccured;
        }


        private bool VectorsAreEqual(Vector3 oldPosition, Vector3 newPosition, float tolerance = .001f){
            return Vector3.Distance(oldPosition, newPosition) <= tolerance;
        }

        void OnMoveOccured(InputAction.CallbackContext context){
            Vector3 newPosition = context.ReadValue<Vector3>();
            if(!VectorsAreEqual(m_lastPosition, newPosition)){
                m_lastPosition = newPosition;
                EventOccured();
            }
        }

        // Update is called once per frame
        void Update()
        {
            if(m_controller == null){
                return;
            }

            float currentTime = Time.time;
            if(currentTime - m_lastInteractionTime >= m_deactivationTime){
                m_controller.gameObject.SetActive(false);
            }
        }
    }
}
