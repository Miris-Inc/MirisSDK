// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Miris.Runtime
{
    // Draws Shark into each camera on its own - the editor's Scene views and the Game view - where
    // SharkSplatRenderSystem's frame path draws one camera's views together. Each camera gets its
    // own native view, sorted from that camera, rendered as the camera culls and composited by a
    // CommandBuffer on the camera, so the camera shows this frame's render.
    //
    // Every view renders at the one native size, whatever the camera's aspect: the composite maps
    // the camera's own projection onto the whole texture, so a wide Scene view samples it
    // anamorphically rather than wrongly. The size only grows, so dragging a window does not
    // rebuild it every frame.
    internal class SharkCameraViews
    {
        const CameraEvent CompositeEvent = CameraEvent.AfterForwardAlpha;

        // Steps the native size grows in, and its cap per axis.
        const int SizeGranularity = 256;
        const int MaxRenderDimension = 4096;

        class CameraView
        {
            public int Index;
            public Texture2D[] Slots;
            public int Slot;
            // Executed as the camera culls, outside its render pass - see kAquaSplatRendererEventViewBase.
            public CommandBuffer Render;
            // On the camera, compositing the slot Render wrote.
            public CommandBuffer Composite;
            public MaterialPropertyBlock Properties;
            public Matrix4x4 LastView;
            public Matrix4x4 LastProjection;
        }

        readonly Dictionary<Camera, CameraView> m_views = new();
        readonly List<Camera> m_destroyedCameras = new();
        readonly SortedSet<int> m_freeIndices = new();
        int m_nextIndex;

        int m_renderer = -1;
        int m_ringDepth;
        int m_width;
        int m_height;
        float m_renderScale;
        Mesh m_mesh;
        Material m_material;
        IntPtr m_renderEventCallback;

        // Set when a camera renders from a different view or projection than its last render.
        bool m_cameraMoved;

        readonly float[] m_viewMatrix = new float[16];
        readonly float[] m_projectionMatrix = new float[16];

        // Mirrors SharkSplatRenderSystem.OutputOpacity.
        internal float OutputOpacity = 1.0f;

        internal bool IsActive => m_renderer >= 0;

        internal void Begin(int renderer, int width, int height, float renderScale, Mesh mesh, Material material)
        {
            m_renderer = renderer;
            m_ringDepth = Mathf.Max(1, SplatRendererBridge.AquaSplatRenderer_GetRingDepth());
            m_width = width;
            m_height = height;
            m_renderScale = renderScale;
            m_mesh = mesh;
            m_material = material;
            m_renderEventCallback = SplatRendererBridge.AquaSplatRenderer_GetRenderEventCallbackPtr();
            Camera.onPreCull += OnPreCull;
        }

        // Before the native shutdown event: a CommandBuffer left on a camera would go on issuing view
        // events into a renderer that no longer exists.
        internal void End()
        {
            if (!IsActive)
            {
                return;
            }
            Camera.onPreCull -= OnPreCull;
            foreach (KeyValuePair<Camera, CameraView> entry in m_views)
            {
                if (entry.Key != null)
                {
                    entry.Key.RemoveCommandBuffer(CompositeEvent, entry.Value.Composite);
                }
                ReleaseCommandBuffers(entry.Value);
                DestroySlots(entry.Value);
            }
            m_views.Clear();
            m_freeIndices.Clear();
            m_nextIndex = 0;
            m_renderer = -1;
        }

        // Scene views come and go with their windows, so a destroyed camera's view is released and its
        // index handed to the next camera.
        internal void PruneDestroyedCameras()
        {
            if (!IsActive)
            {
                return;
            }
            m_destroyedCameras.Clear();
            foreach (Camera camera in m_views.Keys)
            {
                if (camera == null)
                {
                    m_destroyedCameras.Add(camera);
                }
            }
            foreach (Camera camera in m_destroyedCameras)
            {
                CameraView view = m_views[camera];
                m_views.Remove(camera);
                ReleaseCommandBuffers(view);
                DestroySlots(view);
                SplatRendererBridge.AquaSplatRenderer_ReleaseView(m_renderer, view.Index);
                m_freeIndices.Add(view.Index);
            }
        }

        // Whether any camera has moved since the last call.
        internal bool TakeCameraMoved()
        {
            bool moved = m_cameraMoved;
            m_cameraMoved = false;
            return moved;
        }

        // How many times `camera`'s view has rendered; -1 if it has none.
        internal int RenderCount(Camera camera)
        {
            if (!IsActive || camera == null || !m_views.TryGetValue(camera, out CameraView view))
            {
                return -1;
            }
            return SplatRendererBridge.AquaSplatRenderer_GetViewRenderCount(m_renderer, view.Index);
        }

        static bool DrawsInto(Camera camera)
        {
            return camera.cameraType == CameraType.SceneView || camera == Camera.main;
        }

        void OnPreCull(Camera camera)
        {
            if (!DrawsInto(camera) || camera.pixelWidth <= 0 || camera.pixelHeight <= 0)
            {
                return;
            }
            if (!EnsureSize(camera))
            {
                return;
            }
            if (!m_views.TryGetValue(camera, out CameraView view) && !TryAddView(camera, out view))
            {
                return;
            }
            // A resize leaves every view without surfaces.
            if (view.Slots == null && !TryCreateSlots(view))
            {
                return;
            }

            Matrix4x4 worldToCamera = camera.worldToCameraMatrix;
            Matrix4x4 projection = camera.projectionMatrix;
            if (worldToCamera != view.LastView || projection != view.LastProjection)
            {
                view.LastView = worldToCamera;
                view.LastProjection = projection;
                m_cameraMoved = true;
            }
            SharkSplatRenderSystem.ToColumnMajor(worldToCamera, m_viewMatrix);
            SharkSplatRenderSystem.ToColumnMajor(projection, m_projectionMatrix);
            SplatRendererBridge.AquaSplatRenderer_SetView(m_renderer, view.Index, m_viewMatrix, m_projectionMatrix);

            // A new slot every frame, so the render never writes the surface the previous frame's
            // composite may still be sampling.
            view.Slot = (view.Slot + 1) % m_ringDepth;
            view.Render.Clear();
            view.Render.IssuePluginEvent(m_renderEventCallback,
                                         SplatRendererBridge.ViewEventId(view.Index, view.Slot, m_ringDepth));
            Graphics.ExecuteCommandBuffer(view.Render);

            // Not until the view has rendered once: before that, its surfaces hold nothing to show.
            view.Composite.Clear();
            if (SplatRendererBridge.AquaSplatRenderer_GetViewRenderCount(m_renderer, view.Index) > 0)
            {
                view.Properties.SetTexture("_LeftTex", view.Slots[view.Slot]);
                view.Properties.SetFloat("_OutputOpacity", OutputOpacity);
                view.Composite.DrawMesh(m_mesh, Matrix4x4.identity, m_material, 0, 0, view.Properties);
            }
        }

        // Grows the native size to fit `camera`, dropping every view's surfaces for OnPreCull to
        // recreate at the new size. False if the resize failed, leaving nothing to draw this frame.
        bool EnsureSize(Camera camera)
        {
            int neededWidth = Mathf.CeilToInt(camera.pixelWidth * m_renderScale);
            int neededHeight = Mathf.CeilToInt(camera.pixelHeight * m_renderScale);
            if (neededWidth <= m_width && neededHeight <= m_height)
            {
                return true;
            }

            int width = Mathf.Min(RoundUp(Mathf.Max(m_width, neededWidth)), MaxRenderDimension);
            int height = Mathf.Min(RoundUp(Mathf.Max(m_height, neededHeight)), MaxRenderDimension);
            if (width == m_width && height == m_height)
            {
                return true;
            }
            if (SplatRendererBridge.AquaSplatRenderer_Resize(m_renderer, width, height) != 0)
            {
                return false;
            }
            m_width = width;
            m_height = height;

            // Resize released every surface. Each view makes new ones as its camera next renders, so
            // one that fails leaves only that camera without, to retry next time.
            foreach (CameraView view in m_views.Values)
            {
                DestroySlots(view);
            }
            return true;
        }

        static int RoundUp(int size)
        {
            return (size + SizeGranularity - 1) / SizeGranularity * SizeGranularity;
        }

        bool TryAddView(Camera camera, out CameraView view)
        {
            view = new CameraView
            {
                Index = m_freeIndices.Count > 0 ? m_freeIndices.Min : m_nextIndex,
                Render = new CommandBuffer { name = "SharkCameraViews render" },
                Composite = new CommandBuffer { name = "SharkCameraViews composite" },
                Properties = new MaterialPropertyBlock(),
            };
            if (!TryCreateSlots(view))
            {
                ReleaseCommandBuffers(view);
                return false;
            }

            if (!m_freeIndices.Remove(view.Index))
            {
                ++m_nextIndex;
            }
            camera.AddCommandBuffer(CompositeEvent, view.Composite);
            m_views.Add(camera, view);
            return true;
        }

        bool TryCreateSlots(CameraView view)
        {
            view.Slots = new Texture2D[m_ringDepth];
            for (int slot = 0; slot < m_ringDepth; ++slot)
            {
                if (SplatRendererBridge.AquaSplatRenderer_CreateTarget(
                        m_renderer, view.Index, slot, m_width, m_height, out IntPtr nativeTexture) < 0)
                {
                    Debug.LogError($"SharkCameraViews: failed to create a {m_width}x{m_height} surface for "
                                   + $"view {view.Index}");
                    DestroySlots(view);
                    return false;
                }
                // Not linear: the native textures are BGRA8Unorm_sRGB, as in the frame path.
                view.Slots[slot] = Texture2D.CreateExternalTexture(
                    m_width, m_height, TextureFormat.BGRA32, /*mipmap*/ false, /*linear*/ false, nativeTexture);
                view.Slots[slot].hideFlags = HideFlags.HideAndDontSave;
            }
            return true;
        }

        static void ReleaseCommandBuffers(CameraView view)
        {
            view.Render.Release();
            view.Composite.Release();
        }

        static void DestroySlots(CameraView view)
        {
            if (view.Slots == null)
            {
                return;
            }
            foreach (Texture2D slot in view.Slots)
            {
                if (slot != null)
                {
                    UnityEngine.Object.DestroyImmediate(slot);
                }
            }
            view.Slots = null;
        }
    }
}
