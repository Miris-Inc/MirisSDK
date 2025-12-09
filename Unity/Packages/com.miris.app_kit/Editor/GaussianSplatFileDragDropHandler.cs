// Copyright © 2025 Miris, Inc. All rights reserved.

//  Standard library
using System.IO;
using System.Collections.Generic;

// Unity engine
using UnityEngine;
using UnityEditor;

// Aqua package
using Miris.Runtime;

namespace Miris.Editor
{
    [InitializeOnLoad]
    public class GaussianSplatFileDragDropHandler
    {
        private static HashSet<string> s_supportedFileExtensions = new HashSet<string>{
            ".drop",
            ".ply",
        };

        static GaussianSplatFileDragDropHandler()
        {
            SceneView.duringSceneGui += OnSceneGUI;  // Register the callback
        }

        // This handles the drag and drop event in the scene
        private static void OnSceneGUI(SceneView sceneView)
        {
            Event evt = Event.current;

            if (evt.type == EventType.DragUpdated)
            {
                if (AreGaussianSplatFilePaths())
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                }
            }
            else if (evt.type == EventType.DragPerform)
            {
                if (!AreGaussianSplatFilePaths())
                {
                    return;
                }

                string[] draggedFiles = DragAndDrop.paths;

                // The dragged item(s) are not gaussian splat assets.
                if (draggedFiles.Length == 0)
                {
                    return;
                }

                Vector3 spawnLocation;
                if (!MouseIntersectsGroundPlane(evt.mousePosition, out spawnLocation))
                {
                    bool intersectsCameraPlane = MouseIntersectsCameraPlane(
                        evt.mousePosition,
                        5,
                        out spawnLocation
                    );

                    // Because the camera plane is parallel to camera near/far plane, the mouse must intersect.
                    Debug.Assert(intersectsCameraPlane);
                }

                // Spawn all objects for each asset.
                DragAndDrop.AcceptDrag();
                foreach (string filePath in draggedFiles)
                {
                    SpawnRenderableObject(filePath, spawnLocation);
                }
                evt.Use();
            }
        }

        private static bool AreGaussianSplatFilePaths()
        {
            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
            {
                foreach (string path in DragAndDrop.paths)
                {
                    Debug.Log(path);
                    if (!s_supportedFileExtensions.Contains(Path.GetExtension(path)))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        private static bool MouseIntersectsGroundPlane(Vector2 mousePosition, out Vector3 intersectLocation)
        {
            // Intersect against the world scene ground plane.
            Plane groundPlane = new(Vector3.up, Vector3.zero);
            return MouseIntersectPlane(mousePosition, groundPlane, out intersectLocation);
        }

        private static bool MouseIntersectsCameraPlane(Vector2 mousePosition, float distance, out Vector3 intersectLocation)
        {
            // Create a plane parallel to camera plane at a distance.
            Ray cameraRay = new(Camera.current.transform.position, Camera.current.transform.forward);
            Vector3 cameraRayCast = cameraRay.GetPoint(distance);
            Plane cameraPlane = new(Camera.current.transform.forward, cameraRayCast);
            return MouseIntersectPlane(mousePosition, cameraPlane, out intersectLocation);
        }

        private static bool MouseIntersectPlane(Vector2 mousePosition, Plane plane, out Vector3 intersectLocation)
        {
            float distance;
            intersectLocation = new(0, 0, 0);
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (plane.Raycast(ray, out distance))
            {
                intersectLocation = ray.GetPoint(distance);
                return true;
            }

            return false;
        }

        private static GameObject SpawnRenderableObject(string filePath, Vector3 location)
        {
            string assetName = Path.GetFileName(filePath);

            MirisStreamController streamController = GameObject.FindFirstObjectByType<MirisStreamController>();
            if (streamController == null)
            {
                // Create a controller if one doesn't exist in the scene.
                GameObject controllerObject = new GameObject("Miris Stream Controller");
                streamController = controllerObject.AddComponent<MirisStreamController>();
            }

            // Instantiate a new GameObject at location
            GameObject newObj = new GameObject(assetName);
            newObj.transform.position = location;

            // Add the asset data source component.
            MirisStream stream = newObj.AddComponent<MirisStream>();
            // TODO: Replace with asset Id; OR get rid of this file entirely, not sure if it's still relevant
            //stream.m_url = filePath;

            //Debug.Log($"Spawned {filePath} in scene as: {newObj.name}");

            return newObj;
        }
    }
}
