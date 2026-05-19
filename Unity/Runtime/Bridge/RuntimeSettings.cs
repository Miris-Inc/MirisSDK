// Copyright © 2026 Miris, Inc. All rights reserved.

// This is a valid C++ and C# file :)

#if __cplusplus
#define public
#else
#define USING_CSHARP
#endif

#if USING_CSHARP

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Miris.Runtime
{
#endif

#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
#endif
    public struct RuntimeSettings
    {

        // Developer Note:
        //
        // C# struct does not support default initializers in 9.0, nor do they support
        // parameterless constructors, thus we have defined RuntimeSettings defaults
        // in MirisStreamController.m_runtimeSettings.
        //
        // Unfortunately we need to duplicate those defaults in C++ so sane defaults are initialized
        // for unit tests.

#if __cplusplus
        RuntimeSettings() :
            m_targetFramesPerSecond(72.0f)
            , m_splatCountBudget(400000)
            , m_nodeCountBudget(200)
            , m_congestionMinInflightBytes(256 * 1024)
            , m_congestionMaxInflightBytes(128 * 1024 * 1024)
            , m_xrModeActive(false)

        {
            // TODO: if we ever add more heavy-weight members (like containers), we may want to
            // generate a hash when the data is updated, which is then used for comparison.
        }

        bool operator==(const RuntimeSettings& other) const {
            return (
                m_targetFramesPerSecond == other.m_targetFramesPerSecond &&
                m_congestionMinInflightBytes == other.m_congestionMinInflightBytes &&
                m_congestionMaxInflightBytes == other.m_congestionMaxInflightBytes &&
                m_splatCountBudget == other.m_splatCountBudget &&
                m_nodeCountBudget == other.m_nodeCountBudget &&
                m_xrModeActive == other.m_xrModeActive
            );
        }

        bool operator!=(const RuntimeSettings& other) const {
            return !(*this == other);
        }

#endif

#if USING_CSHARP
        [Range(24.0f, 120.0f)]
#endif
        public float m_targetFramesPerSecond;



#if USING_CSHARP
        [Range(1.0f, 20000000.0f)]
#endif
        public int m_splatCountBudget;
#if USING_CSHARP
        [Range(1.0f, 20000.0f)]
#endif
        public int m_nodeCountBudget;

#if USING_CSHARP
        [Range(256 * 1024, 1024 * 1024 * 100)]
#endif
        public int m_congestionMinInflightBytes;

#if USING_CSHARP
        [Range(1024 * 1024, 1024 * 1024 * 500)]
#endif
        public int m_congestionMaxInflightBytes;

        // True when an immersive XR session is active
        public bool m_xrModeActive;

    }
#if USING_CSHARP
}
#else
;
#undef public
#endif
