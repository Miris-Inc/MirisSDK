using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Aqua.Runtime
{

    public class XREnvironment : MonoBehaviour {

        private string m_materialPath;
        public float m_fadeDuration = 1.0f;
        private bool m_skyboxIsVisible = true;
        
        public void AddSkybox(string vrPath, float yaw) {

            if (!m_skyboxIsVisible) {
                return;
            }
            
            m_materialPath = vrPath;
            
            if (Camera.main == null) {
                Debug.Log("No camera found.");
                return;
            }

            Material skyboxMaterial = Resources.Load<Material>(path: m_materialPath);
            
            Skybox skybox = Camera.main.GetComponent<Skybox>();
            
            if (skybox == null) {
                skybox = Camera.main.gameObject.AddComponent<Skybox>();
            }
            
            Camera.main.clearFlags = CameraClearFlags.Skybox;
            skybox.material = skyboxMaterial;
            
            skybox.material.SetFloat("_Yaw", yaw);
            
            m_skyboxIsVisible = true;
            FadeInSkybox(skybox);
        }

        public void RemoveSkybox() {
            
            ProcessSkyboxFadeOut();
        
        }
        
        public void HideSkybox() {
            
            ProcessSkyboxFadeOut();
            
        }

        private void ProcessSkyboxFadeOut() {
            
            Skybox existingSkybox = Camera.main.GetComponent<Skybox>();

            if (existingSkybox != null && existingSkybox.material != null) {
                StartCoroutine(FadeOutSkybox(existingSkybox));
            }
        }

        public void SetSkyboxVisible(bool isEnabled) {
            m_skyboxIsVisible = isEnabled;
        }
        
        public bool IsSkyboxVisible() {
            return m_skyboxIsVisible;
        }
        
        private IEnumerator FadeOutSkybox(Skybox skybox) {
            
            // Fade out the current skybox
            yield return StartCoroutine(FadeSkybox(0.0f, m_fadeDuration, false, skybox));
            
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            
            // Remove the skybox component after the fadeout completes
            Destroy(skybox);
        }
        
        private void FadeInSkybox(Skybox skybox) {
            StartCoroutine(FadeSkybox(1.0f, m_fadeDuration, true, skybox));
        }
        
        private IEnumerator FadeSkybox(float targetAlpha, float duration, bool isFadingIn, Skybox skybox) {
            
            float elapsedTime = 0.0f;
            
            float startAlpha = isFadingIn ? 0.0f : 1.0f;
            
            while (elapsedTime < duration) {
                
                elapsedTime += Time.deltaTime;
                
                float t= elapsedTime / duration;
                
                float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                
                skybox.material.SetFloat("_Alpha",newAlpha);
                
                yield return null;
                
            }
        }
        
    }
   
}
