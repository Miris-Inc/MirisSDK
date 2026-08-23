// Copyright © 2026 Miris, Inc. All rights reserved.
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.XR;
#if MIRIS_ENABLE_XR_MANAGEMENT
using UnityEngine.XR.Management;
#endif

namespace Miris.Runtime
{
    public static class XRFrameInfo {
        public static int m_multipassId { get; set; }
    }
    
    public class XRUtils
    {

        // 1.6f is used to simulate an average height for a user in desktop mode
        // in order to account for distance to the floor when spawning assets at the ground
        public const float c_desktopUserHeight = 1.6f;

        // XR Management (com.unity.xr.management) is an optional dependency.
        static public bool IsXR()
        {
#if MIRIS_ENABLE_XR_MANAGEMENT
            XRLoader xrLoader = XRGeneralSettings.Instance?.Manager?.activeLoader;
            return xrLoader != null && !string.IsNullOrWhiteSpace(xrLoader.name);
#else
            return false;
#endif
        }

        static public bool IsAR()
        {
#if MIRIS_ENABLE_XR_MANAGEMENT
            XRLoader xrLoader = XRGeneralSettings.Instance?.Manager?.activeLoader;
            return xrLoader != null && xrLoader.name.Contains("AR");
#else
            return false;
#endif
        }

        // TODO:: Move subsystem to be a member variable to reduce cost of instantiating new subsystem list every call
        private XRInputSubsystem GetXRInputSubsystem()
        {
            List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            return subsystems.Count > 0 ? subsystems[0] : null;
        }

        // TODO: Profile and look into how to find exact y value of current position
        // Most likely Distance comapres from originTransform x/z values to points
        public (Vector3, Quaternion, Vector3) GetXRFloorTransform(Transform originTransform){

            float additionalYOffset = IsXR() ? 0 : c_desktopUserHeight;
            additionalYOffset = IsAR() ? 1.0f : additionalYOffset;

            if (originTransform != null){
                Vector3 position = new Vector3(originTransform.position.x, originTransform.position.y - additionalYOffset, originTransform.position.z);
                return (position, originTransform.rotation, originTransform.localScale);
            } else {
                Vector3 position = new Vector3(Camera.main.transform.position.x, Camera.main.transform.position.y - additionalYOffset, Camera.main.transform.position.z);
                return (Vector3.zero, Quaternion.identity, Vector3.one);
            }
        }

        
        public float GetXRFloorHeight(GameObject gameObject)
        {
            if(gameObject != null){
                (Vector3 pos, Quaternion rot, Vector3 scale) = GetXRFloorTransform(gameObject.transform);
                return pos.y;
            } else { 
                return -1 * c_desktopUserHeight;
            }
        }
        
        public bool IsSinglePassXR() 
        {
                
            if (!XRSettings.enabled) 
            {
                return false;
            }

            return XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced ||
                   XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassMultiview;

        }

        public bool IsMultiPassXR() 
        {
            if (!XRSettings.enabled) 
            {
                return false;
            }

            return XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.MultiPass; 
            
        }

        public bool SortMonoOrLeftEye(Camera camera) {
            return (camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Mono || camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left);
        }
        
        public bool IsStereo()
        {
            return (IsMultiPassXR() || IsSinglePassXR());    
        }
        
        public int GetEyeCount() 
        {
            if (!XRSettings.enabled) 
            {
                return 1;
            }

            if (IsSinglePassXR()) {
                return 2;
            } 

            return 1;
        }

    }
}
