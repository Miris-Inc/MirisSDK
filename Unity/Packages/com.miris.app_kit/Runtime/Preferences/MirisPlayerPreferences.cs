using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

using UnityEngine;

namespace Miris.Runtime
{
    public class MirisPlayerPreferences : MonoBehaviour, IPreferences
    {
        [SerializeField]
        private MirisStreamController m_streamController;

        [SerializeField]
        private MirisStream m_stream;

        // Default values 
        private LodRefinementParameters m_lodRefinementParametersDefault;

        // --------------------------------------------------------------------
        // Unity overrides
        // --------------------------------------------------------------------

        protected void Awake()
        {
            Debug.Assert(m_streamController != null, "Must reference an MirisStreamController component");
            Debug.Assert(m_stream != null, "Must reference an MirisStream component");
            
        }

        protected void OnDisable()
        {
            if (m_streamController != null && m_streamController.IsActive())
            {
                m_streamController.GetAssetManager().ServerEnvironmentChanged -= OnEnvironmentChanged;
                m_streamController.m_onMetadataLoadedActions.Remove(LoadScenePreferences);
            }
        }
        
        protected async void Start()
        {
            Debug.Assert(m_streamController.isActiveAndEnabled);

            SaveDefaultPreferences();
            await LoadPreferences();

            RegisterCallbacks();
        }

        private void OnEnvironmentChanged(string environment)
        {
            Preferences.instance.streaming.environment = environment;
            _ = Preferences.Save();
        }

        private string GetSceneKey()
        {
            if (m_stream.IsLoaded())
            {
                return m_stream.m_assetId;
            }
            return "";
        }

        private void RegisterCallbacks()
        {
            m_streamController.GetAssetManager().ServerEnvironmentChanged += OnEnvironmentChanged;
            m_streamController.m_onMetadataLoadedActions.Add(LoadScenePreferences);
        }

        // --------------------------------------------------------------------
        // IPreferences interface
        // --------------------------------------------------------------------        

        public void SavePreferences()
        {
            Preferences.instance.streaming.environment = m_streamController.GetAssetManager().SelectedEnvironment ?? "";

            if (m_stream.IsLoaded())
            {
                // Set per-scene prefs
                Preferences.ScenePreferences perScenePrefs = new();
                perScenePrefs.m_lodRefinementParameters = m_streamController.m_lodRefinementParameters;
                perScenePrefs.m_fadeLargeSplats = m_streamController.fadeLargeSplats;

                // Store
                Preferences.instance.scenes[GetSceneKey()] = perScenePrefs;
            }

            _ = Preferences.Save();
        }

        public async Task LoadPreferences()
        {
            await Preferences.completedLoading;

            string environment = Preferences.instance.streaming.environment;
            if (!string.IsNullOrWhiteSpace(environment))
            {
                await m_streamController.GetAssetManager().SetServerEnvironment(environment);
            }

            m_streamController.GetAssetManager().SetTags(Preferences.instance.ResolveTags(Preferences.instance.streaming.tags));
            m_streamController.GetAssetManager().SetViewerKey(Preferences.instance.ResolveViewerKey(Preferences.instance.streaming.viewerKey));

            // Load scene preferences
            LoadScenePreferences();
        }

        private void LoadScenePreferences()
        {
            if (!m_stream.IsLoaded())
            {
                return;
            }


            if (Preferences.instance.scenes.TryGetValue(GetSceneKey(), out Preferences.ScenePreferences perScenePrefs))
            {
                // Only apply a few select ones for now.  Don't wanna override with any values that is 
                // not yet exposed on the developer UI.
                m_streamController.m_lodRefinementParameters.m_lodSelectionMode = perScenePrefs.m_lodRefinementParameters.m_lodSelectionMode;
                m_streamController.m_lodRefinementParameters.m_lodMaxDistance = perScenePrefs.m_lodRefinementParameters.m_lodMaxDistance;
                m_streamController.m_lodRefinementParameters.m_lowestLodLimit = perScenePrefs.m_lodRefinementParameters.m_lowestLodLimit;
                m_streamController.m_lodRefinementParameters.m_highestLodLimit = perScenePrefs.m_lodRefinementParameters.m_highestLodLimit;
                m_streamController.m_lodRefinementParameters.m_fixedLodIndex = perScenePrefs.m_lodRefinementParameters.m_fixedLodIndex;
                m_streamController.m_lodRefinementParameters.m_splatCountBudget = perScenePrefs.m_lodRefinementParameters.m_splatCountBudget;

                m_streamController.fadeLargeSplats = perScenePrefs.m_fadeLargeSplats;
            }
        }

        public void ClearPreferences()
        {
            Preferences.instance.streaming = new();
        }

        public void SaveDefaultPreferences()
        {
            m_lodRefinementParametersDefault = m_streamController.m_lodRefinementParameters;
        }

        public async void RestoreDefaultPreferences()
        {
            await m_streamController.GetAssetManager().ResetEnvironmentToDefault();

            if (m_stream.IsLoaded())
            {
                // Clear scene specific prefs.
                Preferences.instance.scenes.Remove(GetSceneKey());

                // Copy over defaults.
                m_streamController.m_lodRefinementParameters = m_lodRefinementParametersDefault;

                // Re-query the scene metadata
                m_streamController.m_loadedMetadata = false;
                m_streamController.GetAssetMetadata();
            }

            ClearPreferences();

            _ = Preferences.Save();
        }
    }
}
