// Copyright © 2026 Miris, Inc. All rights reserved.

// Unity engine
using UnityEngine;

namespace Miris.Runtime
{
    /// <summary>
    /// One scene's viewer key, overriding the project's <see cref="MirisSettings"/> key while that
    /// scene is the active scene. Edited from the Miris > Settings window.
    /// </summary>
    public class MirisSceneSettings : MirisSceneAsset
    {
        public const string ResourcesFolder = "MirisSceneSettings";

        [SerializeField]
        [Tooltip("The asset viewer key this scene streams with, in place of the project's. Leave it "
                 + "empty to use the project's.")]
        private string m_viewerKey = "";

        /// <summary>The settings for the scene at <paramref name="scenePath"/>, or null when it has none.</summary>
        public static MirisSceneSettings Find(string scenePath)
        {
            return Find<MirisSceneSettings>(ResourcesFolder, scenePath);
        }

        /// <summary>The active scene's settings, or null when it has none.</summary>
        public static MirisSceneSettings ForActiveScene()
        {
            return Find(ActiveScenePath);
        }

        /// <summary>
        /// The viewer key the active scene streams with: its own when it has one, else the project's.
        /// Empty means the key configured in ClientConfig.
        /// </summary>
        public static string ResolveViewerKey()
        {
            MirisSceneSettings settings = ForActiveScene();
            return settings != null && !string.IsNullOrWhiteSpace(settings.m_viewerKey)
                ? settings.m_viewerKey
                : MirisSettings.Load().ViewerKey;
        }
    }
}
