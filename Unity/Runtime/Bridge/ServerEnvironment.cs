// Copyright © 2026 Miris, Inc. All rights reserved.

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
    using SetServerEnvironmentCallback = void(*)(bool success, void* userData);
#else
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void SetServerEnvironmentCallback(bool success, System.IntPtr userData);
#endif

#if __cplusplus
    using AddStreamCallback = void(*)(int objectId, void* userData);
#else
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void AddStreamCallback(int objectId, System.IntPtr userData);
#endif

#if USING_CSHARP
}
#endif
