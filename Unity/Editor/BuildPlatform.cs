// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using UnityEngine;
using UnityEditor;

namespace Miris.Editor
{
    // Match target platforms enumerated in $AQUA_ROOT/aqua_cmd.py
    // Also see GetUnityBuildTarget()
    public enum BuildPlatform : int
    {
        android,
        linux,
        osx,
        windows,
        ios,
        visionos
    }

    static public class BuildPlatformExtensions
    {
        static public string GetLibraryFolderName(this BuildPlatform buildPlatform)
        {
            return buildPlatform.ToString();
        }

        static public string GetPlatformSpecificLibraryFileName(this BuildPlatform buildPlatform, string libraryBaseName)
        {
            return $"{buildPlatform.GetLibraryPrefix()}{libraryBaseName}{buildPlatform.GetLibraryExtension()}";
        }

        static public string GetLibraryExtension(this BuildPlatform buildPlatform)
        {
            return buildPlatform switch
            {
                BuildPlatform.linux => ".so",
                BuildPlatform.android => ".so",
                BuildPlatform.osx => ".dylib",
                BuildPlatform.ios => ".xcframework",
                BuildPlatform.visionos => ".xcframework",
                BuildPlatform.windows => ".dll",
                _ => throw new ArgumentOutOfRangeException(
                    $"{nameof(BuildPlatform)}.{buildPlatform.ToString()} is an un-supported build platform"
                )
            };
        }

        static public string GetLibraryPrefix(this BuildPlatform buildPlatform)
        {
            return buildPlatform switch
            {
                BuildPlatform.linux => "lib",
                BuildPlatform.android => "lib",
                BuildPlatform.osx => "lib",
                BuildPlatform.ios => "",
                BuildPlatform.visionos => "",
                BuildPlatform.windows => "",
                _ => throw new ArgumentOutOfRangeException(
                    $"{nameof(BuildPlatform)}.{buildPlatform.ToString()} is an un-supported build platform"
                )
            };
        }

        static public BuildTarget GetUnityBuildTarget(this BuildPlatform buildPlatform)
        {
            return buildPlatform switch
            {
                BuildPlatform.linux => BuildTarget.StandaloneLinux64,
                BuildPlatform.android => BuildTarget.Android,
                BuildPlatform.osx => BuildTarget.StandaloneOSX,
                BuildPlatform.ios => BuildTarget.iOS,
                BuildPlatform.visionos => BuildTarget.VisionOS,
                BuildPlatform.windows => BuildTarget.StandaloneWindows64,
                _ => throw new ArgumentOutOfRangeException(
                    $"{nameof(BuildPlatform)}.{buildPlatform.ToString()} is an un-supported build platform"
                )
            };
        }

        static public BuildPlatform GetCurrentEditorPlatform()
        {
            return Application.platform switch
            {
                RuntimePlatform.LinuxEditor => BuildPlatform.linux,
                RuntimePlatform.WindowsEditor => BuildPlatform.windows,
                RuntimePlatform.OSXEditor => BuildPlatform.osx,
                _ => throw new ArgumentOutOfRangeException(
                    $"{nameof(RuntimePlatform)}.{Application.platform.ToString()} is an un-supported build platform"
                )
            };
        }

        static public BuildPlatform GetCurrentTargetPlatform()
        {
            return EditorUserBuildSettings.activeBuildTarget switch
            {
                BuildTarget.StandaloneLinux64 => BuildPlatform.linux,
                BuildTarget.Android => BuildPlatform.android,
                BuildTarget.StandaloneWindows64 => BuildPlatform.windows,
                BuildTarget.StandaloneOSX => BuildPlatform.osx,
                BuildTarget.iOS => BuildPlatform.ios,
                BuildTarget.VisionOS => BuildPlatform.visionos,
                _ => throw new ArgumentOutOfRangeException(
                    $"{nameof(BuildTarget)}.{EditorUserBuildSettings.activeBuildTarget.ToString()} is an un-supported target platform"
                )
            };
        }
    }

}
