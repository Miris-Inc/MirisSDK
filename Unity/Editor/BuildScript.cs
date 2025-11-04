#nullable enable

using System;
using System.IO;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Aqua.Editor
{
    public class BuildScript
    {
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

            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            BuildPipeline.BuildPlayer(bc.Scenes.ToArray(), buildPath, BuildTarget.iOS, BuildOptions.None);

            PlayerSettings.iOS.sdkVersion = oldSDKVersion;
        }
    }
}
