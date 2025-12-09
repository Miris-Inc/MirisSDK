// Copyright © 2025 Miris.All rights reserved.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Miris.Runtime
{
    public class TimelineTouchUIController : MonoBehaviour
    {
        [Header("Timeline")]
        [SerializeField]
        private Slider m_timelineSlider;
        
        [SerializeField]
        private Image m_sliderBackground;

        [SerializeField]
        private Material m_loadingMaterial;

        [SerializeField]
        private Material m_loadedMaterial;

        Coroutine m_resetSeekStateCoroutine;

        [SerializeField]
        private MirisStreamController m_streamController;

        TimelinePlaybackState m_stateToRestoreAfterSeek = TimelinePlaybackState.Playing;

        // --------------------------------------------------------------------
        // Unity event handling
        // --------------------------------------------------------------------
        protected void Start()
        {
            InitializeUI();
        }

        protected void Update()
        {
            SyncUI();
        }

        protected void OnDestroy()
        {
            TeardownUI();
        }

        // --------------------------------------------------------------------
        // UI setup & teardown
        // --------------------------------------------------------------------
        private void InitializeUI()
        {
            m_timelineSlider.onValueChanged.AddListener(OnTimelineSliderValueChanged);
        }

        private void TeardownUI()
        {
            m_timelineSlider.onValueChanged.RemoveListener(OnTimelineSliderValueChanged);
        }

        // --------------------------------------------------------------------
        // UI Synchronization
        // --------------------------------------------------------------------
        private void SyncUI()
        {
            TimelineConfig timelineConfig = m_streamController.GetTimeline().GetConfig();
            SyncTimeline(timelineConfig);
            SyncSliderBackground(timelineConfig);
        }

         private void SyncSliderBackground(TimelineConfig config)
        {
            switch (config.m_playbackState)
            {
                case TimelinePlaybackState.Seeking:
                case TimelinePlaybackState.Paused:
                    {
                        m_sliderBackground.material = m_loadedMaterial;
                        break;
                    }
                case TimelinePlaybackState.Buffering:
                    {
                        m_sliderBackground.material = m_loadingMaterial;
                        break;
                    }
                case TimelinePlaybackState.Playing:
                    {
                        m_sliderBackground.material = m_loadedMaterial;
                        break;
                    }
            }
        }

        private bool assetIsAnimated()
        {
            m_streamController.GetTimeline().GetTimeRange(out Timecode startTime, out Timecode endTime);

            return endTime.m_frames > startTime.m_frames;
        }

        private void SyncTimeline(TimelineConfig config)
        {
            // Set time range
            m_streamController.GetTimeline().GetTimeRange(out Timecode startTime, out Timecode endTime);
            m_timelineSlider.minValue = startTime.m_frames;
            m_timelineSlider.maxValue = endTime.m_frames;

            // Set current frame
            Timecode currentTime = m_streamController.GetTimeline().GetCurrentTime();
            if (config.m_playbackState != TimelinePlaybackState.Seeking)
            {
                m_timelineSlider.SetValueWithoutNotify(currentTime.m_frames);
            }

            m_timelineSlider.gameObject.SetActive(assetIsAnimated());
        }

        // --------------------------------------------------------------------
        // UI event handling
        // --------------------------------------------------------------------
        private bool IsScrubbingTimeline()
        {
            return m_timelineSlider.gameObject.GetComponent<TouchSliderExpand>().IsScrubbing();
        }

        public void ResumePlayBack()
        {
            TimelineConfig config = m_streamController.GetTimeline().GetConfig();
            config.m_playbackState = TimelinePlaybackState.Playing;
            m_streamController.GetTimeline().SetConfig(config);
        }

        public void OnPlaybackStateButtonClicked()
        {
            if(!assetIsAnimated())
            {
                return;
            }

            TimelineConfig config = m_streamController.GetTimeline().GetConfig();

            switch (config.m_playbackState)
            {
                case TimelinePlaybackState.Playing:
                {
                    config.m_playbackState = TimelinePlaybackState.Paused;
                    break;
                }
                case TimelinePlaybackState.Paused:
                {
                    config.m_playbackState = TimelinePlaybackState.Playing;
                    break;
                }
                default:
                {
                    // No-op
                    return;
                }
            }

            m_streamController.GetTimeline().SetConfig(config);
        }

        private void OnTimelineSliderValueChanged(float value)
        {
            TimelineConfig config = m_streamController.GetTimeline().GetConfig();

            // Store state to restore to after a small window of time.
            if (
                config.m_playbackState == TimelinePlaybackState.Playing
                || config.m_playbackState == TimelinePlaybackState.Paused
            )
            {
                m_stateToRestoreAfterSeek = config.m_playbackState;
            }

            config.m_playbackState = TimelinePlaybackState.Seeking;
            m_streamController.GetTimeline().SetConfig(config);

            if (!IsScrubbingTimeline())
            {
                if (m_resetSeekStateCoroutine != null)
                {
                    StopCoroutine(m_resetSeekStateCoroutine);
                    m_resetSeekStateCoroutine = null;
                }
                m_resetSeekStateCoroutine = StartCoroutine(ResetSeekState(0.1f));
            }

            Timecode newTime = new();
            newTime.m_frames = (int)value;
            newTime.m_framesPerSecond = config.m_framesPerSecond;

            m_streamController.GetTimeline().SeekToTime(newTime);
        }

        private IEnumerator ResetSeekState(float secondsLater)
        {
            yield return new WaitForSeconds(secondsLater);
            TimelineConfig config = m_streamController.GetTimeline().GetConfig();
            config.m_playbackState = m_stateToRestoreAfterSeek;
            m_streamController.GetTimeline().SetConfig(config);
        }
    }
}
