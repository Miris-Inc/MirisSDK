// Copyright © 2026 Miris, Inc. All rights reserved.

// This is a valid C++ and C# file :)

#if __cplusplus
#define public 
#else
#define USING_CSHARP
#endif 

#if USING_CSHARP
namespace Miris.Runtime
{
#endif

public enum LodRequestPriority
{
    // LODs closer to the camera get requested first.
    Proximity = 0,

    // LODs that are in the camera viewing center AND closer to 
    // the camera position gets requested first.
    CentralityProximity
}

#if USING_CSHARP
} // Miris.Runtime
#else
;
#undef public 
#endif
