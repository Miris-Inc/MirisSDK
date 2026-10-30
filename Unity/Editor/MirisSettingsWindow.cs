// Copyright © 2026 Miris, Inc. All rights reserved.

// Standard library
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Unity engine
using UnityEditor;
using UnityEngine;

// Miris package
using Miris.Runtime;

namespace Miris.Editor
{
    /// <summary>
    /// Edits the project's <see cref="MirisSettings"/>, plus whatever fields installed packages add
    /// through <see cref="MirisSettingsExtension"/>.
    /// </summary>
    public class MirisSettingsWindow : EditorWindow
    {
        private const string c_defaultAssetPath = "Assets/Resources/" + MirisSettings.ResourcesPath + ".asset";

        private MirisSettings m_settings;
        private SerializedObject m_serializedSettings;
        private SerializedObject m_serializedSceneSettings;
        private Vector2 m_scrollPosition;

        private static List<MirisSettingsExtension> s_extensions;

        [MenuItem("Miris/Settings", false, 20)]
        public static void Open()
        {
            MirisSettingsWindow window = GetWindow<MirisSettingsWindow>();
            window.titleContent = new GUIContent("Miris Settings");
            window.minSize = new Vector2(320, 360);
            window.Show();
        }

        /// <summary>
        /// The project's settings asset, created under Assets/Resources if it has none yet.
        /// </summary>
        public static MirisSettings GetOrCreateSettingsAsset()
        {
            return GetOrCreateSettingsAsset<MirisSettings>(MirisSettings.ResourcesPath);
        }

        /// <summary>
        /// The settings asset at a Resources-relative path, created under Assets/Resources if
        /// the project has none yet.
        /// </summary>
        public static T GetOrCreateSettingsAsset<T>(string resourcesPath) where T : ScriptableObject
        {
            T settings = Resources.Load<T>(resourcesPath);
            if (settings != null)
            {
                return settings;
            }

            string assetPath = $"Assets/Resources/{resourcesPath}.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            settings = CreateInstance<T>();
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            MirisSceneAsset.ActiveSceneChanged += Repaint;
        }

        private void OnDisable()
        {
            MirisSceneAsset.ActiveSceneChanged -= Repaint;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            MirisSettings.NotifyChanged();
            MirisSceneAsset.NotifyChanged();
            Repaint();
        }

        private bool TryBindSettings()
        {
            if (m_settings == null)
            {
                m_settings = Resources.Load<MirisSettings>(MirisSettings.ResourcesPath);
            }

            // A domain reload (entering play mode) keeps m_settings but drops the SerializedObject.
            if (m_settings != null
                && (m_serializedSettings == null || m_serializedSettings.targetObject != m_settings))
            {
                m_serializedSettings = new SerializedObject(m_settings);
            }

            return m_settings != null;
        }

        private void OnGUI()
        {
            if (!TryBindSettings())
            {
                EditorGUILayout.HelpBox(
                    "This project has no Miris settings yet, so it uses the defaults.",
                    MessageType.Info);
                if (GUILayout.Button($"Create {c_defaultAssetPath}"))
                {
                    GetOrCreateSettingsAsset();
                    MirisSettings.NotifyChanged();
                }
                return;
            }

            m_serializedSettings.Update();

            using (EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(m_scrollPosition))
            {
                m_scrollPosition = scroll.scrollPosition;

                EditorGUILayout.LabelField("Project", PlayerSettings.productName, EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(m_serializedSettings.FindProperty("m_viewerKey"));
                foreach (MirisSettingsExtension extension in GetExtensions())
                {
                    extension.DrawProjectFields();
                }

                EditorGUILayout.Space();
                DrawSceneSettings();

                EditorGUILayout.Space();
                if (GUILayout.Button("Open Asset Browser"))
                {
                    MirisAssetBrowserWindow.Open();
                }
            }

            if (m_serializedSettings.ApplyModifiedProperties())
            {
                MirisSettings.NotifyChanged();
            }
        }

        internal static List<MirisSettingsExtension> GetExtensions()
        {
            s_extensions ??= TypeCache.GetTypesDerivedFrom<MirisSettingsExtension>()
                .Where(type => !type.IsAbstract)
                .Select(type => (MirisSettingsExtension)Activator.CreateInstance(type))
                .ToList();
            return s_extensions;
        }

        private void DrawSceneSettings()
        {
            string scenePath = MirisSceneAsset.ActiveScenePath;
            if (string.IsNullOrEmpty(scenePath))
            {
                EditorGUILayout.LabelField("Active Scene", "Untitled", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Save the scene to give it settings.", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField("Active Scene", Path.GetFileNameWithoutExtension(scenePath), EditorStyles.boldLabel);

            MirisSceneSettings sceneSettings = MirisSceneSettings.Find(scenePath);
            if (sceneSettings == null)
            {
                if (GUILayout.Button("Create Scene Settings"))
                {
                    MirisSceneAssets.Create<MirisSceneSettings>(MirisSceneSettings.ResourcesFolder, scenePath);
                    MirisSceneAsset.NotifyChanged();
                }
            }
            else
            {
                if (m_serializedSceneSettings == null || m_serializedSceneSettings.targetObject != sceneSettings)
                {
                    m_serializedSceneSettings = new SerializedObject(sceneSettings);
                }

                m_serializedSceneSettings.Update();
                EditorGUILayout.PropertyField(m_serializedSceneSettings.FindProperty("m_viewerKey"));
                if (m_serializedSceneSettings.ApplyModifiedProperties())
                {
                    MirisSceneAsset.NotifyChanged();
                }
            }

            foreach (MirisSettingsExtension extension in GetExtensions())
            {
                extension.DrawSceneFields(scenePath);
            }
        }
    }

    /// <summary>
    /// The default Inspector for <see cref="MirisSettings"/>, plus the change notification the
    /// Miris > Settings window sends.
    /// </summary>
    [CustomEditor(typeof(MirisSettings))]
    internal class MirisSettingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (DrawDefaultInspector())
            {
                MirisSettings.NotifyChanged();
            }
        }
    }
}
