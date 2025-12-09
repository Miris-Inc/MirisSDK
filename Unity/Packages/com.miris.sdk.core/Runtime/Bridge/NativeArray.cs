// Copyright © 2025 Miris, Inc. All rights reserved.

// This is a valid C++ and C# file :)

#if __cplusplus
#else
#define USING_CSHARP
#endif 

#if USING_CSHARP
using System.Runtime.InteropServices;
namespace Miris.Runtime {
#endif

#if __cplusplus
    using FillNativeArrayCallback = void(*)(void* ptr, int count, void* userData);
#else
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void FillNativeArrayCallback(System.IntPtr ptr, int count, System.IntPtr userData);
#endif

#if USING_CSHARP
}
#endif
