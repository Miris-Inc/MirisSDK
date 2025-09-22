// Copyright © 2024 Miris. All rights reserved.

// Unity engine
using UnityEditor;

// Aqua
using Aqua.Runtime;

namespace Aqua.Editor
{
    /// <summary>
    /// Renders a custom editor for the AquaSceneObjectReference such that we can display a few 
    /// non-serialized properties for debugging purposes.
    /// </summary>
    [CustomEditor(typeof(AquaSceneObjectReference)), CanEditMultipleObjects]
    public class AquaSceneObjectReferenceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector (for other properties)
            DrawDefaultInspector();

            AquaSceneObjectReference sceneObjectRef = (AquaSceneObjectReference)target;

            // Display the read-only properties
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Id", sceneObjectRef.m_sceneObject.GetId());
            EditorGUILayout.LabelField("Name", sceneObjectRef.m_sceneObject.GetName());
            EditorGUI.EndDisabledGroup();
        }
    }
}