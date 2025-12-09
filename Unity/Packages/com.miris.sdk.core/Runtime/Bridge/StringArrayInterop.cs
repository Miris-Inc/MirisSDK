// Copyright © 2025 Miris, Inc. All rights reserved.

// C# Standard library
using AOT;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

// Unity packages
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The functionality in this file is subject to change as the scene API evolves.

namespace Miris.Runtime
{
    public class StringArrayInterop : IDisposable
    {
        private string[] m_managedStrings;
        private IntPtr[] m_stringPtrs;
        private IntPtr m_unmanagedArray;

        public StringArrayInterop(string[] strings)
        {
            m_managedStrings = strings;

            m_stringPtrs = new IntPtr[m_managedStrings.Length];
            for (int i = 0; i < m_managedStrings.Length; i++)
            {
                // Convert string to unmanaged ANSI string
                m_stringPtrs[i] = Marshal.StringToHGlobalAnsi(m_managedStrings[i]);
            }

            m_unmanagedArray = Marshal.AllocHGlobal(IntPtr.Size * m_managedStrings.Length);
            Marshal.Copy(m_stringPtrs, 0, m_unmanagedArray, m_managedStrings.Length);
        }

        public IntPtr GetUnmanagedStringArray()
        {
            return m_unmanagedArray;
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(m_unmanagedArray);
            for (int i = 0; i < m_stringPtrs.Length; i++)
            {
                Marshal.FreeHGlobal(m_stringPtrs[i]);
            }
        }
    }
}
