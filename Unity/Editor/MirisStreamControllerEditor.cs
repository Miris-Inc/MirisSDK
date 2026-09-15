// Copyright © 2026 Miris, Inc. All rights reserved.

// Unity engine
using UnityEditor;
using UnityEngine;

// Miris package
using Miris.Runtime;

namespace Miris.Editor
{
    /// <summary>
    /// Carries the dragged asset across the DragAndDrop generic-data channel, from wherever a
    /// drag starts (MirisAssetBrowserWindow) to whichever drop target accepts it
    /// (MirisStreamAssetBrowserDropHandler).
    /// </summary>
    internal class MirisStreamDragPayload
    {
        public const string GenericDataKey = "MirisStreamController.Asset";

        public string AssetId;
        public string AssetName;
        public MirisStreamController Controller;
    }

    /// <summary>
    /// Default inspector for MirisStreamController, plus a "Browse Assets…" button that opens
    /// the standalone MirisAssetBrowserWindow. Asset browsing itself lives entirely in that
    /// window now -- this used to also host an inline browser foldout, but that's gone in favor
    /// of one browsing experience instead of two.
    /// </summary>
    [CustomEditor(typeof(MirisStreamController))]
    public class MirisStreamControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var controller = (MirisStreamController)target;

            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("Browse Assets…", GUILayout.Height(20)))
            {
                MirisAssetBrowserWindow.Open(controller);
            }
        }
    }

    /// <summary>
    /// Accepts drags started by MirisAssetBrowserWindow when they're dropped onto the Hierarchy
    /// window or the Scene View, spawning a wired-up Miris Stream instance either way through the
    /// one shared SpawnMirisStreamInstance method.
    /// </summary>
    [InitializeOnLoad]
    internal static class MirisStreamAssetBrowserDropHandler
    {
        private const string SourcePrefabPath = "Packages/com.miris.sdk.core/Prefabs/Miris Stream.prefab";

        static MirisStreamAssetBrowserDropHandler()
        {
            DragAndDrop.AddDropHandler(OnHierarchyDrop);
            DragAndDrop.AddDropHandler(OnSceneDrop);
        }

        private static DragAndDropVisualMode OnHierarchyDrop(int dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            if (!TryGetPayload(out MirisStreamDragPayload payload))
            {
                return DragAndDropVisualMode.None;
            }

            if (perform)
            {
                SpawnMirisStreamInstance(payload, parentForDraggedObjects, worldPosition: null);
            }

            return DragAndDropVisualMode.Copy;
        }

        private static DragAndDropVisualMode OnSceneDrop(UnityEngine.Object dropUpon, Vector3 worldPosition, Vector2 viewportPosition, Transform parentForDraggedObjects, bool perform)
        {
            if (!TryGetPayload(out MirisStreamDragPayload payload))
            {
                return DragAndDropVisualMode.None;
            }

            if (perform)
            {
                SpawnMirisStreamInstance(payload, parentForDraggedObjects, worldPosition);
            }

            return DragAndDropVisualMode.Copy;
        }

        private static bool TryGetPayload(out MirisStreamDragPayload payload)
        {
            payload = DragAndDrop.GetGenericData(MirisStreamDragPayload.GenericDataKey) as MirisStreamDragPayload;
            return payload != null;
        }

        /// <summary>Instantiates a wired-up MirisStream from a dragged or picked asset.</summary>
        internal static void SpawnMirisStreamInstance(MirisStreamDragPayload payload, Transform parent, Vector3? worldPosition)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Could not find Miris Stream prefab at '{SourcePrefabPath}'.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null)
            {
                instance.transform.SetParent(parent, worldPositionStays: false);
            }
            if (worldPosition.HasValue)
            {
                instance.transform.position = worldPosition.Value;
            }

            instance.name = string.IsNullOrEmpty(payload.AssetName) ? prefab.name : payload.AssetName;

            // The prefab's serialized defaults for these fields are stale (predates the m_url ->
            // m_assetId refactor), so set them explicitly rather than trusting the prefab.
            MirisStream stream = instance.GetComponent<MirisStream>();
            if (stream != null)
            {
                stream.m_assetId = payload.AssetId;
                stream.m_streamController = payload.Controller;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Create Miris Stream");
            Selection.activeGameObject = instance;
        }
    }
}
