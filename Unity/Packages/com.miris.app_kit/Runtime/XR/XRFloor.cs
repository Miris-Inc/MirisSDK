using System.Collections.Generic;

using UnityEngine;
using UnityEngine.XR;

namespace Miris.Runtime
{
    public class XRFloor : MonoBehaviour
    {
        [SerializeField]
        private GameObject m_xrOrigin;
        
        [SerializeField]
        public GameObject m_floorPrefab;

        private XRUtils m_xrUtils = new XRUtils();

        private GameObject m_floorObject;

        private void UpdateTransform(){
            Transform cameraOffsetTransform = m_xrOrigin.transform.Find("Camera Offset");
            (Vector3 pos, Quaternion rot, Vector3 scale) = m_xrUtils.GetXRFloorTransform(cameraOffsetTransform);
            transform.position = pos;
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void OnEnable()
        {
            if(m_floorObject == null){
                UpdateTransform();
                m_floorObject = Instantiate(m_floorPrefab, transform);
            }

            
        }

        void OnDisable()
        {
            if(m_floorObject != null){
                Destroy(m_floorObject);
                m_floorObject = null;
            }
        }

        void Update(){
            if(m_floorObject != null){
                UpdateTransform();
                m_floorObject.transform.position = transform.position;
            }
        }
    }
}
