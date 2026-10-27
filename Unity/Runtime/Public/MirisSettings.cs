// Copyright © 2026 Miris, Inc. All rights reserved.

// Unity engine
using UnityEngine;

namespace Miris.Runtime
{
    /// <summary>
    /// Project-wide MirisStreamController configuration. Lives in a Resources folder so builds can
    /// load it; edit it from the Miris > Settings window.
    /// </summary>
    public class MirisSettings : MirisSettingsAsset<MirisSettings>
    {
        public const string ResourcesPath = "MirisSettings";

        [SerializeField]
        [Tooltip("Asset viewer key used by the AssetManager. Empty falls back to the key configured "
                 + "in ClientConfig.")]
        private string m_viewerKey = "";

        public static MirisSettings Load()
        {
            return Load(ResourcesPath);
        }

        public string ViewerKey => m_viewerKey;
    }
}
