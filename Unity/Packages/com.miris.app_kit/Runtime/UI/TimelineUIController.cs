// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;
using UnityEngine.UI;

using TMPro;

using System;
using System.Collections;

namespace Miris.Runtime
{
    public class TimelineUIController : MonoBehaviour
    {
        [SerializeField]
        private MirisStreamController m_streamController;

        [Header("Playback State")]

        [SerializeField]
        private Sprite m_playSprite;

        [SerializeField]
        private Sprite m_pauseSprite;

        [SerializeField]
        private Sprite m_bufferingSprite;

        [SerializeField]
        private Button m_playbackStateButton;
        private Image m_playbackStateImage;

        [Header("Rewind / Fast Forward")]

        [SerializeField]
        private Button m_rewindButton;

        [SerializeField]
        private Button m_fastForwardButton;

        [Header("Wrap Mode")]

        [SerializeField]
        private Sprite m_repeatSprite;

        [SerializeField]
        private Sprite m_playOnceSprite;

        [SerializeField]
        private Button m_wrapModeButton;
        private Image m_wrapModeImage;

        [Header("Playback Rate")]

        [SerializeField]
        private TMP_Dropdown m_playbackRateDropdown;

        private float[] m_playbackRateOptions = new float[] {
            -2.0f,
            -1.0f,
            -0.5f,
            0.5f,
            1.0f,
            2.0f,
            4.0f,
        };

        [Header("Timeline")]

        [SerializeField]
        private Slider m_timelineSlider;

        [SerializeField]
        private TMP_Text m_currentTimeText;

        [SerializeField]
        private TMP_Text m_endTimeText;

        Coroutine m_resetSeekStateCoroutine;

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

        static private string PlaybackRateToString(float playbackRate)
        {
            return $"{playbackRate:F1}x";
        }

        private void InitializeUI()
        {
            m_playbackStateButton.onClick.AddListener(OnPlaybackStateButtonClicked);
            m_playbackStateImage = m_playbackStateButton.GetComponent<Image>();

            m_rewindButton.onClick.AddListener(OnRewindButtonClicked);
            m_fastForwardButton.onClick.AddListener(OnFastForwardButtonClicked);

            m_wrapModeButton.onClick.AddListener(OnWrapModeButtonClicked);
            m_wrapModeImage = m_wrapModeButton.GetComponent<Image>();

            m_playbackRateDropdown.ClearOptions();
            foreach (float value in m_playbackRateOptions)
            {
                m_playbackRateDropdown.options.Add(new TMP_Dropdown.OptionData(PlaybackRateToString(value)));
            }
            m_playbackRateDropdown.onValueChanged.AddListener(OnPlaybackRateValueChanged);

            m_timelineSlider.onValueChanged.AddListener(OnTimelineSliderValueChanged);
        }

        private void TeardownUI()
        {
            m_timelineSlider.onValueChanged.RemoveListener(OnTimelineSliderValueChanged);
            m_playbackRateDropdown.onValueChanged.RemoveListener(OnPlaybackRateValueChanged);
            m_wrapModeButton.onClick.RemoveListener(OnWrapModeButtonClicked);
            m_fastForwardButton.onClick.RemoveListener(OnFastForwardButtonClicked);
            m_rewindButton.onClick.RemoveListener(OnRewindButtonClicked);
            m_playbackStateButton.onClick.RemoveListener(OnPlaybackStateButtonClicked);
        }

        // --------------------------------------------------------------------
        // UI Synchronization
        // --------------------------------------------------------------------

        private void SyncUI()
        {
            TimelineConfig timelineConfig = m_streamController.GetTimeline().GetConfig();
            SyncPlaybackStateButton(timelineConfig);
            SyncWrapModeButton(timelineConfig);
            SyncPlaybackRateDropdown(timelineConfig);
            SyncTimeline(timelineConfig);
        }

