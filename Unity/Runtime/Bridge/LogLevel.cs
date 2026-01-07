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

// Should match spdlog's levels
#if __cplusplus
enum class LogLevel
#else
public enum LogLevel
#endif
{
    Trace = 0,
    Debug,
    Info,
    Warn,
    Error, 
    Critical
}

#if USING_CSHARP
} // Miris.Runtime
#else
;
#undef public 
#endif
