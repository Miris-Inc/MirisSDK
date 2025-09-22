// Copyright © 2024 Miris. All rights reserved.

// Unity engine
using UnityEditor;

// Aqua
using Aqua.Runtime;

namespace Aqua.Editor
{
    /// <summary>
    /// Renders a custom editor for the GaussianSplatAquaDataSource such that we can display a few 
    /// non-serialized properties for debugging purposes.
    /// </summary>
    [CustomEditor(typeof(GaussianSplatAquaDataSource)), CanEditMultipleObjects]
    public class GaussianSplatAquaDataSourceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector (for other properties)
            DrawDefaultInspector();

            GaussianSplatAquaDataSource dataSourceComponent = (GaussianSplatAquaDataSource)target;

            // Display the read-only properties
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Splat Count", dataSourceComponent.GetSplatCount());
            EditorGUILayout.BoundsField("Bounds", dataSourceComponent.m_object.GetBoundingBox());
            EditorGUILayout.IntField("Lod Index", dataSourceComponent.GetLodIndex());
            EditorGUI.EndDisabledGroup();
        }
    }
}