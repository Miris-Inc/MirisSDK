// Copyright © 2026 Miris, Inc. All rights reserved.

// Standard library
using System;

// Unity engine
using UnityEngine;

namespace Miris.Runtime
{
    /// <summary>
    /// A settings asset loaded from a Resources folder, standing in with defaults until the project
    /// has one. The static state is per settings type.
    /// </summary>
    public abstract class MirisSettingsAsset<T> : ScriptableObject where T : MirisSettingsAsset<T>
    {
        private static T s_loaded;
        private static T s_defaults;

        public static event Action Changed;

        /// <summary>
        /// The project's settings asset at <paramref name="resourcesPath"/>, or defaults when the
        /// project has none.
        /// </summary>
        protected static T Load(string resourcesPath)
        {
            if (s_loaded == null)
            {
                s_loaded = Resources.Load<T>(resourcesPath);
            }

            if (s_loaded == null && s_defaults == null)
            {
                s_defaults = CreateInstance<T>();
                s_defaults.hideFlags = HideFlags.DontSave;
            }

            return s_loaded != null ? s_loaded : s_defaults;
        }

        public static void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
