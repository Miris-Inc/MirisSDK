using System;
using UnityEngine;
using UnityEditor;

namespace Aqua.Editor
{
    // Match target platforms enumerated in $AQUA_ROOT/aqua_cmd.py
    // Also see GetUnityBuildTarget()
    public enum AquaBuildPlatform : int
    {
        android,
        linux,
        osx,
        windows,
        ios
    }

    static public class AquaBuildPlatformExtensions
    {
        static public string GetLibraryFolderName(this AquaBuildPlatform buildPlatform)
        {
            return buildPlatform.ToString();
        }

        static public string GetPlatformSpecificLibraryFileName(this AquaBuildPlatform buildPlatform, string libraryBaseName)
        {
            return $"{buildPlatform.GetLibraryPrefix()}{libraryBaseName}{buildPlatform.GetLibraryExtension()}";
        }

        static public string GetLibraryExtension(this AquaBuildPlatform buildPlatform)
        {
            return buildPlatform switch
            {
                AquaBuildPlatform.linux => ".so",
                AquaBuildPlatform.android => ".so",
                AquaBuildPlatform.osx => ".dylib",
                AquaBuildPlatform.ios => ".xcframework",
                AquaBuildPlatform.windows => ".dll",
                _ => throw new ArgumentOutOfRangeException(
                    $"{nameof(AquaBuildPlatform)}.{buildPlatform.ToString()} is an un-supported build platform"
                )
            };
        }

        static public string GetLibraryPrefix(this AquaBuildPlatform buildPlatform)
        {
            return buildPlatform switch
            {
                AquaBuildPlatform.linux => "lib",
                AquaBuildPlatform.android => "lib",
                AquaBuildPlatform.osx => "lib",
                AquaBuildPlatform.ios => "",
                AquaBuildPlatform.windows => "",
                _ => throw new ArgumentOutOfRangeException(
                    $"{nameof(AquaBuildPlatform)}.{buildPlatform.ToString()} is an un-supported build platform"
                )
            };
        }

        static public BuildTarget GetUnityBuildTarget(this AquaBuildPlatform buildPlatform)
        {
            return buildPlatform switch
            {
                AquaBuildPlatform.linux => BuildTarget.StandaloneLinux64,
                AquaBuildPlatform.android => BuildTarget.Android,
                AquaBuildPlatform.osx => BuildTarget.StandaloneOSX,
                AquaBuildPlatform.ios => BuildTarget.iOS,
                AquaBuildPlatform.windows => BuildTarget.StandaloneWindows64,
                _ => throw new ArgumentOutOfRangeException(
                    $"{nameof(AquaBuildPlatform)}.{buildPlatform.ToString()} is an un-supported build platform"
                )
            };
        }

        static public AquaBuildPlatform GetCurrentEditorPlatform()
        {
            return Application.platform switch
            {
                RuntimePlatform.LinuxEditor => AquaBuildPlatform.linux,
                RuntimePlatform.WindowsEditor => AquaBuildPlatform.windows,
                RuntimePlatform.OSXEditor => AquaBuildPlatform.osx,
                _ => throw new ArgumentOutOfRangeException(
                    $"{nameof(RuntimePlatform)}.{Application.platform.ToString()} is an un-supported build platform"
                )
            };
        }

        static public AquaBuildPlatform GetCurrentTargetPlatform()
        {
            return EditorUserBuildSettings.activeBuildTarget switch
            {
                BuildTarget.StandaloneLinux64 => AquaBuildPlatform.linux,
                BuildTarget.Android => AquaBuildPlatform.android,
                BuildTarget.StandaloneWindows64 => AquaBuildPlatform.windows,
                BuildTarget.StandaloneOSX => AquaBuildPlatform.osx,
                BuildTarget.iOS => AquaBuildPlatform.ios,
                _ => throw new ArgumentOutOfRangeException(
                    $"{nameof(BuildTarget)}.{EditorUserBuildSettings.activeBuildTarget.ToString()} is an un-supported target platform"
                )
            };
        }
    }

}
