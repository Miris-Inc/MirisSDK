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

#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
#endif
    public struct LodRefinementParameters
    {

        // Developer Note:
        //
        // C# struct does not support default initializers in 9.0, nor do they support
        // parameterless constructors, thus we have defined LodRefinementParameters defaults
        // in MirisStreamController.m_lodRefinementParameters.
        //
        // Unfortunately we need to duplicate those defaults in C++ so sane defaults are initialized
        // for unit tests.

#if __cplusplus
        LodRefinementParameters()
            : m_lodSelectionMode(LodSelectionMode::Distance)
            , m_lodRequestPriority(LodRequestPriority::CentralityProximity)
            , m_targetFramesPerSecond(72.0f)
            , m_graphicsLodCalibratorType(GraphicsLodCalibratorType::Disabled)
            , m_graphicsLodCalibratorInfluence(0.5f)
            , m_lowestLodLimit(0.0f)
            , m_highestLodLimit(0.8f)
            , m_lodMaxDistance(20.0f)
            , m_lodUpdateDistance(5.0f)
            , m_lodUpdateRotation(20.0f)
            , m_fixedLodIndex(0)
            , m_splatCountBudget(1000000)
        {
            // TODO: if we ever add more heavy-weight members (like containers), we may want to
            // generate a hash when the data is updated, which is then used for comparison.
        }

        bool operator==(const LodRefinementParameters& other) const {
            return (
                m_lodSelectionMode == other.m_lodSelectionMode &&
                m_lodRequestPriority == other.m_lodRequestPriority &&
                m_targetFramesPerSecond == other.m_targetFramesPerSecond &&
                m_graphicsLodCalibratorType == other.m_graphicsLodCalibratorType &&
                m_graphicsLodCalibratorInfluence == other.m_graphicsLodCalibratorInfluence &&
                m_lowestLodLimit == other.m_lowestLodLimit &&
                m_highestLodLimit == other.m_highestLodLimit &&
                m_lodMaxDistance == other.m_lodMaxDistance &&
                m_lodUpdateDistance == other.m_lodUpdateDistance &&
                m_lodUpdateRotation == other.m_lodUpdateRotation &&
                m_fixedLodIndex == other.m_fixedLodIndex &&
                m_splatCountBudget == other.m_splatCountBudget
            );
        }

        bool operator!=(const LodRefinementParameters& other) const {
            return !(*this == other);
        }

#endif

        public LodSelectionMode m_lodSelectionMode;

        // Defines the order in which we request lods to be streamed.
        public LodRequestPriority m_lodRequestPriority;

        // ----------------------------------
        // LOD Graphics Calibration params
        // ----------------------------------

        public GraphicsLodCalibratorType m_graphicsLodCalibratorType;


#if USING_CSHARP
        [Range(0.0f, 1.0f)]
#endif
        public float m_graphicsLodCalibratorInfluence;

        // The target frame rate for our application, used by the graphics lod calibrator

#if USING_CSHARP
        [Range(24.0f, 120.0f)]
#endif
        public float m_targetFramesPerSecond;

        // ----------------------------------
        // LodSelectionMode::Distance Params
        // ----------------------------------

        // Normalized range (0...1) that determines which LODs are considered
#if USING_CSHARP
        [Range(0.0f, 1.0f)]
#endif
        public float m_lowestLodLimit;

#if USING_CSHARP
        [Range(0.0f, 1.0f)]
#endif
        public float m_highestLodLimit;

        // Maximum distance to consider for the distance based LOD curve.  Any chunks
        // past this distance will be the lowest LOD
#if USING_CSHARP
        [Range(1.0f, 200.0f)]
#endif
        public float m_lodMaxDistance;

        // Distance traveled before triggering another LOD update
#if USING_CSHARP
        [Range(0.01f, 10.0f)]
#endif
        public float m_lodUpdateDistance;

        // Cosine of the angle between the camera can turn before triggering another LOD update
#if USING_CSHARP
        [Range(1.0f, 90.0f)]
#endif
        public float m_lodUpdateRotation;

        // ----------------------------------
        // LodSelectionMode::Fixed Params
        // ----------------------------------
#if USING_CSHARP
        [Range(1.0f, 20000000.0f)]
#endif
        public int m_splatCountBudget;
#if USING_CSHARP
        [Range(0.0f, 15.0f)]
#endif
        public int m_fixedLodIndex;
    }
#if USING_CSHARP
}
#else
;
#undef public
#endif
