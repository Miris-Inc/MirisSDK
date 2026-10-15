// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Miris.Runtime
{
    using ContextHandle = IntPtr;

    /// <summary>
    /// Context API Object used by our C# code 
    /// </summary>
    public class Context : IDisposable
    {
#if UNITY_EDITOR
        public static string DoNotWarnAgainPrefsKey = "MirisClient_DoNotWarnAgain";
#endif

        private ContextHandle m_handle = System.IntPtr.Zero;

        /// <summary>
        /// Meant for Miris-internal use.
        /// </summary>
        public ContextHandle? GetContextHandleInternal()
        {
            if (m_handle == System.IntPtr.Zero)
            {
                return null;
            }

            return m_handle;
        }

        public Context()
        {
            MirisDebug.Log("Creating Context");
            try
            {
                m_handle = MirisBindings.CreateAquaContext();
            }
            catch (DllNotFoundException ex)
            {
                Debug.LogError($"Failed to create Context: Native library not found. {ex.Message}");
                m_handle = System.IntPtr.Zero;

#if UNITY_EDITOR
                bool ignoreWarning = EditorPrefs.GetBool(DoNotWarnAgainPrefsKey, false);
                if (ignoreWarning)
                {
                    return;
                }

                int option = EditorUtility.DisplayDialogComplex(
                    "Miris SDK - Native Library Missing",
                    #if MIRIS_INTERNAL
                    "The Miris native library was not found - the Miris SDK will not function until it is installed or built.\n\n" +
                    "Contact our DevOps team if you have trouble building the native library.\n\n" +
                    "You can download the native library using the Miris Release Downloader.\n\n" +
                    #else
                    "The Miris native library was not found - the Miris SDK will not function until it is installed.\n\nYou can download the native library using the Miris Release Downloader.\n\n" +
                    #endif
                    "Details: " + ex.Message,
                    "OK",
                    "Open Release Downloader",
                    "Do not show again");

                switch (option)
                {
                    // OK
                    case 0:
                        // Do nothing
                        break;
                    // "Open Release Downloader"
                    case 1:
                        UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Miris/Platform Downloader");
                        break;
                    // "Do not show again"
                    case 2:
                        EditorPrefs.SetBool(DoNotWarnAgainPrefsKey, true);
                        break;
                    default:
                        break;
                }
#endif
                throw;
            }
        }

        public void Dispose()
        {
            if (m_handle != System.IntPtr.Zero)
            {
                MirisDebug.Log("Destroying Context");
                MirisBindings.DestroyAquaContext(m_handle);
                m_handle = System.IntPtr.Zero;
            }
        }

        public void BeginFrame(double previousFrameTimeMs)
        {
            MirisBindings.BeginFrame(m_handle, previousFrameTimeMs);
        }
    }
}
