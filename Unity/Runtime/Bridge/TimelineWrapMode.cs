// This is a valid C++ and C# file :)

#if __cplusplus
#define public 
#else
#define USING_CSHARP
#endif 

#if USING_CSHARP
namespace Aqua.Runtime
{
#endif

public enum TimelineWrapMode
{
    PlayOnce = 0,
    Repeat
}

#if USING_CSHARP
} // Aqua.Runtime
#else
;
#undef public 
#endif
