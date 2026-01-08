// Copyright © 2025 Miris, Inc. All rights reserved.

// This is a valid C++ and C# file :)

#if __cplusplus
#define public 
#else
#define USING_CSHARP
#endif 

#if USING_CSHARP
using System.Runtime.InteropServices;
namespace Miris.Runtime {
#endif

    // Provides information about the current game frame.
#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
#endif
    public struct FrameInfo
    {

    // C# struct does not support default initializers in 9.0
#if __cplusplus
        public FrameInfo() 
            : m_deltaTimeSeconds(0)
            , m_frameCount(0)
            , m_splatCount(0) {
        }
#endif

        public FrameInfo(float deltaTimeSeconds, int frameCount, int splatCount)
        {
            m_deltaTimeSeconds = deltaTimeSeconds;
            m_frameCount = frameCount;
            m_splatCount = splatCount;
        }

        // Time in seconds, since the last frame.
        public float m_deltaTimeSeconds;

        // Number of frames since the start of the game.
        public int m_frameCount;

        // Number of active splats in the renderer
        public int m_splatCount;
    }
#if USING_CSHARP
}
#else
;
#undef public 
#endif
