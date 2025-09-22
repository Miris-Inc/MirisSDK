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

namespace Aqua.Runtime
{
#endif

    /// -------------------------
    /// changes to scene objects by ids, passed between c# and c/cpp
    /// -------------------------
#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
#endif
    public struct SceneChangeIds
    {
#if __cplusplus
        SceneChangeIds()
        {
            m_createdObjectIds = nullptr;
            m_modifiedObjectIds = nullptr;
            m_modifiedObjectFlags = nullptr;
            m_activatedObjectIds = nullptr;
            m_deactivatedObjectIds = nullptr;
            m_createdObjectsCount = 0;
            m_modifiedObjectsCount = 0;
            m_activatedObjectsCount = 0;
            m_deactivatedObjectsCount = 0;
        }
#endif

#if USING_CSHARP

        public void AllocateArrays()
        {
            m_createdObjectIds = Marshal.AllocHGlobal(sizeof(int) * m_createdObjectsCount);
            m_modifiedObjectIds = Marshal.AllocHGlobal(sizeof(int) * m_modifiedObjectsCount);
            m_modifiedObjectFlags = Marshal.AllocHGlobal(sizeof(int) * m_modifiedObjectsCount);
            m_activatedObjectIds = Marshal.AllocHGlobal(sizeof(int) * m_activatedObjectsCount);
            m_deactivatedObjectIds = Marshal.AllocHGlobal(sizeof(int) * m_deactivatedObjectsCount);
        }

        public void Free()
        {
            Marshal.FreeHGlobal(m_createdObjectIds);
            Marshal.FreeHGlobal(m_modifiedObjectIds);
            Marshal.FreeHGlobal(m_modifiedObjectFlags);
            Marshal.FreeHGlobal(m_activatedObjectIds);
            Marshal.FreeHGlobal(m_deactivatedObjectIds);
        }

        public unsafe Span<int> createdObjectIds
        {
            get
            {
                return new Span<int>((int*)m_createdObjectIds.ToPointer(), m_createdObjectsCount);
            }
        }

        public unsafe Span<int> modifiedObjectIds
        {
            get
            {
                return new Span<int>((int*)m_modifiedObjectIds.ToPointer(), m_modifiedObjectsCount);
            }
        }

        public unsafe Span<int> modifiedObjectFlags
        {
            get
            {
                return new Span<int>((int*)m_modifiedObjectFlags.ToPointer(), m_modifiedObjectsCount);
            }
        }

        public unsafe Span<int> activatedObjectIds
        {
            get
            {
                return new Span<int>((int*)m_activatedObjectIds.ToPointer(), m_activatedObjectsCount);
            }
        }

        public unsafe Span<int> deactivatedObjectIds
        {
            get
            {
                return new Span<int>((int*)m_deactivatedObjectIds.ToPointer(), m_deactivatedObjectsCount);
            }
        }
#endif

#if __cplusplus
        public int* m_createdObjectIds;
        public int* m_modifiedObjectIds;
        public int* m_modifiedObjectFlags;
        public int* m_activatedObjectIds;
        public int* m_deactivatedObjectIds;
#else
        public IntPtr m_createdObjectIds;
        public IntPtr m_modifiedObjectIds;
        public IntPtr m_modifiedObjectFlags;
        public IntPtr m_activatedObjectIds;
        public IntPtr m_deactivatedObjectIds;
#endif
        public int m_createdObjectsCount;
        public int m_modifiedObjectsCount;
        public int m_activatedObjectsCount;
        public int m_deactivatedObjectsCount;
    };

#if USING_CSHARP
} // Aqua.Runtime
#else

#undef public
#endif
