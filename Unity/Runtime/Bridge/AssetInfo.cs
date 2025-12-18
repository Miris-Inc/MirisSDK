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

    // Asset info.
#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
#endif
    public struct AssetInfo
    {
        // Asset uuid
#if __cplusplus
        char m_uuid[64] = {0};
#else
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string m_uuid;
#endif

        // Asset name
#if __cplusplus
        char m_name[256] = {0};
#else
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string m_name;
#endif

        // Absolute URL for obtaining the asset data proper
#if __cplusplus
        char m_contentUrl[4096] = {0};
#else
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4096)]
        public string m_contentUrl;
#endif

        // Absolute URL for retrieving an image suitable for use as a thumbnail preview of the asset
#if __cplusplus
        char m_thumbnailUrl[4096] = {0};
#else
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4096)]
        public string m_thumbnailUrl;
#endif
    }
    
#if USING_CSHARP
}
#else
;
#undef public
#endif
