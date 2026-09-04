// Copyright © 2026 Miris, Inc. All rights reserved.

#nullable enable

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;

namespace Miris.Editor
{
    public class BuildScript
    {
        // Set only for the duration of a visionOS simulator build. Runtime code uses it to work
        // around limits the simulator has and the device does not - see GPUResourceTracker.
        const string SimulatorDefine = "MIRIS_VISIONOS_SIMULATOR";

        /// <summary>
        /// Applies platform-specific preprocessor defines from AQUA_PLATFORM_DEFINES env var.
        /// Called before each build to ensure correct defines are set.
        /// </summary>
        private static void ApplyPlatformDefines(BuildTarget buildTarget)
        {
            var definesEnv = Environment.GetEnvironmentVariable("AQUA_PLATFORM_DEFINES");
            if (string.IsNullOrEmpty(definesEnv))
                return;

            var namedBuildTarget = buildTarget switch
            {
                BuildTarget.Android => NamedBuildTarget.Android,
                BuildTarget.iOS => NamedBuildTarget.iOS,
                BuildTarget.StandaloneOSX => NamedBuildTarget.Standalone,
                BuildTarget.StandaloneWindows64 => NamedBuildTarget.Standalone,
                BuildTarget.StandaloneLinux64 => NamedBuildTarget.Standalone,
                BuildTarget.VisionOS => NamedBuildTarget.VisionOS,
                _ => NamedBuildTarget.Unknown
            };

            if (namedBuildTarget == NamedBuildTarget.Unknown)
                return;

            var currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            var defineSet = new HashSet<string>(
                currentDefines.Split(';', StringSplitOptions.RemoveEmptyEntries)
            );

            var newDefines = definesEnv.Split(';', StringSplitOptions.RemoveEmptyEntries);
            int addedCount = 0;
            foreach (var define in newDefines)
            {
                if (defineSet.Add(define))
                    addedCount++;
            }

            if (addedCount > 0)
            {
                var updatedDefines = string.Join(";", defineSet);
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, updatedDefines);
                Debug.Log($"[BuildScript] Applied {addedCount} platform defines: {definesEnv}");
            }
        }

        protected class BuildContext
        {
            public BuildContext()
            {
                foreach (var scene in EditorBuildSettings.scenes)
                {
                    if (scene.enabled)
                    {
                        Scenes.Add(scene.path);
                    }
                }

                BaseBuildPath = Environment.GetEnvironmentVariable("AQUA_ROOT");
                ProjectName = Environment.GetEnvironmentVariable("AQUA_PROJECT");
            }

            public bool IsValid()
            {
                if (BaseBuildPath == null)
                {
                    Debug.LogError("AQUA_PROJECT environment variable not set");
                    return false;
                }

                return true;
            }

            public List<string> Scenes = new List<string>();
            public string? BaseBuildPath;
            public string? ProjectName;
        }

        static void MetaQuestBuild()
        {
            BuildContext bc = new BuildContext();
            if (!bc.IsValid())
            {
                return;
            }

            ApplyPlatformDefines(BuildTarget.Android);
            string buildPath = Path.Combine(bc.BaseBuildPath, "build-output", "apps", "mq3", $"{bc.ProjectName}.apk");
            BuildPipeline.BuildPlayer(bc.Scenes.ToArray(), buildPath, BuildTarget.Android, BuildOptions.None);
        }

        static void AndroidBuild(bool buildAAB = false)
        {
            bool oldUserSettings = EditorUserBuildSettings.buildAppBundle;

            BuildContext bc = new BuildContext();
            string extension = buildAAB ? ".aab" : ".apk";

            if (!bc.IsValid())
            {
                return;
            }

            ApplyPlatformDefines(BuildTarget.Android);
            string buildPath = Path.Combine(bc.BaseBuildPath, "build-output", "apps", "android", $"{bc.ProjectName}{extension}");
            EditorUserBuildSettings.buildAppBundle = buildAAB;
            BuildPipeline.BuildPlayer(bc.Scenes.ToArray(), buildPath, BuildTarget.Android, BuildOptions.None);
            EditorUserBuildSettings.buildAppBundle = oldUserSettings;
        }

        static void AndroidBuildAAB()
        {
            AndroidBuild(true);
        }

        static void AndroidBuildAPK()
        {
            AndroidBuild(false);
        }

