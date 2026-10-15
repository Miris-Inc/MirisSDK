// Copyright © 2026 Miris, Inc. All rights reserved.
using UnityEngine;
#if MIRIS_ENABLE_XR_MANAGEMENT
using UnityEngine.XR.Management;
#endif

namespace Miris.Runtime
{
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
    }
}
