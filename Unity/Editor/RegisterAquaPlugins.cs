// Copyright © 2024 Miris. All rights reserved.

// Standard library
using System.Collections.Generic;
using System.IO;

// Unity Engine & Editor
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditor.Build;

namespace Aqua.Editor
{
    public static class AquaPlayerBuildHandler
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
            RegisterAquaPlugins.ValidateAquaPlugins();

            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        }
    }

    [InitializeOnLoad]
    public class RegisterAquaPlugins
    {
        private const string c_aquaUnityPluginName = "AquaUnity";

        // This static constructor is called when the Unity editor initializes or recompiles.
        static RegisterAquaPlugins()
        {
            ValidateAquaPlugins();
        }

        static public void ValidateAquaPlugins()
        {
            // We need the Aqua C++ libraries registered for both current desktop platform (what your editor is running in)
            // AND current deployment platform

            HashSet<AquaBuildPlatform> requiredBuildPlatforms = new HashSet<AquaBuildPlatform>();
            requiredBuildPlatforms.Add(AquaBuildPlatformExtensions.GetCurrentEditorPlatform());
            requiredBuildPlatforms.Add(AquaBuildPlatformExtensions.GetCurrentTargetPlatform());

            foreach (AquaBuildPlatform buildPlatform in requiredBuildPlatforms)
            {
                string relativeLibraryPath = Path.Join(
                    "Plugins",
                    buildPlatform.GetLibraryFolderName(),
                    buildPlatform.GetPlatformSpecificLibraryFileName(c_aquaUnityPluginName)
                );

                // Check if the native plugin is registered with the Unity project.
                string packagePath = PackageUtils.GetSdkPackagePath("core");
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
