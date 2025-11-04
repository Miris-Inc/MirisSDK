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
    /// <summary>
    /// GaussianSplatRenderComponent provides the Unity interface for rendering gaussian splats.
    ///
    /// Each GaussianSplatRenderComponent will register itself to GaussianSplatRenderSystem which
    /// installs the camera callbacks for performing the graphics API calls.
    /// 
    /// The component holds onto a GaussianSplatRenderer instance that implements the actual
    /// rendering functionality. 
    ///
    /// When the m_dataSource reference is updated, GPU resources will be initialized and data 
    /// is transferred to the active GaussianSplatRenderer.
    /// 
    /// - Update() is called per-frame and is used to pass any renderer specific state.
    /// - Render() is called by the GaussianSplatRenderSystem and invokes the chosen render pipeline.
    /// 
    /// TODO:
    /// 1. Get viewport framing to work
    /// 
    /// </summary>

    [ExecuteInEditMode]
    public class GaussianSplatRenderComponent : MonoBehaviour
    {
        // ... existing fields ...
        public void UpdateCompositePass(Material material)
        {
            if (m_gsRenderer != null)
                m_gsRenderer.UpdateCompositePass(material);
        }
        // Reference to source 3DGS asset data.
        [SerializeField]
        [Tooltip("The asset to render (debug aid only)")]
        public GaussianSplatAquaDataSource[] m_dataSourceComponents;
        [SerializeField]
        [HideInInspector]
        public GaussianSplatDataSource[] m_dataSources;
        private GaussianSplatDataSource[] m_prevDataSources;
        private GaussianSplatDataSource[] m_validDataSources;
        private GaussianSplatDataSource[] m_prevValidDataSources;
        // Base Renderer instance
        private GaussianSplatRenderer m_gsRenderer;
        // Debug Renderer instance
        private DebugRenderer m_debugRenderer;

        // Cached bounds
        private Bounds m_bounds = new();
        private Bounds m_physicalBounds;

        // Tracks the number of frames that has been rendered
        // in order to know when to execute the sorting algorithm.
        private int m_frameCounter = 0;

        private AquaTransform m_transform;
        public Matrix4x4 m_assetMatrix = Matrix4x4.identity;

        // ---------------------------------------------------------
        // Render Pipeline Selection
        // ---------------------------------------------------------

        public enum Pipeline : int
        {
            Geometry,
            Points
        }

        [Space(10)]
        [SerializeField]
        public Pipeline m_renderPipeline = Pipeline.Geometry;
        private Pipeline m_prevRenderPipeline;

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

        // Array of geometry renderer modes that require debug drawing
        private static GeometryRenderer.GeometryDrawMode[] m_debugDrawModes = {
            GeometryRenderer.GeometryDrawMode.SplatsWithBoundingBox,
            GeometryRenderer.GeometryDrawMode.SplatsWithBoundingLocator,
            GeometryRenderer.GeometryDrawMode.BoundingBoxOnly,
            GeometryRenderer.GeometryDrawMode.BoundingLocatorOnly
        };
        private static GeometryRenderer.GeometryDrawMode[] m_debugOnlyDrawModes = {
            GeometryRenderer.GeometryDrawMode.BoundingBoxOnly,
            GeometryRenderer.GeometryDrawMode.BoundingLocatorOnly
        };

        public int m_lodHeatMapMinLodIndex = 0;
        public int m_lodHeatMapMaxLodIndex = 5;

        [SerializeField]
        [Range(0.0f, 10.0f)]
        public float m_nearClipThreshold = 0.25f;

        [SerializeField]
        public bool m_fadeLargeSplats = false;

        // ---------------------------------------------------------
        // Sorting related enums & members
        // ---------------------------------------------------------

        // Allow selection of different GPU Sorting Algorithms.
        [SerializeField] private GpuSortAlgorithm m_sortAlgorithm = GpuSortAlgorithm.DeviceRadixSort;

        private GpuSortAlgorithm m_prevSortAlgorithm;

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
        // Profiling
        // ---------------------------------------------------------

        static string s_profilerPrefix = "[GaussianSplatRenderComponent] ";
        static readonly ProfilerMarker s_populateCommandBufferMarker = new ProfilerMarker(
            s_profilerPrefix + "Populate command buffer"
        );
        static readonly ProfilerMarker s_updateRendererMarker = new ProfilerMarker(
            s_profilerPrefix + "Update renderer"
        );
        static readonly ProfilerMarker s_updateBoundsMarker = new ProfilerMarker(
            s_profilerPrefix + "Update bounds"
        );
        static readonly ProfilerMarker s_getVisibleDataSourcesMarker = new ProfilerMarker(
            s_profilerPrefix + "Get visible data sources"
        );

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        public int GetSplatCount()
        {
            return m_gsRenderer != null ? m_gsRenderer.splatCount : 0;
        }

        public Bounds GetObjectBounds()
        {
            return m_bounds;
        }

        public Bounds GetApproximatePhysicalBounds()
        {
            return m_physicalBounds;
        }

        // Is our data source in a valid state?
        public bool IsAssetValid()
        {
            if (m_dataSources == null)
            {
                return false;
            }

            if (m_dataSources.Length == 0)
            {
                return false;
            }

            foreach (var dataSource in m_dataSources)
            {
                if (dataSource.IsValid())
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------
        // Unity event handling
        // ---------------------------------------------------------

        public void Awake()
        {
            m_prevRenderPipeline = m_renderPipeline;
        }

        // Called when this component is enabled.
        public void OnEnable()
        {
            // This will trigger the resources to be populated in the next Update() where
            // there is a valid m_dataSource.
            GaussianSplatRenderSystem.m_instance.RegisterRenderer(this);
        }

        // Called when this component is disabled.
        public void OnDisable()
        {
            GaussianSplatRenderSystem.m_instance.UnregisterRenderer(this);
            m_gsRenderer?.Dispose();
            m_gsRenderer = null;
            m_debugRenderer?.Dispose();
            m_debugRenderer = null;
        }

        // Update is called once per frame
        public void Update()
        {
            // The renderer may be asked to update when the asset is in an  
            // invalid state. If this happens just skip any further updates
            if (!IsAssetValid())
            {
                return;
            }

            m_transform = new AquaTransform(transform) * m_assetMatrix; // TODO: Maybe make this a struct so we don't keep reallocing?

            // Re-populate rendering resources when the asset or GPU sorting algorithm changes.
            m_prevValidDataSources = m_validDataSources;
#if UNITY_EDITOR
            m_validDataSources = GetVisibleDataSourcesSorted(Camera.main, performCulling: false);
#else
            m_validDataSources = GetVisibleDataSourcesSorted(Camera.main);
#endif
            if (RendererDirty())
            {
                UpdateRenderer();

                // Record the state the renderer was most recently initialized with
                m_prevSortAlgorithm = m_sortAlgorithm;
                m_prevRenderPipeline = m_renderPipeline;
            }

            // Update common renderer state
            m_gsRenderer.SetGaussianSigmaThreshold(m_gaussianSigmaThreshold);
            m_gsRenderer.SetAlphaCullingThreshold(m_alphaCullingThreshold);
            m_gsRenderer.SetSHOrder(m_SHOrder);

            // Update render pipeline specific state
            switch (m_renderPipeline)
            {
                case Pipeline.Geometry:
                    (m_gsRenderer as GeometryRenderer).m_geometryDrawMode = m_drawMode;
                    (m_gsRenderer as GeometryRenderer).SetSortBehavior(m_sortBehavior);
                    (m_gsRenderer as GeometryRenderer).m_frameCounter = m_frameCounter;
                    (m_gsRenderer as GeometryRenderer).m_sortNthFrame = m_sortNthFrame;
                    (m_gsRenderer as GeometryRenderer).SetMinMaxLodIndices(m_lodHeatMapMinLodIndex, m_lodHeatMapMaxLodIndex);
                    (m_gsRenderer as GeometryRenderer).m_nearClipThreshold = m_nearClipThreshold;
                    (m_gsRenderer as GeometryRenderer).m_fadeLargeSplats = m_fadeLargeSplats;
                    break;
                case Pipeline.Points:
                    (m_gsRenderer as PointRenderer).m_pointDrawMode = m_pointsDrawMode;
                    (m_gsRenderer as PointRenderer).m_pointSHAxis = m_pointsSHAxis;
                    (m_gsRenderer as PointRenderer).m_pointSHChannel = m_pointsSHChannel;
                    (m_gsRenderer as PointRenderer).m_flatnessPercent = m_pointsFlatnessPercent;
                    break;
                default:
                    break;
            }
        }

        // ---------------------------------------------------------
        // Resource management
        // ---------------------------------------------------------

        private bool AreDataSourcesDirty()
        {
            if (!ReferenceEquals(m_prevDataSources, m_dataSources))
            {
                return true;
            }
            if (m_prevValidDataSources.Length != m_validDataSources.Length)
            {
                return true;
            }

            for (uint dataSourceIndex = 0; dataSourceIndex < m_validDataSources.Length; ++dataSourceIndex)
            {
                if (m_validDataSources[dataSourceIndex] != m_prevValidDataSources[dataSourceIndex] ||
                    m_validDataSources[dataSourceIndex].m_dirty)
                {
                    return true;
                }
            }

            return false;
        }

        private GaussianSplatDataSource[] GetValidDataSources()
        {
            List<GaussianSplatDataSource> validDataSources = new(m_dataSources.Length);
            foreach (GaussianSplatDataSource dataSource in m_dataSources)
            {
                if (dataSource.IsValid())
                {
                    validDataSources.Add(dataSource);
                }
            }

            return validDataSources.ToArray();
        }

        private bool RendererDirty()
        {
            bool missing_renderer = (m_gsRenderer == null);
            bool pipeline_changed = (m_prevRenderPipeline != m_renderPipeline);
            bool geometry_renderer_needs_update = (m_renderPipeline == Pipeline.Geometry) && (m_prevSortAlgorithm != m_sortAlgorithm);
            return missing_renderer || pipeline_changed || geometry_renderer_needs_update || AreDataSourcesDirty();
        }

        private GaussianSplatDataSource[] GetVisibleDataSourcesSorted(Camera camera, bool performCulling=true)
        {
            s_getVisibleDataSourcesMarker.Begin();

            List<(GaussianSplatDataSource dataSource, float distance)> visibleDataSources = new();

            Plane[] localPlanes = null;

            if (performCulling)
            {
                // returned camera frustum planes are in worldspace
                Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);

                // create localspace frustum planes to optimise testing each datasource
                // since the returned bounds for these are in the assets localspace
                localPlanes = new Plane[6];
                uint planeIndex = 0;
                foreach (Plane plane in frustumPlanes)
                {
                    Vector3 planeCentreWorld = -frustumPlanes[planeIndex].normal * frustumPlanes[planeIndex].distance;
                    Vector3 planeCentreLocal = m_transform.InverseTransformPoint(planeCentreWorld);
                    Vector3 planeNormalLocal = m_transform.InverseTransformDirection(frustumPlanes[planeIndex].normal);

                    localPlanes[planeIndex] = new Plane(planeNormalLocal, planeCentreLocal);
                    planeIndex++;
                }
            }

            Vector3 camWorldPos = camera.transform.position;
            foreach (var dataSource in m_dataSources)
            {
                if (!dataSource.IsValid())
                    continue;

                Bounds dataSourceBounds = dataSource.GetObjectBounds();
                if (!performCulling || GeometryUtility.TestPlanesAABB(localPlanes, dataSourceBounds))
                {
                    float distance = Vector3.Distance(camWorldPos, m_transform.TransformPoint(dataSourceBounds.center));
                    visibleDataSources.Add((dataSource, distance));
                }
            }

            visibleDataSources.Sort((a, b) => a.distance.CompareTo(b.distance));
            GaussianSplatDataSource[] sorted = visibleDataSources.Select(x => x.dataSource).ToArray();

            s_getVisibleDataSourcesMarker.End();

            return sorted;
        }

        private void CalculatePhysicalBounds()
        {
            Vector3[] corners = BoundsUtils.BoundsGetCorners(m_bounds);
            m_physicalBounds = new Bounds(m_transform.TransformPoint(corners[0]), Vector3.zero);
            for (int cornerIndex = 1; cornerIndex < corners.Length; cornerIndex++)
            {
                m_physicalBounds.Encapsulate(m_transform.TransformPoint(corners[cornerIndex]));
            }
        }

        private void UpdateRenderer()
        {
            using (s_updateRendererMarker.Auto())
            {
                if (m_gsRenderer == null || m_prevRenderPipeline != m_renderPipeline)
                {
                    Debug.Log("Updating Gaussian Splats Renderer");

                    m_gsRenderer?.Dispose();

                    switch (m_renderPipeline)
                    {
                        case Pipeline.Geometry:
                            m_gsRenderer = new GeometryRenderer();
                            Debug.Log("  created new Geometry Renderer");
                            break;
                        case Pipeline.Points:
                            m_gsRenderer = new PointRenderer();
                            Debug.Log("  created new Point Renderer");
                            break;
                        default:
                            break;
                    }
                }

                // Track current valid visible data sources for change management.
                m_prevDataSources = m_dataSources;
                // Initialize
                m_frameCounter = 0;
                // Update resources on the active renderer
                m_gsRenderer.UpdateResources(m_validDataSources);

                Debug.Log($"'{GameObjectUtils.GetGameObjectPath(gameObject)}': updated renderer with {m_gsRenderer.splatCount} splats for {m_validDataSources.Length} visible dataSources");

                // Update render pipeline specific things
                switch (m_renderPipeline)
                {
                    case Pipeline.Geometry:
                        (m_gsRenderer as GeometryRenderer).SetSortAlgorithm(m_sortAlgorithm);
                        (m_gsRenderer as GeometryRenderer).SetObjectIdColor(GameObjectUtils.HashGameObjectToColor(this.gameObject));
                        break;
                    default:
                        break;
                }

                // Expand bounds to encapsulate all data sources.
                using (s_updateBoundsMarker.Auto())
                {
                    // compute an aggregate bounds by getting the min/max of the dataSource
                    // bounds min/max vectors. this appears to be faster than repeatedly 
                    // expanding the bounds using Bounds.Encapsulate(Bounds) for each source
                    Vector3 minBound = Vector3.positiveInfinity;
                    Vector3 maxBound = Vector3.negativeInfinity;
                    Bounds dataSourceBound = new();
                    foreach (var dataSource in m_validDataSources)
                    {
                        dataSourceBound = dataSource.GetObjectBounds();
                        minBound = Vector3.Min(minBound, dataSourceBound.min);
                        maxBound = Vector3.Max(maxBound, dataSourceBound.max);
                    }
                    m_bounds.SetMinMax(minBound, maxBound);
                }
                CalculatePhysicalBounds();
            }
        }

        // ---------------------------------------------------------
        // Rendering execution
        // ---------------------------------------------------------

        // Can this component render splats?
        public bool CanRender()
        {
            return (m_gsRenderer != null);
        }

        public void Render(Camera camera, CommandBuffer commandBuffer)
        {
            using (s_populateCommandBufferMarker.Auto())
            {
                Assert.IsTrue(CanRender());

                if (m_validDataSources.Length > 0)
                {
                    // Update data source properties once per frame (instead of once per view per frame).
                    if (m_frameCounter != Time.frameCount)
                    {
                        m_gsRenderer.UpdateDataSourceProperties(m_validDataSources);
                    }

                    // Buffer handling
                    m_gsRenderer.BuildBuffers(commandBuffer);

                    // Execute rendering
                    if (m_renderPipeline != Pipeline.Geometry ||
                        m_renderPipeline == Pipeline.Geometry && System.Array.IndexOf(m_debugOnlyDrawModes, m_drawMode) < 0)
                    {
                        m_gsRenderer.Run(commandBuffer, camera, m_transform);
                    }

                    // Perform debug rendering
                    if (m_renderPipeline == Pipeline.Geometry && System.Array.IndexOf(m_debugDrawModes, m_drawMode) >= 0)
                    {
                        DrawDebug(commandBuffer, camera);
                    }
                }
                m_frameCounter = Time.frameCount;
            }
        }
        
        public void SetFarFieldParameters(Material farFieldMaterial) {
            m_gsRenderer.SetFarFieldParameters(farFieldMaterial);
        }

        private void DrawDebug(CommandBuffer commandBuffer, Camera camera)
        {
            if (m_debugRenderer == null)
            {
                m_debugRenderer = new();
            }

            switch (m_drawMode)
            {
                case GeometryRenderer.GeometryDrawMode.SplatsWithBoundingBox:
                case GeometryRenderer.GeometryDrawMode.BoundingBoxOnly:
                    DrawDebugPrimitives(commandBuffer, DebugRenderer.PrimitiveType.Box);
                    break;
                case GeometryRenderer.GeometryDrawMode.SplatsWithBoundingLocator:
                case GeometryRenderer.GeometryDrawMode.BoundingLocatorOnly:
                    DrawDebugPrimitives(commandBuffer, DebugRenderer.PrimitiveType.Locator);
                    break;
                default:
                    break;
            }
        }

        private void DrawDebugPrimitives(CommandBuffer commandBuffer, DebugRenderer.PrimitiveType primitiveType)
        {
            foreach (GaussianSplatDataSource dataSource in m_validDataSources)
            {
                m_debugRenderer.DrawPrimitive(
                    primitiveType,
                    commandBuffer,
                    m_transform,
                    dataSource.GetObjectBounds(),
                    dataSource.GetObjectIdColor()
                );
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(m_physicalBounds.center, m_physicalBounds.size);
        }
    }
}