        static void IosBuild()
        {
            BuildContext bc = new BuildContext();
            string buildPath;
            if (bc.BaseBuildPath == null)
            {
                Debug.LogWarning("AQUA_ROOT environment variable not set");
                buildPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), bc.ProjectName);
            }
            else
            {
                buildPath = Path.Combine(bc.BaseBuildPath, "build-output", "apps", "ios", bc.ProjectName);
            }

            ApplyPlatformDefines(BuildTarget.iOS);
            BuildPipeline.BuildPlayer(bc.Scenes.ToArray(), buildPath, BuildTarget.iOS, BuildOptions.None);
        }

        static void IosSimulatorBuild()
        {
            var oldSDKVersion = PlayerSettings.iOS.sdkVersion;

            BuildContext bc = new BuildContext();
            string buildPath;
            if (bc.BaseBuildPath == null)
            {
                Debug.LogWarning("AQUA_ROOT environment variable not set");
                buildPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), bc.ProjectName);
            }
            else
            {
                buildPath = Path.Combine(bc.BaseBuildPath, "build-output", "apps", "ios-simulator", bc.ProjectName);
            }

            ApplyPlatformDefines(BuildTarget.iOS);
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            BuildPipeline.BuildPlayer(bc.Scenes.ToArray(), buildPath, BuildTarget.iOS, BuildOptions.None);

            PlayerSettings.iOS.sdkVersion = oldSDKVersion;
        }

        static void WindowsBuild()
        {
            BuildContext bc = new BuildContext();
            if (!bc.IsValid())
            {
                return;
            }

            ApplyPlatformDefines(BuildTarget.StandaloneWindows64);
            string buildPath = Path.Combine(bc.BaseBuildPath, "build-output", "apps", "windows", $"{bc.ProjectName}.exe");
            BuildPipeline.BuildPlayer(bc.Scenes.ToArray(), buildPath, BuildTarget.StandaloneWindows64, BuildOptions.None);
        }

        static void MacOSBuild()
        {
            BuildContext bc = new BuildContext();
            if (!bc.IsValid())
            {
                return;
            }

            ApplyPlatformDefines(BuildTarget.StandaloneOSX);
            string buildPath = Path.Combine(bc.BaseBuildPath, "build-output", "apps", "osx", $"{bc.ProjectName}.app");
            BuildPipeline.BuildPlayer(bc.Scenes.ToArray(), buildPath, BuildTarget.StandaloneOSX, BuildOptions.None);
        }

        static void LinuxBuild()
        {
            BuildContext bc = new BuildContext();
            if (!bc.IsValid())
            {
                return;
            }

            ApplyPlatformDefines(BuildTarget.StandaloneLinux64);
            string buildPath = Path.Combine(bc.BaseBuildPath, "build-output", "apps", "linux", bc.ProjectName);
            BuildPipeline.BuildPlayer(bc.Scenes.ToArray(), buildPath, BuildTarget.StandaloneLinux64, BuildOptions.None);
        }

        static void VisionOSBuild()
        {
            BuildContext bc = new BuildContext();
            if (!bc.IsValid())
            {
                return;
            }

            ApplyPlatformDefines(BuildTarget.VisionOS);
            // Set rather than assumed: a simulator build that dies before its finally runs leaves
            // sdkVersion on Simulator, and inheriting that silently produces a device build Xcode
            // will not offer a real headset for.
            PlayerSettings.VisionOS.sdkVersion = VisionOSSdkVersion.Device;
            string buildPath = Path.Combine(bc.BaseBuildPath, "build-output", "apps", "visionos", bc.ProjectName);
            BuildPipeline.BuildPlayer(bc.Scenes.ToArray(), buildPath, BuildTarget.VisionOS, BuildOptions.None);
        }

        static void VisionOSSimulatorBuild()
        {
            BuildContext bc = new BuildContext();
            if (!bc.IsValid())
            {
                return;
            }

            // Both of these are restored in the finally. VisionOSBuild sets neither, so anything
            // left behind by a failed build silently changes the next device build - a stale
            // sdkVersion would make it a simulator build, and a stale MIRIS_VISIONOS_SIMULATOR
            // would compile the simulator's GPU workarounds into a device player.
            VisionOSSdkVersion oldSdkVersion = PlayerSettings.VisionOS.sdkVersion;
            try
            {
                // Before the defines are captured, since it adds to them: what is restored below
                // is then exactly what a device build would also leave behind.
                ApplyPlatformDefines(BuildTarget.VisionOS);
                string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.VisionOS);
                try
                {
                    PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.VisionOS,
                                                             $"{defines};{SimulatorDefine}");
                    PlayerSettings.VisionOS.sdkVersion = VisionOSSdkVersion.Simulator;
                    string buildPath = Path.Combine(bc.BaseBuildPath, "build-output", "apps", "visionos-simulator", bc.ProjectName);
                    BuildPipeline.BuildPlayer(bc.Scenes.ToArray(), buildPath, BuildTarget.VisionOS, BuildOptions.None);
                }
                finally
                {
                    PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.VisionOS, defines);
                }
            }
            finally
            {
                PlayerSettings.VisionOS.sdkVersion = oldSdkVersion;
            }
        }
    }
}
