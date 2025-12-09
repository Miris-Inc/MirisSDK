// Copyright © 2025 Miris, Inc. All rights reserved.

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


    // Developer Note:
    //
    // C# struct do not support default initializers in 9.0, nor do they support
    // parameterless constructors, thus we have defined LodRefinementParameters defaults
    // in MirisStreamController.m_lodRefinementParameters.
    //
    // Unfortunately we need to duplicate those defaults in C++ so sane defaults are initialized
    // for unit tests.

    /// -------------------------
    /// synced scene metadata struct
    /// -------------------------
#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
#endif
    public struct SceneMetadata 
    {

#if __cplusplus
        SceneMetadata()
         : m_highestLodLimit(0.8f)
        , m_lowestLodLimit(0.0f)
        , m_lodMaxDistance(20.0f)
        , m_verticalOffset(0.0f)
        , m_splatCountBudget(1000000)
        {
            std::memset(m_version, 0, sizeof(m_version));
        }
        void reset(){
            m_highestLodLimit = 0.8f;
            m_lowestLodLimit = 0.0f;
            m_lodMaxDistance = 20.0f;
            m_verticalOffset = 0.0f;
            m_splatCountBudget = 1000000;
            std::memset(m_version, 0, sizeof(m_version));
        }

#endif

#if USING_CSHARP
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string m_version;
#else
        char m_version[256];
#endif

        public float m_highestLodLimit;
        public float m_lowestLodLimit;
        public float m_lodMaxDistance;
        public float m_verticalOffset;
        public int m_splatCountBudget;

    };

    /// -------------------------
    /// synced asset metadata struct
    /// -------------------------
#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
#endif

    public struct AssetMetadata 
    {

#if __cplusplus
        AssetMetadata()
        {
            std::memset(m_version, 0, sizeof(m_version));
        }
        void reset(){
            
            std::memset(m_version, 0, sizeof(m_version));
        }
#endif
        


#if USING_CSHARP

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string m_version;
    #else
        char m_version[256];
    #endif

    };



#if USING_CSHARP
} // Miris.Runtime
#else

#undef public
#endif
