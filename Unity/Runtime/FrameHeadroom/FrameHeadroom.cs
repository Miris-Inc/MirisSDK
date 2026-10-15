// Copyright © 2026 Miris, Inc. All rights reserved.

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Miris.Runtime
{
    // Frame time measured to feed the budget controller, 
    // which cannot use the wall-clock delta when vsynced. the nuts and bolts of this 
    // result in the sample time being a frame or two behind, which should still be acceptable.

    // this is basically because XRDisplaySubsystem.TryGetAppGPUTimeLastFrame isn't available on AVP.

    // Frame time is computed as max( GPU , CPU )
    // where
    // GPU = shark + host (unity passes) + compositor
    // CPU = main thread
    
    // SO THIS WILL ONLY WORK IF WE KEEP THE BLOCKING MAIN THREAD TIME UNDER CONTROL
    
    [DefaultExecutionOrder(-30000)]
    public class FrameHeadroom : MonoBehaviour
    {
#if UNITY_VISIONOS || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        // The compositor runs out-of-process, where nothing in-process can measure it. Unlike host
        // cost this is a property of the OS rather than of the customer's content.
        
        // Note: we give the compositor 1ms of allowance, which is generous, I think.
        const float CompositorAllowanceMs = 1.0f;

        const float SampleFreshSeconds = 0.25f;
        const float LogIntervalSeconds = 10.0f;

        // Shark has to start streaming before Dawn creates a queue, so measurement not being up
        // yet is normal yet for the first seconds. 
        const int MeasuringWarmupFrames = 900; // 10 seconds

        static FrameHeadroom s_instance;

        // Raw, not smoothed: the controller filters its own input. False when no fresh sample
        // exists, and the caller should keep using wall-clock time.
        public static bool TryGetEffectiveFrameMs(out double effectiveMs)
        {
            FrameHeadroom instance = s_instance;
            if (instance == null || !instance.m_hasSample
                || Time.unscaledTime - instance.m_lastSampleTime > SampleFreshSeconds)
            {
                effectiveMs = 0.0;
                return false;
            }
            effectiveMs = instance.m_effectiveMs;
            return true;
        }

        // XR-only: MirisStreamController is the sole consumer and only reads this in XR, and the
        // native side patches MTLCommandQueue process-wide, which is not worth paying for elsewhere.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance != null || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Metal
                || !XRUtils.IsXR())
            {
                return;
            }
            GameObject host = new GameObject("FrameHeadroom");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideInHierarchy;
            host.AddComponent<FrameHeadroom>();
        }

        System.IntPtr m_callback;
        Coroutine m_endOfFrame;

        long m_lastFrameIndex = -1;
        double m_lastThreadCpuSeconds = -1.0;
        float m_mainThreadCpuMs;

        bool m_hasSample;
        float m_lastSampleTime;
        double m_effectiveMs;

        int m_notMeasuringFrames;
        bool m_loggedNotMeasuring;

        double m_sumShark;
        double m_sumHost;
        double m_sumMainCpu;
        double m_sumEffective;
        int m_windowSamples;
        float m_logElapsed;

        void OnEnable()
        {
            // A missing native library is routine in the editor and surfaces on the first call.
            try
            {
                m_callback = FrameHeadroomBridge.AquaFrameHeadroom_GetRenderEventCallbackPtr();
                FrameHeadroomBridge.AquaFrameHeadroom_SetActive(1);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("FrameHeadroom: no native AquaUnity to measure with, so the budget "
                                 + $"controller keeps its wall-clock frame time ({e.GetType().Name})");
                enabled = false;
                return;
            }

            // Only once native is known good, so a failed start leaves nothing for
            // TryGetEffectiveFrameMs to find.
            s_instance = this;
            m_endOfFrame = StartCoroutine(IssueFrameEnd());
        }

        void OnDisable()
        {
            if (s_instance != this)
            {
                return;
            }
            if (m_endOfFrame != null)
            {
                StopCoroutine(m_endOfFrame);
                m_endOfFrame = null;
            }
            FrameHeadroomBridge.AquaFrameHeadroom_SetActive(0);

            m_lastFrameIndex = -1;
            m_lastThreadCpuSeconds = -1.0;
            m_hasSample = false;
            s_instance = null;
        }

        void Update()
        {
            SampleMainThreadCpu();
            GL.IssuePluginEvent(m_callback, FrameHeadroomBridge.FrameBeginEventId);
            PollSample();
            ReportIfNotMeasuring();
        }

        // Nothing is measured until the patch is applied and Dawn's queue has been seen
        void ReportIfNotMeasuring()
        {
            if (m_loggedNotMeasuring || FrameHeadroomBridge.AquaFrameHeadroom_IsMeasuring() != 0)
            {
                return;
            }
            if (++m_notMeasuringFrames < MeasuringWarmupFrames)
            {
                return;
            }
            m_loggedNotMeasuring = true;
            Debug.LogError($"FrameHeadroom: no GPU time measured after {m_notMeasuringFrames} frames "
                           + "- the Metal queue patch did not install, or Shark never created its "
                           + "queue. The budget controller is using wall-clock frame time, which is "
                           + "pinned to the refresh interval in XR.");
        }

        IEnumerator IssueFrameEnd()
        {
            WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                GL.IssuePluginEvent(m_callback, FrameHeadroomBridge.FrameEndEventId);
            }
        }

        // Update to Update spans the whole main-thread frame, and blocked time is excluded because
        // it never accrues to the thread.
        void SampleMainThreadCpu()
        {
            double nowSeconds = FrameHeadroomBridge.AquaFrameHeadroom_ThreadCpuTimeSeconds();
            if (m_lastThreadCpuSeconds >= 0.0)
            {
                m_mainThreadCpuMs = (float)((nowSeconds - m_lastThreadCpuSeconds) * 1000.0);
            }
            m_lastThreadCpuSeconds = nowSeconds;
        }

        void PollSample()
        {
            long frameIndex = FrameHeadroomBridge.AquaFrameHeadroom_GetLatestSample(
                out double hostGpuBusyMs, out double offQueueBusyMs);
            if (frameIndex < 0 || frameIndex == m_lastFrameIndex)
            {
                return;
            }
            m_lastFrameIndex = frameIndex;

            // Host and Shark are summed, not unioned: separate queues on one GPU timeshare, so
            // their overlap is not free parallelism. The CPU term is a frame newer than the GPU
            // ones, which the max never turns on at these magnitudes.
            double gpuMs = hostGpuBusyMs + offQueueBusyMs + CompositorAllowanceMs;
            m_effectiveMs = System.Math.Max(gpuMs, m_mainThreadCpuMs);
            m_hasSample = true;
            m_lastSampleTime = Time.unscaledTime;

            m_sumShark += offQueueBusyMs;
            m_sumHost += hostGpuBusyMs;
            m_sumMainCpu += m_mainThreadCpuMs;
            m_sumEffective += m_effectiveMs;
            ++m_windowSamples;

            Report();
        }

        void Report()
        {
            m_logElapsed += Time.unscaledDeltaTime;
            if (m_logElapsed < LogIntervalSeconds)
            {
                return;
            }

            double perSample = 1.0 / m_windowSamples;
            Debug.Log($"[FrameHeadroom] effective {m_sumEffective * perSample:F2}ms = shark "
                      + $"{m_sumShark * perSample:F2} + host {m_sumHost * perSample:F2} + compositor "
                      + $"{CompositorAllowanceMs:F1}, mainCpu {m_sumMainCpu * perSample:F2}ms "
                      + $"(mean of {m_windowSamples} frames)");

            m_logElapsed = 0.0f;
            m_windowSamples = 0;
            m_sumShark = 0.0;
            m_sumHost = 0.0;
            m_sumMainCpu = 0.0;
            m_sumEffective = 0.0;
        }
#else
        // Kept so MirisStreamController compiles unguarded off Apple platforms.
        public static bool TryGetEffectiveFrameMs(out double effectiveMs)
        {
            effectiveMs = 0.0;
            return false;
        }
#endif
    }
}
