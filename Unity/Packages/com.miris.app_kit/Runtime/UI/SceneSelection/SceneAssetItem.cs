// Copyright © 2025 Miris, Inc. All rights reserved.

// Unity Engine
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace Miris.Runtime
{
    public class SceneAssetItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private SceneAssetInfo m_assetInfo;
        private MirisPlayerSceneManager m_sceneManager;
        private UserInterfaceManager m_uiManager;
        private MobileUserInterfaceManager m_mobileUIManager;
        public void SetAssetInfo(SceneAssetInfo assetInfo)
        {
            m_assetInfo = assetInfo;
        }
        public SceneAssetInfo GetAssetInfo()
        {
            return m_assetInfo;
        }

        public void Start()
        {
            m_sceneManager = FindFirstObjectByType<MirisPlayerSceneManager>();
            m_uiManager = GetComponentInParent<UserInterfaceManager>();
            m_mobileUIManager = GetComponentInParent<MobileUserInterfaceManager>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (m_sceneManager != null)
            {
                HapticsUtility.SendHapticImpulse(0.5f, 0.025f, HapticsUtility.Controller.Both);
                if(m_uiManager != null){
                    m_uiManager.Minimize();
                }
                if(m_mobileUIManager != null){
                    m_mobileUIManager.SetCurrentAssetInfo(m_assetInfo);
                    m_mobileUIManager.ClearUserInterfaceState();
                }
                m_sceneManager.ChangeScene(m_assetInfo.m_assetId);
            }
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            HapticsUtility.SendHapticImpulse(0.2f, 0.025f, HapticsUtility.Controller.Both);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HapticsUtility.SendHapticImpulse(0.2f, 0.025f, HapticsUtility.Controller.Both);
        }
    }
}
