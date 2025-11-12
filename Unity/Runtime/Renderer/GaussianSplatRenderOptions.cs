// Copyright © 2024 Miris. All rights reserved.

// C# Standard Library
using System;
using System.Linq;

// Unity Engine
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEditor;

// Unity packages
using Unity.Profiling;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Aqua.Runtime
{

    // An optional component that can be attached to a GameObject with a Miris Stream for augmenting
    // renderer behavior
    [ExecuteInEditMode]
    public class GaussianSplatRenderOptions : MonoBehaviour
    {
        [Space(10)]
        [SerializeField]
        public GaussianSplatRenderComponent.Pipeline m_renderPipeline = GaussianSplatRenderComponent.Pipeline.Geometry;

        // ---------------------------------------------------------
        // Common Renderer Options
        // ---------------------------------------------------------

        [Header("Common Options")]

        [SerializeField]
        [Range(0.0f, 3.0f)]
        public float m_gaussianSigmaThreshold = 2.5f;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        public float m_alphaCullingThreshold = 0.06f;

        [SerializeField]
        [Range(0, 3)]
        [Tooltip("Spherical Harmonics order to use")]
        public int m_SHOrder = 0;

        // ---------------------------------------------------------
        // Geometry Renderer Specific Options
        // ---------------------------------------------------------

        [Header("Geometry Renderer Options")]
        [SerializeField]
        [Tooltip("The active draw mode")]
        public GeometryRenderer.GeometryDrawMode m_drawMode = GeometryRenderer.GeometryDrawMode.Splats;

        [SerializeField]
        [Range(0.0f, 10.0f)]
        public float m_nearClipThreshold = 0.25f;

        [SerializeField]
        public bool m_fadeLargeSplats = false;

        // ---------------------------------------------------------
        // Sorting related enums & members
        // ---------------------------------------------------------

        // Allow selection of different GPU Sorting Algorithms.
        [SerializeField] 
        private GpuSortAlgorithm m_sortAlgorithm = GpuSortAlgorithm.DeviceRadixSort;

        [SerializeField]
        [Tooltip(
            "Controls when the renderer decides to sort.  Warning: Has an impact on runtime performance.\n\n" +
            "Disabled: Do not sort at all\n" +
            "OnceOnFirstFrame: Sort only ONCE on the very first frame, and not ever after\n" +
            "FirstCameraPerNthFrame: Sort only for the first camera every Nth frame, controlled via the 'Sort Nth Frame' property\n" +
            "FirstCameraPerFrame: Sort only for the first camera on every frame\n" +
            "PerCameraPerFrame: Sort for every camera on every frame.\n"
        )]
        private GeometryRenderer.SortBehavior m_sortBehavior = GeometryRenderer.SortBehavior.FirstCameraPerFrame; // This is our optimal default

        [SerializeField]
        [Range(1, 4800)]
        [Tooltip("Number of frames before the sorting algorithm executes when Sort Behavior is set to 'One Camera Per Nth Frame'")]
        private int m_sortNthFrame = 100;

        // ---------------------------------------------------------
        // Point Renderer Specific Options
        // ---------------------------------------------------------

        [Header("Point Renderer Options")]

        [SerializeField]
        [Tooltip("Points draw mode")]
        public PointRenderer.PointDrawMode m_pointsDrawMode = PointRenderer.PointDrawMode.SplatColor;

        [SerializeField]
        [Tooltip("Points SH Axis (First Order)")]
        public PointRenderer.SHAxis m_pointsSHAxis = PointRenderer.SHAxis.X;

        [SerializeField]
        [Tooltip("Points SH Color Channel")]
        public PointRenderer.SHChannel m_pointsSHChannel = PointRenderer.SHChannel.Red;

        [SerializeField]
        [Range(1, 20)]
        [Tooltip("Points SH Flatness Percentage")]
        public int m_pointsFlatnessPercent = 2;

        // ---------------------------------------------------------
        // Unity event handling
        // ---------------------------------------------------------

        // Update is called once per frame
        public void Update()
        {
            MirisStream stream = GetComponent<MirisStream>();
            if (stream == null)
            {
                return;
            }

            if (stream.m_assetRootObjectIdToRenderComponent != null)
            {
                // TODO: Apply the changes from the serialized fields onto our render components.
            }
        }
    }
}
