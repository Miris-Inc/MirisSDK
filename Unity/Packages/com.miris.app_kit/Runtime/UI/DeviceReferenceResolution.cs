// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;
using UnityEngine.UI;

namespace Miris.Runtime
{
    public class DeviceReferenceResolution : MonoBehaviour
    {
        [SerializeField]
        private CanvasScaler m_scaler;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            AdjustReferenceResolution();
        }

        void AdjustReferenceResolution()
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            m_scaler.referenceResolution = new Vector2(screenWidth, screenHeight);
        }
    }
}
