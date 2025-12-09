// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Miris.Runtime
{
    public class Timeline
    {
#if UNITY_EDITOR
        static private double GetEditorTimeSinceStartup() => EditorApplication.timeSinceStartup;

        private double m_lastTimeEditor = 0.0;
#endif

        private Client m_client;

        internal Timeline(Client client)
        {
            m_client = client;
        }

        public TimelineConfig GetConfig()
        {
            TimelineConfig config = new();
            m_client.GetTimelineConfig(ref config);
            return config;
        }

        public void SetConfig(TimelineConfig config)
        {
            m_client.SetTimelineConfig(ref config);
        }

        public void GetTimeRange(out Timecode startTime, out Timecode endTime)
        {
            startTime = new();
            endTime = new();
            m_client.GetTimeRange(ref startTime, ref endTime);
        }

        public Timecode GetCurrentTime()
        {
            Timecode currentTime = new();
            m_client.GetTime(ref currentTime);
            return currentTime;
        }

        public void AdvanceTime()
        {
            float deltaTime = Time.deltaTime;
#if UNITY_EDITOR
            // Time.deltaTime is always 0 in Edit mode, so we use editor time since startup
            if (!Application.isPlaying)
            {
                double currentTimeEditor = GetEditorTimeSinceStartup();
                deltaTime = m_lastTimeEditor == 0.0 ? float.Epsilon : (float)(currentTimeEditor - m_lastTimeEditor);
                m_lastTimeEditor = currentTimeEditor;
            }
#endif
            m_client.AdvanceTime(deltaTime);
        }

        public void SeekToTime(Timecode newTime)
        {
            m_client.SeekToTime(ref newTime);
        }
    }
}
