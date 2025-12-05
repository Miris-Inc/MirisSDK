#if __cplusplus
#define public
#else
#define USING_CSHARP
#endif

#if USING_CSHARP

using System;
using System.Runtime.InteropServices;

namespace Miris.Runtime
{
#endif

#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
#endif

    public struct Timecode
    {

        // Keep the types in sync between C++ and C#.
#if USING_CSHARP
        public int m_frames;
        public int m_framesPerSecond;
#else 
        using Frames = int;
        using FramesPerSecond = int;
        Frames m_frames;
        FramesPerSecond m_framesPerSecond;
#endif

#if __cplusplus
        static constexpr FramesPerSecond c_defaultFramesPerSecond = 60;

        Timecode(Frames frames=0, FramesPerSecond framesPerSecond=c_defaultFramesPerSecond)
            : m_frames(frames)
            , m_framesPerSecond(framesPerSecond)
        {
        }

        // Convenience for conversion from Unity's deltaTIme
        static Timecode fromSeconds(double seconds, FramesPerSecond framesPerSecond)
        {
            Frames frames = std::round(seconds * framesPerSecond); // round to nearest frame
            return Timecode(frames, framesPerSecond);
        }

        double toSeconds() const
        {
            return static_cast<double>(m_frames) / static_cast<double>(m_framesPerSecond);
        }

        /// Create a Timecode with a newly specified frames-per-second while preserving
        /// the temporal duration.
        Timecode withFramesPerSecond(const FramesPerSecond newFramesPerSecond) const
        {
            Frames newFrames = std::round(toSeconds() * static_cast<double>(newFramesPerSecond));
            return Timecode(newFrames, newFramesPerSecond);
        }

        Timecode operator%(const Timecode& other) const
        {
            checkFrameRateMatch(other);
            int modFrames = m_frames % other.m_frames;
            return Timecode(modFrames, m_framesPerSecond);
        }

        bool operator==(const Timecode& other) const
        {
            return m_frames == other.m_frames && m_framesPerSecond == other.m_framesPerSecond;
        }

        bool operator!=(const Timecode& other) const
        {
            return !(*this == other);
        }

        bool operator<(const Timecode& other) const
        {
            checkFrameRateMatch(other);
            return m_frames < other.m_frames;
        }

        bool operator>(const Timecode& other) const
        {
            return other < *this;
        }

        bool operator<=(const Timecode& other) const
        {
            return !(other < *this);
        }

        bool operator>=(const Timecode& other) const
        {
            return !(*this < other);
        }

        Timecode operator+(const Timecode& other) const
        {
            checkFrameRateMatch(other);
            return Timecode(m_frames + other.m_frames, m_framesPerSecond);
        }

        Timecode operator-(const Timecode& other) const
        {
            checkFrameRateMatch(other);
            return Timecode(m_frames - other.m_frames, m_framesPerSecond);
        }

        void checkFrameRateMatch(const Timecode& other) const 
        {
            AQUA_ASSERT_MSG(m_framesPerSecond == other.m_framesPerSecond, "Frame rates must match for comparison");
        }

#endif

#if USING_CSHARP
        public string ToTimecodeString()
        {
            int totalSeconds = m_frames / m_framesPerSecond;
            int remainingFrames = m_frames % m_framesPerSecond;

            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            return string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D2}", hours, minutes, seconds, remainingFrames);
        }
#endif
    }
#if USING_CSHARP
}
#else
;
#undef public
#endif
