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
#if UNITY_6000_3_OR_NEWER
            DragAndDrop.AddDropHandlerV2(OnHierarchyDrop);
            DragAndDrop.AddDropHandlerV2(OnSceneDrop);
#else
            DragAndDrop.AddDropHandler(OnHierarchyDrop);
            DragAndDrop.AddDropHandler(OnSceneDrop);
#endif
        }

#if UNITY_6000_3_OR_NEWER
        private static DragAndDropVisualMode OnHierarchyDrop(EntityId dropTargetEntityId, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
#else
        private static DragAndDropVisualMode OnHierarchyDrop(int dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
#endif
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

            // The prefab's serialized default for this field is stale (predates the m_url ->
            // m_assetId refactor), so set it explicitly rather than trusting the prefab.
            MirisStream stream = instance.GetComponent<MirisStream>();
            if (stream != null)
            {
                stream.m_assetId = payload.AssetId;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Create Miris Stream");
            Selection.activeGameObject = instance;
        }
    }
}
