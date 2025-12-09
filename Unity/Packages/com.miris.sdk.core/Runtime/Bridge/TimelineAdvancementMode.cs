// Copyright © 2025 Miris, Inc. All rights reserved.

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

public enum TimelineAdvancementMode
{
    RealTime = 0,
}

#if USING_CSHARP
} // Miris.Runtime
#else
;
#undef public 
#endif
