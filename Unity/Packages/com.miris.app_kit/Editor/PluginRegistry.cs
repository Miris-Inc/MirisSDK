// Copyright © 2024 Miris. All rights reserved.

// Standard library
using System.Collections.Generic;
using System.IO;

// Unity Engine & Editor
using UnityEditor;
using UnityEditor.Build;

namespace Miris.Editor
{
    public static class MirisPlayerBuildHandler
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayer);
        }

        private static void BuildPlayer(BuildPlayerOptions options)
        {
            BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;

            // Anything else that needs to throw a BuildFailedException should go here
            PluginRegistry.ValidatePlugins();

            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        }
    }

    [InitializeOnLoad]
    public class PluginRegistry
    {
        private const string c_unityPluginName = "AquaUnity";

        // This static constructor is called when the Unity editor initializes or recompiles.
        static PluginRegistry()
        {
            ValidatePlugins();
        }

        static public void ValidatePlugins()
        {
            // Always require target platform libraries
            // Editor platform libraries (e.g., Windows .dll) are optional:
            // - If they exist, validate them (in case Editor scripts use native code in the future)
            // - If they don't exist, allow the build (enables cross-compilation without building editor libs)
            // This allows building Android on Windows without requiring Windows native libraries,
            // while still validating editor libraries if a developer has built them.

            string packagePath = PackageUtils.GetSdkPackagePath("core");
            HashSet<BuildPlatform> requiredBuildPlatforms = new HashSet<BuildPlatform>();

            // Always require target platform libraries
            requiredBuildPlatforms.Add(BuildPlatformExtensions.GetCurrentTargetPlatform());

            // Check if editor platform libraries exist - if so, they'll be validated
            // If not, we'll allow the build to proceed (optional validation)
            BuildPlatform editorPlatform = BuildPlatformExtensions.GetCurrentEditorPlatform();
            string editorLibraryPath = Path.Join(
                packagePath,
                "Plugins",
                editorPlatform.GetLibraryFolderName()
            );
            editorLibraryPath = Path.Join(
                editorLibraryPath,
                editorPlatform.GetPlatformSpecificLibraryFileName(c_unityPluginName)
            );

            // Check for both files and directories (iOS/visionOS framework/xcframework are directory structures)
            if (File.Exists(editorLibraryPath) || Directory.Exists(editorLibraryPath))
            {
                requiredBuildPlatforms.Add(editorPlatform);
            }

            foreach (BuildPlatform buildPlatform in requiredBuildPlatforms)
            {
                string relativeLibraryPath = Path.Join(
                    "Plugins",
                    buildPlatform.GetLibraryFolderName(),
                    buildPlatform.GetPlatformSpecificLibraryFileName(c_unityPluginName)
                );

                // Check if the native plugin is registered with the Unity project.
                string libraryFilePath = Path.Join(packagePath, relativeLibraryPath);
                if (!File.Exists(libraryFilePath) && !Directory.Exists(libraryFilePath))
                {
                    // Library file does not exist, tell developer to run build.
                    throw new BuildFailedException(
                        $"Did not find library at path {libraryFilePath}. " +
                        $"Please run `./aqua_cmd.py --target-platform {buildPlatform} build`!"
                    );
                }
            }
        }
    }
}
