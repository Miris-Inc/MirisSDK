// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Miris.Runtime.Experimental
{

    // Renders a MirisStreamController's streams with the native Shark splat renderer instead of the
    // SDK's own, and composites what it produces over the camera.
    public class SplatRenderer : MonoBehaviour
    {
        const string CompositeShaderResource = "Shaders/SplatComposite";
        const int DefaultMaxSplats = 2 * 1024 * 1024;

        // Whether the composite flips V when sampling the surfaces - a fact about Metal's, Dawn's
        // and Unity's texture conventions stacked on each other. NOT the framebuffer-orientation
        // flip, which the shader handles unconditionally via _ProjectionParams.x; if the image is
        // upside down and changing this does not fix it, suspect that.
        const bool CompositeVFlip = true;

        // Fraction of the drawable to rasterize. 1 is native; lower trades sharpness for fill cost.
        //
        // Change this in the SCENE, not here: Unity deserializes the scene's value over this
        // initializer, which only applies to a component with no stored value.
        [SerializeField, Range(0.1f, 1.0f)] float m_renderScale = 1.0f;

        [SerializeField] int m_maxSplats = DefaultMaxSplats;

        // The client Shark attaches to rather than creating a second one of. Required: the streams
        // under this controller are what decide which assets load, and from where.
        [SerializeField] MirisStreamController m_streamController;

        // Every MirisStream pointing at that controller, found once at adoption. Discovered rather
        // than serialized so a stream can never be left on the SDK's renderer by being forgotten in
        // a list.
        MirisStream[] m_sdkStreams = Array.Empty<MirisStream>();

        // True once native has accepted a drained batch. Only used to keep the rejection error to
        // one line rather than one per frame.
        bool m_changesAccepted = false;

        // True once the streams have been enabled - see StartSdkStreamWhenAdopted.
        bool m_streamStarted = false;
        int m_adoptWaitFrames = 0;

        // Scratch for the model transform push, and the last one sent. Compared rather than pushed
        // blindly so a stationary asset costs nothing per frame.
        readonly float[] m_modelMatrix = new float[16];
        readonly float[] m_modelMatrixInverse = new float[16];
        // Last matrix pushed per ModelRoot
        readonly Dictionary<int, Matrix4x4> m_lastModelMatrices = new();

        // Two in stereo, because the views are the eyes; one otherwise.
        int m_viewCount = 1;

        // Surfaces per view, read from native in TryInitialize. Not a constant here - native owns
        // the ring, and if the two disagree the composite reads a surface Shark is writing, which
        // tears intermittently rather than failing.
        int m_ringDepth = 1;

        // The camera the compositor presents, and the source of the per-view matrices. In stereo
        // the two views are its two eyes, which are matrices on one camera rather than two cameras.
        Camera m_camera;
        bool m_isStereo;

        // The size Shark actually rasterizes into, resolved once in Update before anything is
        // created.
        int m_renderWidth;
        int m_renderHeight;
        int m_renderSizeWaitFrames;

        int m_renderer = -1;
        Texture2D[] m_sharedTextures;
        CommandBuffer m_commandBuffer;
        GameObject m_quad;
        Material m_quadMaterial;

        // Reused every frame so StageViews does not allocate at frame rate.
        readonly float[] m_viewMatrix = new float[16];
        readonly float[] m_projectionMatrix = new float[16];

        // Unity's Matrix4x4 is column-major but its mNN fields are named row-first, and the
        // shared ValueConversion.MatrixToFloatArray emits ROW-major - which is not what
        // shark_set_view_proj takes. Hence a local flatten rather than that helper.
        static void ToColumnMajor(Matrix4x4 matrix, float[] result)
        {
            for (int column = 0; column < 4; ++column)
            {
                Vector4 columnVector = matrix.GetColumn(column);
                result[4 * column + 0] = columnVector.x;
                result[4 * column + 1] = columnVector.y;
                result[4 * column + 2] = columnVector.z;
                result[4 * column + 3] = columnVector.w;
            }
        }

        void Start()
        {
            if (m_streamController == null)
            {
                Debug.LogError("SplatRenderer: no MirisStreamController assigned - there is no client to "
                               + "adopt and nothing would be rendered");
                enabled = false;
                return;
            }

            m_camera = Camera.main;
            if (m_camera == null)
            {
                Debug.LogError("SplatRenderer: no camera to render from - tag one MainCamera");
                enabled = false;
                return;
            }

            // Both guards are against the stored value, not the initializer: a scene serialized
            // against an older field set deserializes a zero over it, and [Range] constrains only
            // what the Inspector will let you type.
            if (m_maxSplats <= 0)
            {
                m_maxSplats = DefaultMaxSplats;
            }
            m_renderScale = Mathf.Clamp(m_renderScale <= 0.0f ? 1.0f : m_renderScale, 0.1f, 1.0f);

            m_isStereo = XRUtils.IsXR();
            m_viewCount = m_isStereo ? 2 : 1;

            Debug.Log($"SplatRenderer: starting - SDK-driven via '{m_streamController.name}', "
                      + $"{(m_isStereo ? "stereo" : "mono")}, {m_viewCount} view(s). Asset ids and server "
                      + "come from the MirisStreams.");

            // Everything past this point needs the render size, and in XR that is the
            // compositor's to give - see TryInitialize.
        }

        // Nothing here can run until the render size is known, which in XR means waiting for XR to
        // come up: Shark's resolution is fixed at shark_create, so there is no resize short of
        // rebuilding the render state mid-stream.
        void Update()
        {
            if (m_renderer >= 0)
            {
                StartSdkStreamWhenAdopted();
                PushModelTransforms();
                return;
            }

            if (!ResolveRenderSize(out m_renderWidth, out m_renderHeight))
            {
                // Not an error for the first few frames, but waiting forever is silent and looks
                // exactly like a stream that never arrived.
                if (++m_renderSizeWaitFrames % 120 == 0)
                {
                    Debug.LogWarning($"SplatRenderer: still waiting for a render size after "
                                     + $"{m_renderSizeWaitFrames} frames - nothing has been created yet");
                }
                return;
            }

            TryInitialize();
        }

        // The size Shark rasterizes into. In XR that is the compositor's per-eye drawable
        // (1888x1792 on Vision Pro): rendering under it and letting the compositor upscale is the
        // single largest sharpness loss in the path.
        bool ResolveRenderSize(out int width, out int height)
        {
            int drawableWidth = m_isStereo ? XRSettings.eyeTextureWidth : m_camera.pixelWidth;
            int drawableHeight = m_isStereo ? XRSettings.eyeTextureHeight : m_camera.pixelHeight;

            if (drawableWidth <= 0 || drawableHeight <= 0)
            {
                width = 0;
                height = 0;
                return false;
            }

            width = Mathf.Max(1, Mathf.RoundToInt(drawableWidth * m_renderScale));
            height = Mathf.Max(1, Mathf.RoundToInt(drawableHeight * m_renderScale));
            return true;
        }

        // Attaches native to the controller's client and subscribes to its drained changes. Adopt,
        // subscribe, and only then ask for the asset: the SDK's drain is destructive, so a batch
        // that lands before native has a context is gone for good.
        bool AdoptControllerClient()
        {
            Client client = m_streamController.GetClient();
            IntPtr handle = client != null ? (client.GetClientHandleInternal() ?? IntPtr.Zero) : IntPtr.Zero;
            if (handle == IntPtr.Zero)
            {
                Debug.LogError("SplatRenderer: the stream controller has no live client to adopt - is it "
                               + "initialized? MirisStreamController.Start runs before this only if its "
                               + "script execution order is earlier");
                return false;
            }

            m_sdkStreams = FindStreamsForController();
            if (m_sdkStreams.Length == 0)
            {
                Debug.LogError("SplatRenderer: no MirisStream in the scene points at "
                               + $"'{m_streamController.name}' - nothing would be streamed");
                return false;
            }

            if (SplatRendererBridge.AquaSplatRenderer_AdoptClient(
                    m_renderer, handle, m_sdkStreams[0].m_assetId, m_maxSplats) != 0)
            {
                Debug.LogError("SplatRenderer: the native renderer rejected the client handle");
                return false;
            }

            // After acceptance, never before: a rejection would otherwise leave the streams
            // suppressed with Shark not running, and neither renderer would draw. Still ahead of any
            // loading, which is what the flag needs - each render component reads it at construction.
            SetStreamsRenderedExternally(true);

            // Adoption is deferred to the render thread, so it has not happened yet - hence the flag
            // being set by the first drain that native accepts rather than here.
            m_streamController.SceneChangesDrained += OnSceneChangesDrained;
            Debug.Log($"SplatRenderer: adopting the SDK's client for {m_sdkStreams.Length} stream(s) - the "
                      + "controller owns streaming, camera and frame pacing from here");
            return true;
        }

        // Who draws these streams' splats. True while Shark is rendering them; restored on teardown,
        // because a suppressed stream is invisible to the SDK's renderer too.
        void SetStreamsRenderedExternally(bool renderedExternally)
        {
            foreach (MirisStream stream in m_sdkStreams)
            {
                if (stream != null)
                {
                    stream.RenderedExternally = renderedExternally;
                }
            }
        }

        // Every MirisStream in the scene whose controller is ours, disabled ones included.
        MirisStream[] FindStreamsForController()
        {
            MirisStream[] all = FindObjectsByType<MirisStream>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<MirisStream> mine = new List<MirisStream>();
            foreach (MirisStream stream in all)
            {
                if (stream != null && stream.m_streamController == m_streamController)
                {
                    mine.Add(stream);
                }
            }
            return mine.ToArray();
        }

        // Keeps Shark's copy of the stream's transform current, so moving the MirisStream GameObject
        // moves its splats. The inverse goes with it because the true-perspective projection
        // transforms a quadratic form and WGSL has no inverse() builtin - so it is computed once
        // here rather than per splat there.
        void PushModelTransforms()
        {
            int liveModelRoots = 0;
            foreach (MirisStream stream in m_sdkStreams)
            {
                if (stream == null)
                {
                    continue;
                }

                Matrix4x4 streamToWorld = stream.transform.localToWorldMatrix;
                foreach (KeyValuePair<int, MirisAssetRenderComponent> modelRoot in stream.ModelRoots)
                {
                    ++liveModelRoots;
                    MirisAssetRenderComponent renderComponent = modelRoot.Value;
                    if (renderComponent == null)
                    {
                        continue;
                    }

                    // In order to match MirisAssetRenderComponent.Update (the old Unity renderer),
                    // we need to account both for the stream's Unity transform AND the ModelRoot's
                    // placement within the scene (e.g. it's own transform composed with the parent's
                    // spawn offset).
                    Matrix4x4 model = streamToWorld * renderComponent.m_assetMatrix;
                    if (m_lastModelMatrices.TryGetValue(modelRoot.Key, out Matrix4x4 previous)
                        && (previous == model))
                    {
                        continue;
                    }
                    m_lastModelMatrices[modelRoot.Key] = model;

                    ToColumnMajor(model, m_modelMatrix);
                    ToColumnMajor(model.inverse, m_modelMatrixInverse);
                    SplatRendererBridge.AquaSplatRenderer_SetModelTransform(
                        m_renderer, modelRoot.Key, m_modelMatrix, m_modelMatrixInverse);
                }
            }

            // If there's an asset swap, we can safely just clear the cache
            if (m_lastModelMatrices.Count > liveModelRoots)
            {
                m_lastModelMatrices.Clear();
            }
        }

        // Lets the assets go, once native confirms it holds the controller's client. MirisStream
        // adds its asset in OnEnable, frames before this component finishes initializing, and
        // AdoptClient only takes effect on the next render event - so holding the streams disabled
        // until adoption is confirmed removes that window rather than narrowing it.
        void StartSdkStreamWhenAdopted()
        {
            if (m_streamStarted)
            {
                return;
            }

            if (SplatRendererBridge.AquaSplatRenderer_IsClientAdopted(m_renderer) != 1)
            {
                // Adoption happens on the next render event, so a frame or two is normal. Waiting
                // forever is not, and looks exactly like an asset that never streamed.
                if (++m_adoptWaitFrames % 120 == 0)
                {
                    Debug.LogWarning($"SplatRenderer: still waiting for the client to be adopted after "
                                     + $"{m_adoptWaitFrames} frames - the stream has not been started");
                }
                return;
            }

            m_streamStarted = true;

            foreach (MirisStream stream in m_sdkStreams)
            {
                if (stream.enabled)
                {
                    // Already running, so it added its asset before adoption: whatever activated in
                    // the meantime is missing from Shark. Say so rather than rendering a partial set.
                    Debug.LogWarning($"SplatRenderer: MirisStream '{stream.name}' was already enabled, so "
                                     + "it added its asset before Shark adopted the client - objects "
                                     + "activated before now are missing. Ship MirisStream components "
                                     + "disabled and let this component enable them.");
                    continue;
                }
                stream.enabled = true;
            }

            Debug.Log($"SplatRenderer: client adopted after {m_adoptWaitFrames} frames - started "
                      + $"{m_sdkStreams.Length} stream(s)");
        }

        // Called from inside MirisStreamController's drain scope: scene lock held, on its thread,
        // with arrays that are freed the moment it returns. Everything this does has to be
        // synchronous, and native must not defer it to the render thread.
        void OnSceneChangesDrained(SceneChangeTracker.Changes changes)
        {
            if (m_renderer < 0)
            {
                return;
            }

            // The controller drains every LateUpdate whether or not the scene changed, so most
            // batches are empty. Skipping those keeps a P/Invoke and a native lock off every frame,
            // and reserves the native "no context yet" warning for batches that really lose something.
            if (changes.m_changeIds.m_createdObjectsCount == 0
                && changes.m_changeIds.m_activatedObjectsCount == 0
                && changes.m_changeIds.m_deactivatedObjectsCount == 0
                && changes.m_changeIds.m_remaskedObjectsCount == 0
                && changes.m_changeIds.m_deletedObjectsCount == 0)
            {
                return;
            }
            int applied = SplatRendererBridge.AquaSplatRenderer_ApplySceneChanges(m_renderer, ref changes.m_changeIds);
            if (applied < 0)
            {
                // -1 is the argument/ownership rejection, not "nothing to do" - worth saying once.
                if (m_changesAccepted)
                {
                    Debug.LogError("SplatRenderer: native rejected the drained scene changes");
                    m_changesAccepted = false;
                }
                return;
            }
            m_changesAccepted = true;
        }

        void TryInitialize()
        {
            Debug.Log($"SplatRenderer: initializing at {m_renderWidth}x{m_renderHeight}"
                      + $" (scale {m_renderScale}, waited {m_renderSizeWaitFrames} frames)");

            m_renderer = SplatRendererBridge.AquaSplatRenderer_Create(m_renderWidth, m_renderHeight);
            if (m_renderer < 0)
            {
                Debug.LogError("SplatRenderer: failed to create the native renderer");
                enabled = false;
                return;
            }

            m_ringDepth = Mathf.Max(1, SplatRendererBridge.AquaSplatRenderer_GetRingDepth());
            m_sharedTextures = new Texture2D[m_viewCount * m_ringDepth];
            Debug.Log($"SplatRenderer: ring depth {m_ringDepth}, {m_viewCount * m_ringDepth} surfaces");

            for (int view = 0; view < m_viewCount; ++view)
            {
                for (int slot = 0; slot < m_ringDepth; ++slot)
                {
                    if (SplatRendererBridge.AquaSplatRenderer_CreateTarget(
                            m_renderer, view, slot, m_renderWidth, m_renderHeight,
                            out IntPtr sharedNativeTexture) < 0)
                    {
                        Debug.LogError("SplatRenderer: failed to create a shared texture (is the macOS graphics API set to Metal?)");
                        Teardown();
                        enabled = false;
                        return;
                    }
                    m_sharedTextures[view * m_ringDepth + slot] = Texture2D.CreateExternalTexture(
                        m_renderWidth, m_renderHeight, TextureFormat.BGRA32, false, false, sharedNativeTexture);
                }
            }

            if (!AdoptControllerClient())
            {
                Teardown();
                enabled = false;
                return;
            }

            // Fatal, not best-effort: with the streams already suppressed, a missing composite means
            // Shark renders into surfaces nothing displays and the SDK's renderer has been told to
            // stand down - a permanently blank scene. Better to hand the splats back.
            if (!BuildComposite())
            {
                Teardown();
                enabled = false;
                return;
            }

            // One plugin event per frame, not one per view: the native side renders every view in a
            // single call so the depth sort is shared.
            m_commandBuffer = new CommandBuffer { name = "SplatRenderer" };
            m_commandBuffer.IssuePluginEvent(SplatRendererBridge.AquaSplatRenderer_GetRenderEventCallbackPtr(),
                                             SplatRendererBridge.FrameEventId);
            m_camera.AddCommandBuffer(CameraEvent.AfterEverything, m_commandBuffer);
        }

        // The per-view composite: one mesh whose vertices ARE clip space, drawn last, sampling
        // each view's own surface. See SplatComposite.shader for why this rather than
        // CommandBuffer.Blit.
        bool BuildComposite()
        {
            // Resources.Load rather than Shader.Find: a shader only ever reached by Shader.Find is
            // stripped from a player build.
            Shader compositeShader = Resources.Load<Shader>(CompositeShaderResource);
            if (compositeShader == null)
            {
                Debug.LogError($"SplatRenderer: '{CompositeShaderResource}' is missing from the package's "
                               + "Resources - nothing could be displayed");
                return false;
            }

            m_quad = new GameObject("SplatRenderer_Composite");
            // Its vertices ignore the transform entirely, but its BOUNDS do not: a renderer whose
            // bounds fall outside the frustum is culled before the vertex shader ever runs. The
            // huge bounds below make that impossible; parenting to the camera is belt and braces.
            m_quad.transform.SetParent(m_camera.transform, false);
            m_quad.transform.localPosition = Vector3.zero;
            m_quad.transform.localRotation = Quaternion.identity;

            MeshFilter filter = m_quad.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildCompositeMesh();

            MeshRenderer renderer = m_quad.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            m_quadMaterial = new Material(compositeShader);
            m_quadMaterial.SetFloat("_VFlip", CompositeVFlip ? 1.0f : 0.0f);
            renderer.sharedMaterial = m_quadMaterial;
            StageCompositeTextures();
            return true;
        }

        // Two triangles at +/-1 in XY, which the vertex shader emits as clip space unchanged.
        // The bounds are deliberately enormous rather than the mesh's own: see BuildComposite.
        static Mesh BuildCompositeMesh()
        {
            Mesh mesh = new Mesh { name = "SplatRenderer_CompositeQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-1.0f, -1.0f, 0.0f),
                new Vector3(-1.0f, 1.0f, 0.0f),
                new Vector3(1.0f, 1.0f, 0.0f),
                new Vector3(1.0f, -1.0f, 0.0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(0.0f, 1.0f),
                new Vector2(1.0f, 1.0f),
                new Vector2(1.0f, 0.0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1.0e5f);
            return mesh;
        }

        // Points the material at whichever surface each view should sample this frame. The slot
        // comes from native rather than being assumed - see AquaSplatRenderer_GetPresentSlot. In
        // mono only _LeftTex is bound, which is the one the shader samples at eye index 0.
        void StageCompositeTextures()
        {
            if (m_quadMaterial == null)
            {
                return;
            }
            for (int view = 0; view < m_viewCount; ++view)
            {
                int slot = SplatRendererBridge.AquaSplatRenderer_GetPresentSlot(m_renderer, view);
                if (slot < 0)
                {
                    continue;
                }
                Texture2D surface = m_sharedTextures[view * m_ringDepth + slot];
                m_quadMaterial.SetTexture(view == 0 ? "_LeftTex" : "_RightTex", surface);
            }
        }

        // onBeforeRender rather than LateUpdate: TrackedPoseDriver re-applies the head pose from
        // onBeforeRender at [BeforeRenderOrder(-30000)], and that is the sample Unity renders with.
        // Reading the camera in LateUpdate would stage a pose one step staler than the one the quad
        // is drawn with, and the content would lag its own window under head motion. This handler
        // carries the default order, so it runs after the driver's.
        void OnEnable()
        {
            Application.onBeforeRender += StageViews;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= StageViews;
        }

        void StageViews()
        {
            if (m_renderer < 0 || m_camera == null)
            {
                return;
            }

            // Staged for the render event registered in TryInitialize to pick up when the
            // CommandBuffer actually runs later this frame - no native rendering happens here.
            // worldToCameraMatrix and projectionMatrix rather than localToWorldMatrix plus a
            // field of view: an off-axis per-eye frustum cannot be recovered from the latter.
            for (int view = 0; view < m_viewCount; ++view)
            {
                // In stereo the view IS the eye. GetStereoViewMatrix already folds in the head pose
                // and the XR Origin, and GetStereoProjectionMatrix is the only source of the real
                // off-axis frustum - the mono projection is symmetric and does not even enclose it.
                if (m_isStereo && m_camera.stereoEnabled)
                {
                    Camera.StereoscopicEye eye = (Camera.StereoscopicEye)view;
                    ToColumnMajor(m_camera.GetStereoViewMatrix(eye), m_viewMatrix);
                    ToColumnMajor(m_camera.GetStereoProjectionMatrix(eye), m_projectionMatrix);
                }
                else
                {
                    ToColumnMajor(m_camera.worldToCameraMatrix, m_viewMatrix);
                    ToColumnMajor(m_camera.projectionMatrix, m_projectionMatrix);
                }

                SplatRendererBridge.AquaSplatRenderer_SetView(
                    m_renderer, view, m_viewMatrix, m_projectionMatrix);
            }

            // Re-staged every frame rather than once at build: with a ring the slot changes
            // per frame, and a composite that bound its textures once would sample a stale
            // surface without ever saying so.
            StageCompositeTextures();
        }

        void Teardown()
        {
            // Logged first and unconditionally: on visionOS whether teardown is reached at all is
            // the open question - leaving the immersive space need not destroy anything, and
            // stopping from Xcode kills the process with no callbacks.
            Debug.Log($"SplatRenderer: Teardown - renderer {m_renderer}, camera "
                      + $"{(m_camera == null ? "null" : m_camera.name)}");

            // First: the controller outlives this component, so a handler left attached would call
            // into a torn-down renderer on its next drain.
            if (m_streamController != null)
            {
                m_streamController.SceneChangesDrained -= OnSceneChangesDrained;
            }
            // Hand rendering back to the SDK. Left suppressed, these streams would be invisible to
            // both renderers for as long as they live - and the controller outlives this component.
            SetStreamsRenderedExternally(false);
            m_sdkStreams = Array.Empty<MirisStream>();
            m_lastModelMatrices.Clear();
            m_changesAccepted = false;
            m_streamStarted = false;

            // Before the shared textures are dropped: the material holds one, and the display
            // object is parented to the camera, which outlives this component.
            if (m_quad != null)
            {
                // The composite builds its own Mesh, which is not owned by the GameObject and
                // survives it - Unity leaks meshes created from script unless they are destroyed.
                MeshFilter filter = m_quad.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    Destroy(filter.sharedMesh);
                }
                Destroy(m_quad);
                m_quad = null;
            }
            if (m_quadMaterial != null)
            {
                Destroy(m_quadMaterial);
                m_quadMaterial = null;
            }

            if (m_commandBuffer != null && m_camera != null)
            {
                m_camera.RemoveCommandBuffer(CameraEvent.AfterEverything, m_commandBuffer);
                m_commandBuffer = null;
            }

            if (m_sharedTextures != null)
            {
                for (int i = 0; i < m_sharedTextures.Length; ++i)
                {
                    m_sharedTextures[i] = null;
                }
            }
            if (m_renderer >= 0)
            {
                // After the shared textures are dropped: this releases the IOSurfaces they wrap.
                // GL.IssuePluginEvent rather than the CommandBuffer, which is already gone by now -
                // the release must happen on the render thread, and Destroy only waits for it.
                GL.IssuePluginEvent(SplatRendererBridge.AquaSplatRenderer_GetRenderEventCallbackPtr(),
                                    SplatRendererBridge.ShutdownEventId);
                SplatRendererBridge.AquaSplatRenderer_Destroy(m_renderer);
                m_renderer = -1;
            }
        }

        void OnDestroy()
        {
            Teardown();
        }
    }
}
