#if __cplusplus
#define public
#else
#define USING_CSHARP
#endif

#if USING_CSHARP

using System;
using System.Runtime.InteropServices;

namespace Aqua.Runtime
{
#endif

#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
#endif
    public struct TimelineConfig
    {
#if __cplusplus
        TimelineConfig()
            : m_advancementMode(TimelineAdvancementMode::RealTime)
            , m_wrapMode(TimelineWrapMode::Repeat)
            , m_playbackState(TimelinePlaybackState::Playing)
            , m_playbackRate(1.0)
            , m_framesPerSecond(Timecode::c_defaultFramesPerSecond)
        {
        }
#endif
        public TimelineAdvancementMode m_advancementMode;
        public TimelineWrapMode m_wrapMode;
        public TimelinePlaybackState m_playbackState;
        public float m_playbackRate;
        public int m_framesPerSecond;
    }
#if USING_CSHARP
}
#else
;
#undef public
#endif
