// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;
using Unity.Mathematics;

namespace Miris.Runtime
{
    public class GameObjectUtils
    {
        // Tries to maps a GameObject to a unique color.
        static public float4 HashGameObjectToColor(GameObject gameObject)
        {
            string gameObjectPath = GetGameObjectPath(gameObject);
            return HashStringToColor(gameObjectPath);
        }

        static public float4 HashStringToColor(string inputString)
        {
            // Hash the path string to an integer
            int hash = inputString.GetHashCode();

            // Convert hash to RGB values (0-255 range)
            // Use bitwise operations to extract color channels
            byte r = (byte)((hash >> 16) & 0xFF);  // Extract red channel
            byte g = (byte)((hash >> 8) & 0xFF);   // Extract green channel
            byte b = (byte)(hash & 0xFF);          // Extract blue channel

            // Return the color (use normalized 0-1 range for Color)
            return new float4(r / 255f, g / 255f, b / 255f, 1f);
        }

        static public string GetGameObjectPath(GameObject obj)
        {
            string path = "/" + obj.name;
            Transform current = obj.transform.parent;

            while (current != null)
            {
                path = "/" + current.name + path;
                current = current.parent;
            }

            return path;
        }
    }
}