        private void SyncPlaybackStateButton(TimelineConfig config)
        {
            switch (config.m_playbackState)
            {
                case TimelinePlaybackState.Seeking:
                case TimelinePlaybackState.Paused:
                    {
                        m_playbackStateImage.sprite = m_playSprite;
                        break;
                    }
                case TimelinePlaybackState.Buffering:
                    {
                        m_playbackStateImage.sprite = m_bufferingSprite;
                        break;
                    }
                case TimelinePlaybackState.Playing:
                    {
                        m_playbackStateImage.sprite = m_pauseSprite;
                        break;
                    }
            }
        }

        private void SyncWrapModeButton(TimelineConfig config)
        {
            switch (config.m_wrapMode)
            {
                case TimelineWrapMode.PlayOnce:
                    {
                        m_wrapModeImage.sprite = m_playOnceSprite;
                        break;
                    }
                case TimelineWrapMode.Repeat:
                    {
                        m_wrapModeImage.sprite = m_repeatSprite;
                        break;
                    }
            }
        }

        private void SyncPlaybackRateDropdown(TimelineConfig config)
        {
            int index = System.Array.IndexOf(m_playbackRateOptions, config.m_playbackRate);
            m_playbackRateDropdown.SetValueWithoutNotify(index);
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

            m_currentTimeText.text = currentTime.ToTimecodeString();
            m_endTimeText.text = endTime.ToTimecodeString();
        }

        // --------------------------------------------------------------------
        // UI event handling
        // --------------------------------------------------------------------

        private void OnPlaybackStateButtonClicked()
        {
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

        private void OnRewindButtonClicked()
        {
            TimelineConfig config = m_streamController.GetTimeline().GetConfig();
            // TODO: Change this when we add support for PlayEveryFrame mode
            Debug.Assert(config.m_advancementMode == TimelineAdvancementMode.RealTime);

            // In real-time mode the rewind button will decrease the play rate
            int newIndex = Math.Min(m_playbackRateDropdown.options.Count - 1, m_playbackRateDropdown.value - 1);
            newIndex = Math.Max(0, newIndex);
            m_playbackRateDropdown.value = newIndex;
        }

        private void OnFastForwardButtonClicked()
        {
            TimelineConfig config = m_streamController.GetTimeline().GetConfig();
            // TODO: Change this when we add support for PlayEveryFrame mode
            Debug.Assert(config.m_advancementMode == TimelineAdvancementMode.RealTime);

            // In real-time mode the fast forward button will increase the play rate
            int newIndex = Math.Min(m_playbackRateDropdown.options.Count - 1, m_playbackRateDropdown.value + 1);
            newIndex = Math.Max(0, newIndex);
            m_playbackRateDropdown.value = newIndex;
        }

        private void OnWrapModeButtonClicked()
        {
            TimelineConfig config = m_streamController.GetTimeline().GetConfig();

            switch (config.m_wrapMode)
            {
                case TimelineWrapMode.PlayOnce:
                    {
                        config.m_wrapMode = TimelineWrapMode.Repeat;
                        break;
                    }
                case TimelineWrapMode.Repeat:
                    {
                        config.m_wrapMode = TimelineWrapMode.PlayOnce;
                        break;
                    }
            }

            m_streamController.GetTimeline().SetConfig(config);
        }

        private void OnPlaybackRateValueChanged(int selectedIndex)
        {
            TimelineConfig config = m_streamController.GetTimeline().GetConfig();
            config.m_playbackRate = m_playbackRateOptions[selectedIndex];
            m_streamController.GetTimeline().SetConfig(config);
        }

        private void OnTimelineSliderValueChanged(float value)
        {
            TimelineConfig config = m_streamController.GetTimeline().GetConfig();

            // Store state to restore to after a small window of time.
            if (config.m_playbackState == TimelinePlaybackState.Playing || config.m_playbackState == TimelinePlaybackState.Paused)
            {
                m_stateToRestoreAfterSeek = config.m_playbackState;
            }

            config.m_playbackState = TimelinePlaybackState.Seeking;
            m_streamController.GetTimeline().SetConfig(config);

            if (m_resetSeekStateCoroutine != null)
            {
                StopCoroutine(m_resetSeekStateCoroutine);
                m_resetSeekStateCoroutine = null;
            }
            m_resetSeekStateCoroutine = StartCoroutine(ResetSeekState(0.1f));

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
