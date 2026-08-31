// Copyright © 2026 Miris, Inc. All rights reserved.

// C# Standard Library
using System;
using System.Linq;
using System.Collections.Generic;

// Unity Engine
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using Unity.Mathematics;


#if UNITY_EDITOR
using UnityEditor;
#endif

// Unity packages
using Unity.Profiling;

namespace Miris.Runtime
{

    public class GaussianSplatRenderComponent : IDisposable
    {
        private enum CullingState : int
        {
            Enabled = 0,
            Disabled, 
            Paused
        }

        // ... existing fields ...
        public void UpdateCompositePass(Material material)
        {
            if (m_gsRenderer != null)
                m_gsRenderer.UpdateCompositePass(material);
        }
        // Reference to source 3DGS asset data.
        public GaussianSplatDataSource[] m_dataSources;
        private GaussianSplatDataSource[] m_prevDataSources;
        private GaussianSplatDataSource[] m_validDataSources;
        private GaussianSplatDataSource[] m_prevValidDataSources;
        // Base Renderer instance
        private GaussianSplatRenderer m_gsRenderer;
        // Debug Renderer instance
        private DebugRenderer m_debugRenderer;

        // Cached bounds
        private Bounds m_dataSourceBounds = new();
        private Bounds m_dataWorldBounds = new();
        private Bounds m_objectBounds = new();
        private Bounds m_worldBounds = new();

        // Tracks the number of frames that has been rendered
        // in order to know when to execute the sorting algorithm.
        private int m_frameCounter = 0;

        public MirisTransform m_transform;
        public Matrix4x4 m_assetMatrix = Matrix4x4.identity;

        private CullingState m_cullingState = CullingState.Enabled;
        private Plane[] m_cullingPlanes = null;

        // ---------------------------------------------------------
        // Render Pipeline Selection
        // ---------------------------------------------------------

        public enum Pipeline : int
        {
            Geometry,
            Points
        }

        public Pipeline m_renderPipeline = Pipeline.Geometry;
        private Pipeline m_prevRenderPipeline = Pipeline.Geometry;

        // ---------------------------------------------------------
        // Common Renderer Options
        // ---------------------------------------------------------
        public float m_gaussianSigmaThreshold = 3.0f;

        public float m_alphaCullingThreshold = 0.002f;
        public int m_SHOrder = 3;

        // ---------------------------------------------------------
        // Geometry Renderer Specific Options
        // ---------------------------------------------------------
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
        
        public const int DefaultLodHeatMapMinLodIndex = 0;
        public const int DefaultLodHeatMapMaxLodIndex = 4;

        public int m_lodHeatMapMinLodIndex = DefaultLodHeatMapMinLodIndex;
        public int m_lodHeatMapMaxLodIndex = DefaultLodHeatMapMaxLodIndex;

        public float m_nearClipThreshold = 0.25f;
        public bool m_fadeLargeSplats = false;

        // ---------------------------------------------------------
        // Sorting related enums & members
        // ---------------------------------------------------------

        // Allow selection of different GPU Sorting Algorithms.
        private GpuSortAlgorithm m_sortAlgorithm = GpuSortAlgorithm.DeviceRadixSort;

        private GpuSortAlgorithm m_prevSortAlgorithm;

        public GeometryRenderer.SortBehavior m_sortBehavior = GeometryRenderer.SortBehavior.FirstCameraPerFrame; // This is our optimal default

        public int m_sortNthFrame = 100;

        // ---------------------------------------------------------
        // Point Renderer Specific Options
        // ---------------------------------------------------------

        public PointRenderer.PointDrawMode m_pointsDrawMode = PointRenderer.PointDrawMode.SplatColor;
        public PointRenderer.SHAxis m_pointsSHAxis = PointRenderer.SHAxis.X;
        public PointRenderer.SHChannel m_pointsSHChannel = PointRenderer.SHChannel.Red;
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

        // Set at construction, by whoever owns the stream. True when something other than
        // GaussianSplatRenderSystem draws these splats - Shark, through SplatRenderer - in which case
        // this component must do neither of the two things it otherwise does: register with the
        // system (which would draw the same splats a second time) or build its GPU renderer (which
        // would cost VRAM for an asset already resident in the other renderer).

        private bool m_suppressRendering;

        // This is the data source array bounds were last computed from -
        // on the suppressed path only.
        private GaussianSplatDataSource[] m_prevBoundsDataSources;

        // Called when this component is enabled.
        public GaussianSplatRenderComponent(bool suppressRendering = false)
        {
            m_suppressRendering = suppressRendering;
            if (!m_suppressRendering)
            {
                GaussianSplatRenderSystem.m_instance.RegisterRenderer(this);
            }
        }

