// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Miris.Runtime.Experimental
{

    // Drives the native Shark splat renderer from Unity and displays what it produces.
    //
    // Two views even on a flat macOS screen: Shark sorts once per frame and renders per view, and
    // a single view exercises that sequence too weakly to catch it being written wrong.
    public class SplatRenderer : MonoBehaviour
    {
        const string DefaultEnvironment = "Develop";
        const string DefaultServerUrl = "https://dev.miris.com/viewer/v1";
        const string DefaultAssetId = "3e48664f-ed0a-4339-b731-f944f489bbef";
        const int DefaultMaxSplats = 2 * 1024 * 1024;
        const float DefaultViewSeparation = 0.064f;
        const string CompositeShaderName = "Miris/SplatComposite";

        // Desktop (EditorOnGUI) render size only. In XR these are ignored: the size comes
        // from the compositor's per-eye drawable - see ResolveRenderSize.
        [SerializeField] int m_width = 1024;
        [SerializeField] int m_height = 1024;

        // Fraction of the per-eye drawable to rasterize, XR modes only. 1 is native; lower trades
        // sharpness for fill cost.
        //
        // Change this in the SCENE, not here: Unity deserializes the scene's value over this
        // initializer, which only applies to a component with no stored value.
        [SerializeField] float m_renderScale = 1.0f;

        // Which Miris environment to stream from. Only used to look up the viewer key below -
        // Shark takes the server URL directly, and the env->URL mapping in MirisInternalApi is
        // client-scoped, which this path deliberately has no client for.
        [SerializeField] string m_environment = DefaultEnvironment;
        [SerializeField] string m_serverUrl = DefaultServerUrl;
        [SerializeField] string m_assetId = DefaultAssetId;
        // Left empty on purpose: resolved from the local aqua-config for m_environment, so no
        // key is checked in. Set it here only to override that.
        [SerializeField] string m_viewerKey = "";
        [SerializeField] int m_maxSplats = DefaultMaxSplats;

        // Horizontal separation between the two views. Not stereoscopically meaningful here -
        // it just makes the two views visibly distinct so a frame that renders only one is
        // obvious on screen.
        [SerializeField] float m_viewSeparation = DefaultViewSeparation;

        // How the rendered surfaces reach the screen.
        public enum DisplayMode
        {
            // Two synthetic cameras drawn side by side with OnGUI. The desktop iteration loop,
            // and the only place the sort-once/render-per-view invariant is exercised.
            EditorOnGUI,
            // Both eyes, each rendered with its own frustum and blitted 1:1 over the eye buffer.
            // The real thing - see SplatComposite.shader.
            StereoComposite,
        }

        [SerializeField] DisplayMode m_displayMode = DisplayMode.EditorOnGUI;

        // XR modes only. The camera the compositor actually presents, and the source of the
        // per-eye matrices. Left empty, Camera.main is used.
        [SerializeField] Camera m_xrCamera;
        // StereoComposite's shader, and assigned for the same reason as the one above - a shader
        // only ever reached by Shader.Find is stripped from a player build.
        [SerializeField] Shader m_compositeShader;
        // Whether the composite flips V when sampling the surfaces - a fact about Metal's, Dawn's
        // and Unity's texture conventions stacked on each other, only establishable on device.
        // NOT the framebuffer-orientation flip, which the shader handles unconditionally via
        // _ProjectionParams.x; if the image is upside down and this does not fix it, suspect that.
        [SerializeField] bool m_compositeVFlip = true;

        // Assign to stream through the Miris SDK instead of the fields above: Shark then attaches to
        // this controller's client rather than creating a second one, and m_assetId / m_serverUrl are
        // the streams' business, not ours. Left empty, the component keeps its own hardcoded stream -
        // which is what every existing scene does.
        [SerializeField] MirisStreamController m_streamController;

        // Every MirisStream pointing at that controller, found once at adoption. Discovered rather
        // than serialized so adding an asset to the scene needs no wiring here, and so a stream can
        // never be silently left on the SDK's renderer by being forgotten in a list. Inactive objects
        // are included deliberately: they are shipped disabled, and enabling them is our job.
        MirisStream[] m_sdkStreams = System.Array.Empty<MirisStream>();

        // True once native has accepted a drained batch. Only used to keep the rejection error to
        // one line rather than one per frame.
        bool m_changesAccepted = false;

        // True once the stream has been let go. The MirisStream is held disabled until native
        // confirms adoption, because a batch drained before Shark has a context is consumed by the
        // SDK and never re-delivered - the objects in it are simply missing, with nothing to say so.
        bool m_streamStarted = false;
        int m_adoptWaitFrames = 0;

        // Scratch for the model transform push, and the last one sent. Compared rather than pushed
        // blindly so a stationary asset costs nothing per frame.
        readonly float[] m_modelMatrix = new float[16];
        readonly float[] m_modelMatrixInverse = new float[16];
        // Last matrix pushed per ModelRoot
        readonly Dictionary<int, Matrix4x4> m_lastModelMatrices = new();

        bool IsSdkDriven => m_streamController != null;

        // Two, always: on desktop to keep the shared-sort invariant honest, on device because
        // they are the eyes.
        int m_viewCount = 2;

        // Surfaces per view, read from native in Start. Not a constant here - native owns the ring,
        // and if the two disagree the composite reads a surface Shark is writing, which tears
        // intermittently rather than failing.
        int m_ringDepth = 1;

        // Anything that is not the desktop OnGUI loop is driven by the compositor: the XR camera
        // is the view, the render size comes from the drawable, and the display is head-locked.
        bool IsXrMode => m_displayMode != DisplayMode.EditorOnGUI;

        // The size Shark actually rasterizes into, resolved once in Update before anything is
        // created. Not the serialized fields - in XR those are not what gets rendered.
        int m_renderWidth;
        int m_renderHeight;
        int m_renderSizeWaitFrames;

        int m_renderer = -1;
        Camera[] m_cameras;
        GameObject[] m_cameraObjects;
        RenderTexture[] m_cameraTargets;
        Texture2D[] m_sharedTextures;
        int[] m_viewIndices;
        CommandBuffer m_commandBuffer;
        GameObject m_quad;
        Material m_quadMaterial;

        // Reused every frame so StageViews does not allocate at frame rate.
        float[] m_viewMatrix = new float[16];
        float[] m_projectionMatrix = new float[16];

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

        // Mirrors what DeveloperStreamingController does when the environment changes: the key
        // that works is environment-specific, and the local config is where they live.
        string ResolveViewerKey()
        {
            if (!string.IsNullOrEmpty(m_viewerKey))
            {
                return m_viewerKey;
            }
            try
            {
                ClientConfig config = ClientConfig.Load();
#if MIRIS_INTERNAL
                if (config.asset_viewer_keys.ContainsKey(m_environment))
                {
                    return config.asset_viewer_keys[m_environment];
                }
                // Not falling through to GetAssetViewerKey here: it returns whichever key
                // happens to be first, which would hand a Production key to a Develop server
                // and fail authentication with nothing pointing at the cause.
                Debug.LogError($"SplatRenderer: no viewer key configured for environment '{m_environment}'");
                return "";
#else
                return config.GetAssetViewerKey();
#endif
            }
            catch (Exception e)
            {
                Debug.LogError($"SplatRenderer: failed to read the viewer key for ENV {m_environment}: {e}");
                return "";
            }
        }

        // Scenes exist that were serialized against a different field set under this script GUID,
        // so rather than depend on how Unity fills in fields with no stored value, treat empty/zero
        // as "unset" and substitute the default. Otherwise such a scene silently streams from
        // nowhere with an empty asset id.
        void ApplyDefaultsForUnsetFields()
        {
            if (m_width <= 0) { m_width = 1024; }
            if (m_height <= 0) { m_height = 1024; }
            if (string.IsNullOrEmpty(m_environment)) { m_environment = DefaultEnvironment; }
            if (string.IsNullOrEmpty(m_serverUrl)) { m_serverUrl = DefaultServerUrl; }
            if (string.IsNullOrEmpty(m_assetId)) { m_assetId = DefaultAssetId; }
            if (m_maxSplats <= 0) { m_maxSplats = DefaultMaxSplats; }
            if (m_viewSeparation <= 0.0f) { m_viewSeparation = DefaultViewSeparation; }
            // Clamped rather than defaulted: above 1 renders more than the compositor can show,
            // which costs fill for nothing.
            m_renderScale = Mathf.Clamp(m_renderScale <= 0.0f ? 1.0f : m_renderScale, 0.1f, 1.0f);
        }

        void Start()
        {
            ApplyDefaultsForUnsetFields();

            m_viewCount = 2;
            m_cameras = new Camera[m_viewCount];
            m_cameraObjects = new GameObject[m_viewCount];
            m_cameraTargets = new RenderTexture[m_viewCount];
            // Sized once the ring depth is known, in TryInitialize - native has to exist first.
            m_viewIndices = new int[m_viewCount];

            // Deliberately not printing m_assetId or m_serverUrl when the SDK is driving: they are
            // inert in that mode, and echoing them made a stale field look authoritative - changing
            // one and seeing it here reads as confirmation that the asset changed, when the streams
            // decide that. m_environment is still live: it selects the viewer key.
            if (IsSdkDriven)
            {
                Debug.Log($"SplatRenderer: starting - SDK-driven via '{m_streamController.name}', env "
                          + $"'{m_environment}', {m_displayMode}, {m_viewCount} view(s). Asset ids and "
                          + "server come from the MirisStreams.");
            }
            else
            {
                Debug.Log($"SplatRenderer: starting - env '{m_environment}', server '{m_serverUrl}', asset '{m_assetId}', {m_displayMode}, {m_viewCount} view(s)");
            }

            if (IsXrMode && !ResolveXrCamera())
            {
                enabled = false;
                return;
            }

            // Everything past this point needs the render size, and in XR that is the
            // compositor's to give - see TryInitialize.
        }

        // Nothing here can run until the render size is known, which in XR means waiting for XR to
        // come up. Shark's resolution is fixed at shark_create - ctx->width and ctx->height drive
        // initRenderState, ensureRenderTarget, initGraphicsState, the depth sort and
        // updateViewframe - so there is no resize short of rebuilding the render state mid-stream.
        void Update()
        {
            if (m_renderer >= 0)
            {
                StartSdkStreamWhenAdopted();
                if (IsSdkDriven)
                {
                    PushModelTransforms();
                }
                return;
            }

            if (!ResolveRenderSize(out m_renderWidth, out m_renderHeight))
            {
                // Not an error for the first few frames, but waiting forever is silent and looks
                // exactly like a stream that never arrived.
                if (++m_renderSizeWaitFrames % 120 == 0)
                {
                    Debug.LogWarning($"SplatRenderer: still waiting for XR to report an eye texture "
                                     + $"size after {m_renderSizeWaitFrames} frames - nothing has "
                                     + "been created yet");
                }
                return;
            }

            TryInitialize();
        }

        // The size Shark rasterizes into. In XR that is the compositor's per-eye drawable
        // (1888x1792 on Vision Pro), not the serialized fields - rendering under it and letting
        // the compositor upscale is the single largest sharpness loss in the path. m_width and
        // m_height stay the desktop size, where there is no drawable to ask.
        bool ResolveRenderSize(out int width, out int height)
        {
            if (!IsXrMode)
            {
                width = m_width;
                height = m_height;
                return true;
            }

            if (XRSettings.eyeTextureWidth <= 0 || XRSettings.eyeTextureHeight <= 0)
            {
                width = 0;
                height = 0;
                return false;
            }

            width = Mathf.Max(1, Mathf.RoundToInt(XRSettings.eyeTextureWidth * m_renderScale));
            height = Mathf.Max(1, Mathf.RoundToInt(XRSettings.eyeTextureHeight * m_renderScale));
            return true;
        }

        // Attaches native to the controller's client and subscribes to its drained changes.
        //
        // The order matters and is the reason the stream is added from here rather than left to the
        // controller: the SDK's drain is destructive, so any batch that lands before native has a
        // context is gone for good. Adopt, subscribe, and only then ask for the asset.
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

            PushViewerKeyIfNotOverridden();

            if (SplatRendererBridge.AquaSplatRenderer_AdoptClient(
                    m_renderer, handle, m_sdkStreams[0].m_assetId, m_maxSplats) != 0)
            {
                Debug.LogError("SplatRenderer: the native renderer rejected the client handle");
                return false;
            }

            // Only now, and this ordering matters. Suppressing before the request was accepted meant a
            // rejection left the streams suppressed with Shark not running, so neither renderer drew
            // anything and the scene was silently blank. Still ahead of any stream loading, which is
            // what the flag needs: it is read when each render component is constructed, and nothing
            // loads until StartSdkStreamWhenAdopted releases it.
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

        // Every MirisStream in the scene whose controller is ours, disabled ones included - they are
        // shipped disabled and this component is what enables them.
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

        // Gives the controller the viewer key for OUR environment, unless someone chose one.
        //
        // Two failure modes to steer between. Left alone, an empty key field means the controller
        // takes ClientConfig.GetAssetViewerKey(), which looks for a key named "Prod" and otherwise
        // returns whichever is FIRST - so dictionary order decides which server the key belongs to,
        // and a Develop asset gets requested with a Production key. But pushing unconditionally is
        // worse: it silently replaces a key someone typed in the Inspector for this specific stream,
        // and keys can be scoped per asset, so the deliberate one is often the only one that works.
        //
        // The two cases are distinguishable: if the controller's current key is exactly what
        // ClientConfig would default to, nobody chose it and it is safe to replace. Anything else was
        // a deliberate override and is left alone.
        void PushViewerKeyIfNotOverridden()
        {
            string current = m_streamController.ViewerKey;
            string configDefault = "";
            try
            {
                configDefault = ClientConfig.Load().GetAssetViewerKey();
            }
            catch (Exception e)
            {
                // Without the default there is no way to tell a chosen key from a seeded one, so the
                // safe reading is "chosen" - never clobber.
                Debug.LogWarning($"SplatRenderer: cannot read the ClientConfig default key, leaving the "
                                 + $"controller's key alone: {e.Message}");
                return;
            }

            if (!string.IsNullOrEmpty(current) && current != configDefault)
            {
                Debug.Log("SplatRenderer: the stream controller's viewer key was set explicitly - leaving "
                          + "it as is rather than substituting the one configured for "
                          + $"'{m_environment}'");
                return;
            }

            string viewerKey = ResolveViewerKey();
            if (string.IsNullOrEmpty(viewerKey))
            {
                // Deliberately not calling SetViewerKey("") - the controller reads that as "use the
                // ClientConfig default", which is the behaviour being avoided here.
                Debug.LogError($"SplatRenderer: no viewer key for environment '{m_environment}' - the "
                               + "controller keeps whatever ClientConfig defaulted to, which may not "
                               + "match the server the asset lives on");
                return;
            }

            m_streamController.SetViewerKey(viewerKey);
            Debug.Log($"SplatRenderer: pushed the '{m_environment}' viewer key to the stream controller");
        }

        // Keeps Shark's copy of the stream's transform current, so moving the MirisStream GameObject
        // moves its splats.
        //
        // Shark applies this on the GPU through modelRootTransforms, which is why the transform can
        // change without re-uploading any splat data. The inverse goes with it because the
        // true-perspective projection transforms a quadratic form and needs it, and WGSL has no
        // inverse() builtin - so it is computed once here rather than per splat there.
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
                foreach (KeyValuePair<int, GaussianSplatRenderComponent> modelRoot in stream.ModelRoots)
                {
                    ++liveModelRoots;
                    GaussianSplatRenderComponent renderComponent = modelRoot.Value;
                    if (renderComponent == null)
                    {
                        continue;
                    }

                    // In order to match GaussianSplatRenderComponent.Update (the old Unity renderer),
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

        // Lets the asset go, once native confirms it holds the controller's client.
        //
        // This is the ordering the whole SDK-driven path depends on. MirisStream adds its asset in
        // OnEnable, which is frames earlier than this component finishes initializing - it waits on
        // the compositor for a drawable size first - and AdoptClient is only queued, taking effect
        // on the next render event. Anything the SDK drains in between is gone: it consumes the
        // changes for its own bookkeeping and nothing re-delivers them, so those objects never
        // reach Shark and the only symptom is splats that are quietly absent. Holding the stream
        // disabled until adoption is confirmed removes the window rather than narrowing it.
        void StartSdkStreamWhenAdopted()
        {
            if (!IsSdkDriven || m_streamStarted)
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

            // The controller drains every LateUpdate whether or not the scene changed, so most batches
            // are empty. Skipping those keeps a P/Invoke and a native lock off every frame - and stops
            // the native side warning about a context that does not exist yet when the batch it would
            // have lost is empty anyway, which made that warning fire on every healthy startup.
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
                m_viewIndices[view] = view;
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

            if (IsSdkDriven)
            {
                if (!AdoptControllerClient())
                {
                    Teardown();
                    enabled = false;
                    return;
                }
            }
            else
            {
                string viewerKey = ResolveViewerKey();
                if (string.IsNullOrEmpty(viewerKey))
                {
                    Debug.LogError($"SplatRenderer: no viewer key for environment '{m_environment}' - set one on the component or add it to the local aqua-config");
                    Teardown();
                    enabled = false;
                    return;
                }

                if (SplatRendererBridge.AquaSplatRenderer_StreamAsset(
                        m_renderer, viewerKey, m_assetId, m_serverUrl, m_maxSplats) != 0)
                {
                    Debug.LogError("SplatRenderer: the native renderer rejected the stream request - check viewerKey, assetId and serverUrl");
                    Teardown();
                    enabled = false;
                    return;
                }
            }

            BuildCameras();

            if (m_displayMode == DisplayMode.StereoComposite)
            {
                BuildComposite();
            }

            // Only ONE camera carries the plugin event: the native side renders every view in a
            // single call so the depth sort is shared, so attaching it to both would sort twice per
            // frame. It goes on the last camera to render.
            m_commandBuffer = new CommandBuffer { name = "SplatRenderer" };
            m_commandBuffer.IssuePluginEvent(SplatRendererBridge.AquaSplatRenderer_GetRenderEventCallbackPtr(),
                                             SplatRendererBridge.FrameEventId);
            m_cameras[m_viewCount - 1].AddCommandBuffer(CameraEvent.AfterEverything, m_commandBuffer);
        }

        bool ResolveXrCamera()
        {
            if (m_xrCamera == null)
            {
                m_xrCamera = Camera.main;
            }
            if (m_xrCamera == null)
            {
                Debug.LogError("SplatRenderer: the XR camera is required - assign it, or tag one MainCamera");
                return false;
            }
            return true;
        }

        // The per-eye composite: one mesh whose vertices ARE clip space, drawn last, sampling
        // each eye's own surface. See SplatComposite.shader for why this rather than
        // CommandBuffer.Blit.
        void BuildComposite()
        {
            if (m_compositeShader == null)
            {
                Debug.LogError($"SplatRenderer: no composite shader - assign one on the component. "
                               + $"'{CompositeShaderName}' ships with this package, so reaching this "
                               + "means the reference was lost rather than that the shader is missing");
                return;
            }

            m_quad = new GameObject("SplatRenderer_Composite");
            // Parented to the camera so it is always at the camera's position. Its vertices
            // ignore the transform entirely, but its BOUNDS do not: a renderer whose bounds fall
            // outside the frustum is culled before the vertex shader ever runs, and the symptom
            // is a composite that vanishes when you look away from where the object happens to
            // be. Huge bounds below make that impossible; the parenting is belt and braces.
            m_quad.transform.SetParent(m_xrCamera.transform, false);
            m_quad.transform.localPosition = Vector3.zero;
            m_quad.transform.localRotation = Quaternion.identity;

            MeshFilter filter = m_quad.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildCompositeMesh();

            MeshRenderer renderer = m_quad.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            m_quadMaterial = new Material(m_compositeShader);
            m_quadMaterial.SetFloat("_VFlip", m_compositeVFlip ? 1.0f : 0.0f);
            renderer.sharedMaterial = m_quadMaterial;
            StageCompositeTextures();
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

        // Points the material at whichever surface each eye should sample this frame. The slot
        // comes from native rather than being assumed - see AquaSplatRenderer_GetPresentSlot.
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

        void BuildCameras()
        {
            // In XR the real camera is the view. A synthetic offscreen camera is not guaranteed to
            // render inside an immersive space, and if it does not, the plugin event attached to it
            // never fires and nothing draws with no error anywhere. It also means the matrices are
            // the compositor's own.
            if (IsXrMode)
            {
                // Every view is the same physical camera. In stereo the two views are its two
                // eyes, which are matrices on one camera rather than two cameras - Unity renders
                // both in a single instanced pass, and adding a second camera would render the
                // scene twice for nothing.
                for (int i = 0; i < m_viewCount; ++i)
                {
                    m_cameras[i] = m_xrCamera;
                }
                return;
            }

            for (int i = 0; i < m_viewCount; ++i)
            {
                m_cameraObjects[i] = new GameObject("SplatRenderer_View" + i);
                m_cameraObjects[i].transform.SetParent(transform, false);
                float offset = (i - 0.5f * (m_viewCount - 1)) * m_viewSeparation;
                m_cameraObjects[i].transform.localPosition = new Vector3(offset, 0f, 0f);

                Camera view = m_cameraObjects[i].AddComponent<Camera>();
                // These cameras exist to supply matrices and to host the plugin event, not to
                // put pixels on screen - Shark writes the shared textures directly, and OnGUI
                // below is what displays them. A render target keeps them off the backbuffer.
                m_cameraTargets[i] = new RenderTexture(m_renderWidth, m_renderHeight, 24,
                                                       RenderTextureFormat.BGRA32);
                m_cameraTargets[i].Create();
                view.targetTexture = m_cameraTargets[i];
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = Color.black;
                // Pinned to the render target's shape, not left for Unity to infer. Everything
                // downstream - the projection matrix handed to Shark, and the aspect the
                // streaming solver derives from its focal scales - keys off this, and it must
                // describe the surface Shark actually rasterizes into, never the Game view.
                view.aspect = (float)m_renderWidth / (float)m_renderHeight;
                // Fixes the order the two cameras render in, so the plugin event on the last
                // one really is last and both views have been staged by the time it fires.
                view.depth = i;
                m_cameras[i] = view;
            }
        }

        // onBeforeRender rather than LateUpdate. TrackedPoseDriver applies the head pose twice a
        // frame - once in Update, and again from onBeforeRender at [BeforeRenderOrder(-30000)],
        // which is the sample it actually renders with. Reading the camera in LateUpdate gets the
        // Update-time pose, so every frame would be rendered from a pose one step staler than the
        // one Unity draws the quad with, and the content would lag its own window under head
        // motion. This handler carries the default order, so it runs after the driver's.
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
            if (m_renderer < 0)
            {
                return;
            }

            // Staged for the render event registered in Start() to pick up when the
            // CommandBuffer actually runs later this frame - no native rendering happens here.
            // worldToCameraMatrix and projectionMatrix rather than localToWorldMatrix plus a
            // field of view: an off-axis per-eye frustum cannot be recovered from the latter.
            for (int i = 0; i < m_viewCount; ++i)
            {
                if (m_cameras[i] == null)
                {
                    continue;
                }

                // In stereo the view IS the eye. GetStereoViewMatrix already folds in the head pose
                // and the XR Origin, so this composes with the TrackedPoseDriver rather than
                // duplicating it, and GetStereoProjectionMatrix is the only source of the real
                // off-axis frustum - horizontally mirrored per eye, with a shared vertical tilt from
                // the canted displays. The mono projectionMatrix is symmetric and does not even
                // enclose them horizontally.
                if (m_displayMode == DisplayMode.StereoComposite && m_cameras[i].stereoEnabled)
                {
                    Camera.StereoscopicEye eye = (Camera.StereoscopicEye)i;
                    ToColumnMajor(m_cameras[i].GetStereoViewMatrix(eye), m_viewMatrix);
                    ToColumnMajor(m_cameras[i].GetStereoProjectionMatrix(eye), m_projectionMatrix);
                }
                else
                {
                    ToColumnMajor(m_cameras[i].worldToCameraMatrix, m_viewMatrix);
                    ToColumnMajor(m_cameras[i].projectionMatrix, m_projectionMatrix);
                }

                SplatRendererBridge.AquaSplatRenderer_SetView(
                    m_renderer, m_viewIndices[i], m_viewMatrix, m_projectionMatrix);
            }

            if (m_displayMode == DisplayMode.StereoComposite)
            {
                // Re-staged every frame rather than once at build: with a ring the slot changes
                // per frame, and a composite that bound its textures once would sample a stale
                // surface without ever saying so.
                StageCompositeTextures();
            }
        }

        // The two views are drawn side by side: this is a bring-up display, and seeing both
        // surfaces is what makes a frame that rendered only one obvious at a glance.
        void OnGUI()
        {
            if (m_displayMode != DisplayMode.EditorOnGUI)
            {
                return;
            }

            // V is flipped rather than drawn straight. Shark rasterizes through WebGPU, whose
            // framebuffer row 0 is NDC +Y - the top of the image - and Metal stores it that way
            // too. Unity treats row 0 of a texture as the BOTTOM (its UVs originate
            // bottom-left), so an externally-written Metal texture presents upside down.
            //
            // Corrected here and not in unityProjectionToShark: the flip is a property of how Unity
            // samples this texture, not of the camera. A consumer using Metal conventions needs no
            // flip, and negating the projection's Y row would drag fy, fov and cy negative and
            // corrupt the covariance and culling inputs derived from them.
            Rect flippedV = new Rect(0.0f, 1.0f, 1.0f, -1.0f);
            float viewWidth = Screen.width / (float)m_viewCount;
            float renderAspect = (float)m_renderWidth / (float)m_renderHeight;
            for (int i = 0; i < m_viewCount; ++i)
            {
                // Through the ring like the composite does, not m_sharedTextures[i] - with more
                // than one surface per view that index is view 1's second slot, not view 1.
                int presentSlot = SplatRendererBridge.AquaSplatRenderer_GetPresentSlot(m_renderer, i);
                Texture2D surface = presentSlot >= 0 ? m_sharedTextures[i * m_ringDepth + presentSlot] : null;
                if (surface != null)
                {
                    Rect slot = new Rect(i * viewWidth, 0.0f, viewWidth, Screen.height);
                    GUI.DrawTextureWithTexCoords(FitPreservingAspect(slot, renderAspect),
                                                 surface, flippedV, false);
                }
            }
        }

        // DrawTextureWithTexCoords always stretches to fill its rect - unlike DrawTexture it
        // takes no ScaleMode - so the destination has to be letterboxed by hand or the render
        // distorts as the Game view is resized.
        static Rect FitPreservingAspect(Rect slot, float aspect)
        {
            if (slot.width <= 0.0f || slot.height <= 0.0f || aspect <= 0.0f)
            {
                return slot;
            }
            float width = slot.width;
            float height = width / aspect;
            if (height > slot.height)
            {
                height = slot.height;
                width = height * aspect;
            }
            return new Rect(slot.x + 0.5f * (slot.width - width),
                            slot.y + 0.5f * (slot.height - height),
                            width, height);
        }

        void Teardown()
        {
            // Logged unconditionally and first, because on visionOS whether teardown is reached at
            // all is the open question: leaving the immersive space need not destroy anything, and
            // stopping from Xcode kills the process with no callbacks.
            Debug.Log($"SplatRenderer: Teardown - renderer {m_renderer}, cameras "
                      + $"{(m_cameras == null ? "null" : m_cameras.Length.ToString())}");

            // First, and before the early return below: the controller outlives this component, so a
            // handler left attached would call into a torn-down renderer on its next drain.
            if (m_streamController != null)
            {
                m_streamController.SceneChangesDrained -= OnSceneChangesDrained;
            }
            // Hand rendering back to the SDK. Left suppressed, these streams would be invisible to
            // both renderers for as long as they live - and the controller outlives this component.
            SetStreamsRenderedExternally(false);
            m_sdkStreams = System.Array.Empty<MirisStream>();
            m_lastModelMatrices.Clear();
            m_changesAccepted = false;
            m_streamStarted = false;

            // Start() can bail before the arrays exist - a missing XR camera does exactly that -
            // and OnDestroy still runs.
            if (m_cameras == null)
            {
                return;
            }

            // Before the shared textures are dropped: the material holds one, and the display
            // object is parented to the XR camera, which outlives this component.
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

            if (m_commandBuffer != null && m_cameras[m_viewCount - 1] != null)
            {
                m_cameras[m_viewCount - 1].RemoveCommandBuffer(CameraEvent.AfterEverything, m_commandBuffer);
                m_commandBuffer = null;
            }
            for (int i = 0; i < m_viewCount; ++i)
            {
                if (m_cameraObjects[i] != null)
                {
                    Destroy(m_cameraObjects[i]);
                    m_cameraObjects[i] = null;
                }
                if (m_cameraTargets[i] != null)
                {
                    m_cameraTargets[i].Release();
                    m_cameraTargets[i] = null;
                }
                m_cameras[i] = null;
                m_viewIndices[i] = -1;
            }

            // Its own loop: this array is sized per view AND per ring slot, while everything above
            // is per view.
            if (m_sharedTextures != null)
            {
                for (int i = 0; i < m_sharedTextures.Length; ++i)
                {
                    m_sharedTextures[i] = null;
                }
            }
            if (m_renderer >= 0)
            {
                // Releases the IOSurfaces the shared textures above wrap, so it has to come after
                // they are dropped.
                //
                // The release itself happens on the render thread - shark_destroy touches Dawn's
                // device, and doing that from here crashes. GL.IssuePluginEvent queues straight onto
                // the render thread rather than through a camera, which matters because the
                // CommandBuffer is already gone by now. Destroy then only waits, with its own
                // bounded timeout.
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
