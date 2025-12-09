// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

using System.Collections.Generic;

namespace Miris.Runtime
{
    public enum TouchGestureMode {
        Tap, 
        Swipe, 
        Pinch
    }

    public class TouchTracker {
        public Vector2 start {get; set;}
        public Vector2 position {get; set;}
        private float? startTime {get; set;}
        private bool startOnUI {get; set;}
        private bool isActive { get;set; }
        private string name {get; set;}
        public TouchGestureMode gestureMode {get; set;}

        public TouchTracker(string touchName)
        {
            start = default;
            position = default;
            startTime = null;
            startOnUI = false;
            isActive = false;
            name = touchName;
            gestureMode = TouchGestureMode.Tap;
        }

        public void StartTouch(bool isOnUI = false)
        {
            startTime = Time.time;
            start = position;
            startOnUI = isOnUI;
            isActive = true;
        }

        public void EndTouch()
        {
            startTime = null;
            start = default;
            position = default;
            startOnUI = false;
            isActive = false;
            gestureMode = TouchGestureMode.Tap;
        }

        public bool IsActive()
        {
            return isActive;
        }

        public Vector2 Delta(){
            return position - start;
        }

        public float Distance(TouchTracker other)
        {
            return (position - other.position).magnitude;
        }

        public float StartingDistance(TouchTracker other)
        {
            return (start - other.start).magnitude;
        }

        public bool BeganOnUserInterface()
        {
            return startOnUI;
        }

        public float DeltaTime()
        {
            return startTime.HasValue ? (Time.time - startTime.Value) : 0f;
        }
    }

    public class TouchControls
    {
        private List<TouchTracker> m_trackedTouches = new List<TouchTracker> {
            new TouchTracker("PrimaryTouch"), new TouchTracker("SecondaryTouch")
        };

        private float m_timeForTap = 0.1f;
        private float m_gestureSensitivity = 0.01f;

        private MobileUserInterfaceManager m_uiManager;
        private TimelineTouchUIController m_timelineController;

        public void SetUIManager(MobileUserInterfaceManager uiManager)
        {
            m_uiManager = uiManager;
        }

        public void SetTimelineController(TimelineTouchUIController timelineController)
        {
            m_timelineController = timelineController;
        }

        public void Enable(PlayerInputActions actions)
        {
            // primary touch
            actions.Touch.PrimaryTouchStart.performed += ProcessPrimaryTouch;
            actions.Touch.PrimaryTouchButton.performed += ProcessPrimaryTouchStart;
            actions.Touch.PrimaryTouchButton.canceled += ProcessPrimaryTouchComplete;

            // secondary touch
            actions.Touch.SecondaryTouchStart.performed += ProcessSecondaryTouch;
            actions.Touch.SecondaryTouchButton.performed += ProcessSecondaryTouchStart;
            actions.Touch.SecondaryTouchButton.canceled += ProcessSecondaryTouchComplete;
        }

        private bool IsTouchOverUI(Vector2 screenPosition)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = screenPosition;

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }

        /// --------------------------------------------------------------------------------
        /// InputAction Touch Methods
        /// --------------------------------------------------------------------------------
        private void ProcessPrimaryTouch(InputAction.CallbackContext context)
        {
           m_trackedTouches[0].position = context.ReadValue<Vector2>();
        }

        private void ProcessSecondaryTouch(InputAction.CallbackContext context)
        {
           m_trackedTouches[1].position = context.ReadValue<Vector2>();
        }

        private void ProcessPrimaryTouchStart(InputAction.CallbackContext context)
        {
            m_trackedTouches[0].StartTouch(IsTouchOverUI(m_trackedTouches[0].position));
            m_uiManager.TouchStart();
        }

        private void ProcessSecondaryTouchStart(InputAction.CallbackContext context)
        {
            m_trackedTouches[1].StartTouch(IsTouchOverUI(m_trackedTouches[1].position));
        }

        private void ProcessPrimaryTouchComplete(InputAction.CallbackContext context){
            
            m_uiManager.TouchEnd();
            bool startedOnInterface = m_trackedTouches[0].BeganOnUserInterface();
            if(startedOnInterface){
                return;
            }

            float touchDuration = m_trackedTouches[0].DeltaTime();
            Vector2 touchDelta = m_trackedTouches[0].Delta();
            m_trackedTouches[0].EndTouch();

            if(Mathf.Abs(touchDelta.y) > Mathf.Abs(touchDelta.x)){
            } else {
                if(touchDuration <= m_timeForTap && !startedOnInterface)
                {
                    m_timelineController.OnPlaybackStateButtonClicked();
                }
            }
        }

        private void ProcessSecondaryTouchComplete(InputAction.CallbackContext context){
            m_trackedTouches[1].EndTouch();
        }

    }
}
