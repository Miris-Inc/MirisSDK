// Copyright © 2026 Miris, Inc. All rights reserved.

// Standard library
using System;
using System.IO;

// Unity engine
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Miris package
using Miris.Runtime;

namespace Miris.Editor
{
    /// <summary>
    /// Creates <see cref="MirisSceneAsset"/>s and keeps their scene paths current for builds.
    /// </summary>
    public class MirisSceneAssets : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        /// <summary>
        /// Creates a <typeparamref name="T"/>, overriding nothing yet, for the scene at
        /// <paramref name="scenePath"/>, in <c>Assets/Resources/</c><paramref name="resourcesFolder"/>.
        /// </summary>
        public static T Create<T>(string resourcesFolder, string scenePath) where T : MirisSceneAsset
        {
            string folder = "Assets/Resources/" + resourcesFolder;
            Directory.CreateDirectory(folder);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{Path.GetFileNameWithoutExtension(scenePath)}.asset");

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);

            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("m_sceneGuid").stringValue = AssetDatabase.AssetPathToGUID(scenePath);
            serialized.FindProperty("m_scenePath").stringValue = scenePath;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>Draws the scene an asset belongs to, read-only, for its Inspector.</summary>
        public static void DrawScene(MirisSceneAsset asset)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Scene", asset.ScenePath);
            }
        }

        // A scene moved or renamed since its assets were saved would otherwise lose them in the player.
        public void OnPreprocessBuild(BuildReport report)
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<MirisSceneAsset>())
            {
                if (type.IsAbstract)
                {
                    continue;
                }

                foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name))
                {
                    MirisSceneAsset asset = AssetDatabase.LoadAssetAtPath<MirisSceneAsset>(
                        AssetDatabase.GUIDToAssetPath(guid));
                    if (asset != null)
                    {
                        RefreshScenePath(asset);
                    }
                }
            }
        }

        private static void RefreshScenePath(MirisSceneAsset asset)
        {
            string scenePath = asset.ScenePath;
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogWarning($"{AssetDatabase.GetAssetPath(asset)}: its scene no longer exists, so "
                                 + "this asset applies to no scene.", asset);
                return;
            }

            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty savedPath = serialized.FindProperty("m_scenePath");
            if (savedPath.stringValue != scenePath)
            {
                savedPath.stringValue = scenePath;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
    }

    /// <summary>
    /// Draws <see cref="MirisSceneSettings"/> and passes edits on so a running controller picks
    /// them up.
    /// </summary>
    [CustomEditor(typeof(MirisSceneSettings))]
    internal class MirisSceneSettingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            MirisSceneAssets.DrawScene((MirisSceneAsset)target);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_viewerKey"));
            if (serializedObject.ApplyModifiedProperties())
            {
                MirisSceneAsset.NotifyChanged();
            }
        }
    }
}
