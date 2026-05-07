// Copyright © 2026 Miris, Inc. All rights reserved.

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

    /// -------------------------
    /// changes to scene objects by ids, passed between c# and c/cpp
    /// -------------------------
#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
#endif
    public struct SceneChangeIds
    {
#if __cplusplus
        static constexpr size_t ChangeIdsInitialAllocation = 256;

        SceneChangeIds()
        {
            m_createdObjectIds = nullptr;
            m_modifiedObjectIds = nullptr;
            m_modifiedObjectFlags = nullptr;
            m_activatedObjectIds = nullptr;
            m_deactivatedObjectIds = nullptr;
            m_deletedObjectIds = nullptr;
            m_createdObjectsCount = 0;
            m_modifiedObjectsCount = 0;
            m_activatedObjectsCount = 0;
            m_deactivatedObjectsCount = 0;
            m_deletedObjectsCount = 0;
            m_createdObjectsSize = 0;
            m_modifiedObjectsSize = 0;
            m_activatedObjectsSize = 0;
            m_deactivatedObjectsSize = 0;
            m_deletedObjectsSize = 0;
        }

        ~SceneChangeIds()
        {
            FreeArrays();
        }

        void FreeArrays()
        {
            delete[] m_createdObjectIds;     m_createdObjectIds = nullptr;
            delete[] m_modifiedObjectIds;    m_modifiedObjectIds = nullptr;
            delete[] m_modifiedObjectFlags;  m_modifiedObjectFlags = nullptr;
            delete[] m_activatedObjectIds;   m_activatedObjectIds = nullptr;
            delete[] m_deactivatedObjectIds; m_deactivatedObjectIds = nullptr;
            delete[] m_deletedObjectIds;     m_deletedObjectIds = nullptr;
        }

        void InitialAllocation(size_t initialAllocationSize)
        {
            m_createdObjectsSize = initialAllocationSize;
            m_modifiedObjectsSize = initialAllocationSize;
            m_activatedObjectsSize = initialAllocationSize;
            m_deactivatedObjectsSize = initialAllocationSize;
            m_deletedObjectsSize = initialAllocationSize;
            m_createdObjectIds = new int[m_createdObjectsSize];
            m_modifiedObjectIds = new int[m_modifiedObjectsSize];
            m_modifiedObjectFlags = new int[m_modifiedObjectsSize];
            m_activatedObjectIds = new int[m_activatedObjectsSize];
            m_deactivatedObjectIds = new int[m_deactivatedObjectsSize];
            m_deletedObjectIds = new int[m_deletedObjectsSize];
        }

        void AllocateArrays()
        {
            if(m_createdObjectIds == nullptr){
                InitialAllocation(ChangeIdsInitialAllocation);
            }

            if (m_createdObjectsCount > m_createdObjectsSize){
                int newMemorySize = m_createdObjectsCount;
                delete[] m_createdObjectIds;
                m_createdObjectIds = new int[newMemorySize];
                m_createdObjectsSize = newMemorySize;
            }

            if (m_modifiedObjectsCount > m_modifiedObjectsSize) {
                int newMemorySize = m_modifiedObjectsCount;
                delete[] m_modifiedObjectIds;
                delete[] m_modifiedObjectFlags;
                m_modifiedObjectIds = new int[newMemorySize];
                m_modifiedObjectFlags = new int[newMemorySize];
                m_modifiedObjectsSize = newMemorySize;
            }

            if (m_activatedObjectsCount > m_activatedObjectsSize){
                int newMemorySize = m_activatedObjectsCount;
                delete[] m_activatedObjectIds;
                m_activatedObjectIds = new int[newMemorySize];
                m_activatedObjectsSize = newMemorySize;
            }

            if (m_deactivatedObjectsCount > m_deactivatedObjectsSize){
                int newMemorySize = m_deactivatedObjectsCount;
                delete[] m_deactivatedObjectIds;
                m_deactivatedObjectIds = new int[newMemorySize];
                m_deactivatedObjectsSize = newMemorySize;
            }

            if (m_deletedObjectsCount > m_deletedObjectsSize){
                int newMemorySize = m_deletedObjectsCount;
                delete[] m_deletedObjectIds;
                m_deletedObjectIds = new int[newMemorySize];
                m_deletedObjectsSize = newMemorySize;
            }
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
            m_deletedObjectIds = Marshal.AllocHGlobal(sizeof(int) * m_deletedObjectsCount);
        }

        public void Free()
        {
            Marshal.FreeHGlobal(m_createdObjectIds);
            Marshal.FreeHGlobal(m_modifiedObjectIds);
            Marshal.FreeHGlobal(m_modifiedObjectFlags);
            Marshal.FreeHGlobal(m_activatedObjectIds);
            Marshal.FreeHGlobal(m_deactivatedObjectIds);
            Marshal.FreeHGlobal(m_deletedObjectIds);
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

        public unsafe Span<int> deletedObjectIds
        {
            get
            {
                return new Span<int>((int*)m_deletedObjectIds.ToPointer(), m_deletedObjectsCount);
            }
        }
#endif

#if __cplusplus
        public int* m_createdObjectIds;
        public int* m_modifiedObjectIds;
        public int* m_modifiedObjectFlags;
        public int* m_activatedObjectIds;
        public int* m_deactivatedObjectIds;
        public int* m_deletedObjectIds;
#else
        public IntPtr m_createdObjectIds;
        public IntPtr m_modifiedObjectIds;
        public IntPtr m_modifiedObjectFlags;
        public IntPtr m_activatedObjectIds;
        public IntPtr m_deactivatedObjectIds;

        public IntPtr m_deletedObjectIds;
#endif
        public int m_createdObjectsCount;
        public int m_modifiedObjectsCount;
        public int m_activatedObjectsCount;
        public int m_deactivatedObjectsCount;

        public int m_deletedObjectsCount;
        public int m_createdObjectsSize;
        public int m_modifiedObjectsSize;
        public int m_activatedObjectsSize;
        public int m_deactivatedObjectsSize;
        
        public int m_deletedObjectsSize;
    };

#if USING_CSHARP
} // Miris.Runtime
#else

#undef public
#endif
