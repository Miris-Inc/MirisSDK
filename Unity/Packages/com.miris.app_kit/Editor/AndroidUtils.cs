// Copyright © 2025 Miris, Inc. All rights reserved.

using System.Diagnostics;
using System.Threading;

using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Miris.Editor
{
    public class AndroidUtils
    {
        static public string GetPersistentDataPath()
        {
            return $"/sdcard/Android/data/{Application.identifier}/files";
        }

        static public bool BuildApk(string scenePath, string apkPath)
        {
            // Build for android
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                UnityEngine.Debug.Log("Build succeeded: " + summary.totalSize + " bytes");
                return true;
            }
            else
            {
                UnityEngine.Debug.LogError("Build failed: " + summary.result);
                return false;
            }
        }

        static public (string, string) RunAdbCommand(string command)
        {
            UnityEngine.Debug.Log($"Running ADB Command: '{command}'");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                {
                    UnityEngine.Debug.Log("ADB Output: " + output);
                }

                if (!string.IsNullOrEmpty(error))
                {
                    UnityEngine.Debug.LogError("ADB Error: " + error);
                }

                return (output, error);
            }
        }

        static public void WaitForApplicationToQuit(
            string packageName,
            float pollIntervalSeconds = 0.5f,
            float timeOutSeconds = 30.0f
        )
        {
            UnityEngine.Debug.Log(
                $"Waiting for android application '{packageName} to quit. " +
                $"Timeout: {timeOutSeconds} seconds, Polling Interval: {pollIntervalSeconds}"
            );

            bool isRunning = true;
            float elapsedSeconds = 0.0f;

            while (isRunning && elapsedSeconds < timeOutSeconds)
            {
                // Poll then sleep 
                int pollIntervalMs = (int)(pollIntervalSeconds * 1000.0f);
                Thread.Sleep(pollIntervalMs); 
                elapsedSeconds += pollIntervalSeconds;

                // Check if the app is still running by its package name (PID)
                (string output, string _) = RunAdbCommand($"shell pidof {packageName}");
                if (string.IsNullOrEmpty(output)) // No PID means app has exited
                {
                    isRunning = false;
                }
            }

            if (isRunning)
            {
                UnityEngine.Debug.LogWarning($"'{packageName}' has not quit within the timeout of {timeOutSeconds} seconds");
            }

            UnityEngine.Debug.Log($"Android application '{packageName}' quit after waiting for {elapsedSeconds} seconds");
        }
    }
}