        // Changes who draws these splats, for a component that already exists.
        internal void SetRenderingSuppressed(bool suppressed)
        {
            if (m_suppressRendering == suppressed)
            {
                return;
            }
            m_suppressRendering = suppressed;

            if (suppressed)
            {
                GaussianSplatRenderSystem.m_instance.UnregisterRenderer(this);
            }
            else
            {
                GaussianSplatRenderSystem.m_instance.RegisterRenderer(this);
            }
        }

        // Called when this component is disabled.
        public void Dispose()
        {
            GaussianSplatRenderSystem.m_instance.UnregisterRenderer(this);
            m_gsRenderer?.Dispose();
            m_gsRenderer = null;
            m_debugRenderer?.Dispose();
            m_debugRenderer = null;
            m_objectBounds = new();
            m_worldBounds = new();   
            m_dataSourceBounds = new();
            m_dataWorldBounds = new();
        }

        public int GetSplatCount()
        {
            return m_gsRenderer != null ? m_gsRenderer.splatCount : 0;
        }

        public Bounds GetObjectBounds()
        {
            return m_objectBounds;
        }

        public Bounds GetWorldBounds()
        {
            return m_worldBounds;
        }

        public void ToggleCulling() {
            if(m_cullingState == CullingState.Enabled)
            {
                m_cullingState = CullingState.Paused;
            } else {
                m_cullingState = CullingState.Enabled;
            }
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

        private CullingState DetermineCullingState()
        {
            // default to current culling state
            CullingState cullingState = m_cullingState;
            #if UNITY_EDITOR 
            // Disable Culling if Editor && Editor Scene View in use 
            if(SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.hasFocus){
                cullingState = CullingState.Disabled;
            }
            #else 
            // always enable culling if not in the Unity Editor
            cullingState = CullingState.Enabled;
            #endif
            return cullingState;
        }

        public void Update(Transform transform)
        {
            // The renderer may be asked to update when the asset is in an  
            // invalid state. If this happens just skip any further updates
            if (!IsAssetValid())
            {
                return;
            }

            m_transform = new MirisTransform(transform) * m_assetMatrix; // TODO: Maybe make this a struct so we don't keep reallocing?

            if (m_suppressRendering)
            {
                // Everything past here either allocates the renderer or configures it, and
                // configuring a renderer that was never allocated is how this used to throw. The
                // transform above is kept up to date because it costs nothing and an external
                // renderer may still want to read it.
                //
                // We still need to keep bounds up to date.
                if (!ReferenceEquals(m_prevBoundsDataSources, m_dataSources))
                {
                    m_prevBoundsDataSources = m_dataSources;
                    UpdateBounds(m_dataSources, m_dataSources);
                }
                return;
            }

            // Re-populate rendering resources when the asset or GPU sorting algorithm changes.
            m_prevValidDataSources = m_validDataSources;

            m_validDataSources = GetVisibleDataSourcesSorted(Camera.main, cullingState: DetermineCullingState());

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

        private bool RendererDirty()
        {
            bool missing_renderer = (m_gsRenderer == null);
            bool pipeline_changed = (m_prevRenderPipeline != m_renderPipeline);
            bool geometry_renderer_needs_update = (m_renderPipeline == Pipeline.Geometry) && (m_prevSortAlgorithm != m_sortAlgorithm);
            return missing_renderer || pipeline_changed || geometry_renderer_needs_update || AreDataSourcesDirty();
        }

        private GaussianSplatDataSource[] GetVisibleDataSourcesSorted(Camera camera, CullingState cullingState=CullingState.Enabled)
        {
            s_getVisibleDataSourcesMarker.Begin();

            List<(GaussianSplatDataSource dataSource, float distance)> visibleDataSources = new();

            if (cullingState == CullingState.Enabled)
            {
                m_cullingPlanes = null;
                // returned camera frustum planes are in worldspace
                Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);

                // create localspace frustum planes to optimise testing each datasource
                // since the returned bounds for these are in the assets localspace
                m_cullingPlanes = new Plane[6];
                uint planeIndex = 0;
                foreach (Plane plane in frustumPlanes)
                {
                    Vector3 planeCentreWorld = -frustumPlanes[planeIndex].normal * frustumPlanes[planeIndex].distance;
                    Vector3 planeCentreLocal = m_transform.InverseTransformPoint(planeCentreWorld);
                    Vector3 planeNormalLocal = m_transform.InverseTransformDirection(frustumPlanes[planeIndex].normal);

                    m_cullingPlanes[planeIndex] = new Plane(planeNormalLocal, planeCentreLocal);
                    planeIndex++;
                }
            }

            Vector3 camWorldPos = camera.transform.position;
            foreach (var dataSource in m_dataSources)
            {
                // Filter out invalid or inactive data sources.
                if (!dataSource.IsValid() || !dataSource.m_active)
                {
                    continue;
                }

                Bounds dataSourceBounds = dataSource.GetObjectBounds();
                if (cullingState == CullingState.Disabled || GeometryUtility.TestPlanesAABB(m_cullingPlanes, dataSourceBounds))
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

        private void CalculateObjectAndWorldBounds()
        {
            Vector3[] corners = BoundsUtils.BoundsGetCorners(m_dataSourceBounds);
            Vector3[] worldCorners = BoundsUtils.BoundsGetCorners(m_dataWorldBounds);

            // Calculate object space bounds 
            m_objectBounds = new Bounds(m_assetMatrix.MultiplyPoint3x4(corners[0]), Vector3.zero);
            for (int cornerIndex = 1; cornerIndex < corners.Length; cornerIndex++)
            {
                m_objectBounds.Encapsulate(m_assetMatrix.MultiplyPoint3x4(corners[cornerIndex]));
            }

            // Calculate world space bounds
            m_worldBounds = new Bounds(m_transform.TransformPoint(worldCorners[0]), Vector3.zero);
            for (int cornerIndex = 1; cornerIndex < worldCorners.Length; cornerIndex++)
            {
                m_worldBounds.Encapsulate(m_transform.TransformPoint(worldCorners[cornerIndex]));
            }
        }

        private void UpdateRenderer()
        {
            using (s_updateRendererMarker.Auto())
            {
                if (m_gsRenderer == null || m_prevRenderPipeline != m_renderPipeline)
                {
                    MirisDebug.Log("Updating Gaussian Splats Renderer");

                    m_gsRenderer?.Dispose();

                    switch (m_renderPipeline)
                    {
                        case Pipeline.Geometry:
                            m_gsRenderer = new GeometryRenderer();
                            MirisDebug.Log("  created new Geometry Renderer");
                            break;
                        case Pipeline.Points:
                            m_gsRenderer = new PointRenderer();
                            MirisDebug.Log("  created new Point Renderer");
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

                // Set SH order based on available coefficients in the loaded data
                m_SHOrder = m_gsRenderer.GetMaxSHOrder();
                
                // Update render pipeline specific things
                switch (m_renderPipeline)
                {
                    case Pipeline.Geometry:
                        (m_gsRenderer as GeometryRenderer).SetSortAlgorithm(m_sortAlgorithm);
                        break;
                    default:
                        break;
                }

                // Expand bounds to encapsulate all data sources.
                UpdateBounds(m_validDataSources, m_dataSources);
            }
        }

        // Aggregate bounds over the given data sources, then derive the object- and world-space
        // bounds from them.
        private void UpdateBounds(GaussianSplatDataSource[] objectSources, GaussianSplatDataSource[] worldSources)
        {
            using (s_updateBoundsMarker.Auto())
            {
                // compute an aggregate bounds by getting the min/max of the dataSource
                // bounds min/max vectors. this appears to be faster than repeatedly 
                // expanding the bounds using Bounds.Encapsulate(Bounds) for each source
                Vector3 minBound = Vector3.positiveInfinity;
                Vector3 maxBound = Vector3.negativeInfinity;
                foreach (var dataSource in objectSources)
                {
                    Bounds dataSourceBounds = dataSource.GetObjectBounds();
                    minBound = Vector3.Min(minBound, dataSourceBounds.min);
                    maxBound = Vector3.Max(maxBound, dataSourceBounds.max);
                }
                m_dataSourceBounds.SetMinMax(minBound, maxBound);

                Vector3 minWorldBound = Vector3.positiveInfinity;
                Vector3 maxWorldBound = Vector3.negativeInfinity;
                foreach (var dataSource in worldSources)
                {
                    Bounds dataSourceBounds = dataSource.GetObjectBounds();
                    minWorldBound = Vector3.Min(minWorldBound, dataSourceBounds.min);
                    maxWorldBound = Vector3.Max(maxWorldBound, dataSourceBounds.max);
                }
                m_dataWorldBounds.SetMinMax(minWorldBound, maxWorldBound);

                CalculateObjectAndWorldBounds();
            }
        }

        // ---------------------------------------------------------
        // Rendering execution
        // ---------------------------------------------------------

        // Can this component render splats? Both halves matter: the renderer is constructed before
        // its buffers are allocated, and UpdateResources allocates nothing at all while the data
        // sources carry no splats - which is every frame between a model root appearing and its
        // splat data arriving. Answering "yes" in that window put a renderer with null buffers into
        // the render system's active list, and every buffer it touched was a NullReferenceException.
        public bool CanRender()
        {
            return m_gsRenderer != null && m_gsRenderer.HasResources;
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
                    MapLodIndexToColor(dataSource.GetLodIndex())
                );
            }
        }

        private float4 MapLodIndexToColor(int lodIndex)
        {
            return ColorUtils.LodIndexToRgba(lodIndex, m_lodHeatMapMinLodIndex, m_lodHeatMapMaxLodIndex);
        }
    }
}
