using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Miris.Runtime
{
    public class XRTeleportUtils 
    {
        static private void Teleport(Vector3 destination, TeleportationProvider teleportProvider)
        {
            if (teleportProvider)
            {
                TeleportRequest teleportRequest = new TeleportRequest()
                {
                    destinationPosition = destination,
                    destinationRotation = Camera.main.transform.rotation
                };
                teleportProvider.QueueTeleportRequest(teleportRequest);
            }
        }

        static public GameObject SimulateTeleport(XRRayInteractor rayInteractor, TeleportationProvider teleportProvider)
        {
            float yOffset = XRUtils.IsXR() ? 0.0f : XRUtils.c_desktopUserHeight;
            if (rayInteractor != null){
                if(rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit)){
                    Vector3 destination = new Vector3(hit.point.x, hit.point.y + yOffset, hit.point.z);
                    Debug.Log("Teleport Destination: " + destination);
                    Teleport(destination, teleportProvider);
                    return hit.transform.gameObject;
                }
            }
            return null;
        }

        static public bool ValidTeleportLocation(XRRayInteractor rayInteractor)
        {
            if (rayInteractor != null)
            {
                if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
