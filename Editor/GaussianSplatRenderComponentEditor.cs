// Copyright © 2024 Miris. All rights reserved.

// Unity engine
using UnityEditor;

// Aqua
using Aqua.Runtime;

namespace Aqua.Editor
{
    /// <summary>
    /// Renders a custom editor for the GaussianSplatRenderComponent such that we can display a few 
    /// non-serialized properties for debugging purposes.
    /// </summary>
    [CustomEditor(typeof(GaussianSplatRenderComponent)), CanEditMultipleObjects]
    public class GaussianSplatRenderComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector (for other properties)
            DrawDefaultInspector();

            GaussianSplatRenderComponent renderComponent = (GaussianSplatRenderComponent)target;

            // Display the read-only properties
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Splat Count", renderComponent.GetSplatCount());
            EditorGUILayout.BoundsField("Bounds", renderComponent.GetObjectBounds());
            EditorGUI.EndDisabledGroup();
        }
    }
}