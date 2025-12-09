using UnityEngine;

namespace Miris.Runtime
{
    public class CameraOrientation : MonoBehaviour
    {
        private Camera m_camera;
        private ScreenOrientation m_lastOrientation;

        void Start()
        {
            m_camera = GetComponent<Camera>();
            m_camera.usePhysicalProperties = true;
            m_camera.sensorSize = new Vector2(36f, 24f);
            m_camera.focalLength = 20.8f;
            m_camera.gateFit = Camera.GateFitMode.Fill;

            m_lastOrientation = Screen.orientation;

            UpdateSensor(); 
        }

        void Update()
        {
            if(Screen.orientation != m_lastOrientation)
            {
                m_lastOrientation = Screen.orientation;
                UpdateSensor();
            }
        }

        void UpdateSensor()
        {
            if(Screen.height > Screen.width){
                m_camera.sensorSize = new Vector2(24f, 36f);
            } else {
                m_camera.sensorSize = new Vector2(36f, 24f);
            }
        }

    }
}
