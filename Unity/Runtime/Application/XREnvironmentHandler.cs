using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Aqua.Runtime
{

    // --------------------------------------------------------------------
    // XR skybox handling
    // --------------------------------------------------------------------
    public class XREnvironmentHandler : MonoBehaviour {

        private string m_previousPath;

        private XREnvironment m_environment;
        private float m_spawnYaw = 0.0f;

        public void Init(){

            m_environment = gameObject.AddComponent<XREnvironment>();
            DisableEnvironment();
        }

        public void Destroy(){
            if(m_environment != null){
                Destroy(m_environment);
            }
        }

        public void DisableEnvironment(){
            if(m_environment != null){
                if(!m_environment.IsSkyboxVisible()){
                    return;
                }

                m_environment.HideSkybox();
                m_environment.SetSkyboxVisible(false);
            }
        }

        public void EnableEnvironment(){
            if(m_environment != null){
                if(m_environment.IsSkyboxVisible()){
                    return;
                }

                m_environment.SetSkyboxVisible(true);
                if(m_previousPath != ""){
                    FadeInEnvironment(m_previousPath);
                }
            }
        }

        public void SetSpawnYaw(float yaw){
            m_spawnYaw = yaw;
        }


        public void FadeOutEnvironment(){
            m_environment.RemoveSkybox();
        }

        private void FadeInEnvironment(string environmentPath){
            m_environment.AddSkybox(environmentPath, -m_spawnYaw);
        }

        private void CheckEnvironmentChange(string envPath){
            if(envPath != m_previousPath){
                if(envPath != ""){
                    FadeInEnvironment(envPath);
                }
                m_previousPath = envPath;
            }
        }

        public void CheckUpdate(string envPath){
            CheckEnvironmentChange(envPath);
        }

        public void SetFadeDuration(float fadeDuration)
        {
            m_environment.m_fadeDuration = fadeDuration;
        }

    }

    
}
