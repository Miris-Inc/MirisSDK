// Copyright © 2026 Miris, Inc. All rights reserved.

// Standard library
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;

// Unity engine
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;

// For saving out scenes when a test fails, only supported in Editor.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
#endif // UNITY_EDITOR

// Unity packages
using NUnit.Framework;
using Object = UnityEngine.Object;
using Miris.Runtime;
using System.Linq;

namespace Miris.Tests
{
    public class MirisTestUtils
    {
        static public void ClearScene()
        {
            foreach (GameObject gameObject in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                GameObject.DestroyImmediate(gameObject);
            }
        }

        // This is copied from Unity's Image testing framework's ImageAssert.cs
        private static string StripParametricTestCharacters(string name)
        {
            string value = "\"";
            for (int num = name.IndexOf(value); num >= 0; num = name.IndexOf(value))
            {
                name = name.Remove(num, 1);
            }

            string oldValue = ",";
            name = name.Replace(oldValue, "-");
            string oldValue2 = "(";
            name = name.Replace(oldValue2, "_");
            string oldValue3 = ")";
            name = name.Replace(oldValue3, "_");
            return name;
        }

        // Insert platform directory into resource path
        static public string PlatformSpecificPath(string path)
        {
            string platformName = "";
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor :
                    platformName = "Windows";
                    break;
                case RuntimePlatform.OSXEditor:
                    platformName = "OSX";
                    break;
                case RuntimePlatform.LinuxEditor:
                    platformName = "Linux";
                    break;
            }
            if (string.IsNullOrEmpty(platformName)) {
                return path;
            } else {
                return Path.Join(Path.GetDirectoryName(path), platformName, Path.GetFileName(path));
            }
        }

        // Used to save all the game objects in the current scene as a prefab for debugging purposes.
        static public void SaveCurrentSceneAsPrefab(string parentDirectory)
        {
#if UNITY_EDITOR
            // Get the current test name
            string testName = (TestContext.CurrentContext.Test.MethodName != null) ? TestContext.CurrentContext.Test.Name : "NoName";

            // Parent all the created test game objects under one root
            UnityEngine.SceneManagement.Scene activeScene = SceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            GameObject newSceneRoot = new GameObject(testName);
            foreach (GameObject rootObject in rootObjects)
            {
                // Ignore the object that the Unity test framework creates for executing the tests.
                // Maybe there's a more robust way to identify this object (by Component?)
                if (rootObject.name == "Code-based tests runner")
                {
                    continue;
                }

                rootObject.transform.SetParent(newSceneRoot.transform);
            }

            // Save as a prefab next to the failed images
            string prefabFileName = $"{StripParametricTestCharacters(testName)}.prefab";
            string prefabAssetPath = Path.Combine(parentDirectory, prefabFileName);
            UnityEngine.Debug.Log($"Saving scene as prefab: {prefabAssetPath}");
            PrefabUtility.SaveAsPrefabAsset(newSceneRoot, prefabAssetPath);
#endif // UNITY_EDITOR
        }

        static public async Task<string> ResolveContentUrl(MirisStreamController streamController, MirisInternalController internalController, string contentName, string environmentName)
        {
            UnityEngine.Debug.Log($"[ResolveContentUrl] Choosing environment {environmentName}");
            internalController.GetEnvManager().SetEnv(environmentName, ""); // unit tests don't need viewer keys

            UnityEngine.Debug.Log($"[ResolveContentUrl] Fetching asset {contentName}");
            var assets = await streamController.GetAssetManager().GetAssets();
            // TODO: Need to rework this when we go back to actively working on Unity. This is actually the wrong way to acquire assets. We need to be using asset UUIDs, not names!
            var contentUrl = "TODO: OUTDATED";

            UnityEngine.Debug.Log($"[ResolveContentUrl] Found asset {contentName} to be loaded from {contentUrl}");
            return contentUrl;
        }

        /// <summary>
        /// Returns the appropriate test server environment name based on execution context.
        /// In CI: Uses "Loopback" (localhost) since the build IP may not be reachable.
        /// Local dev: Uses "Local" (build machine IP) for testing against local server.
        /// </summary>
        static public string GetTestServerEnvironment()
        {
            string env;
            if (OnLoad.IsCIEnvironment())
            {
                // In CI, use Loopback variants (localhost) since the build IP is unreachable
                env = "Loopback";
                UnityEngine.Debug.Log($"[AquaTestUtils] CI environment detected - using {env} (localhost)");
            }
            else
            {
                // Local dev uses the build machine's IP
                env = "LocalTest";
                UnityEngine.Debug.Log($"[AquaTestUtils] Local dev environment - using {env} (build IP)");
            }
            return env;
        }

        /// <summary>
        /// Returns the appropriate test server host based on execution context.
        /// In CI: Returns "localhost" since the build IP may not be reachable.
        /// Local dev: Returns "localhost" as well (consistent with Loopback environments).
        /// </summary>
        static public string GetTestServerHost()
        {
            // For now, always use localhost for test URLs.
            // CI uses localhost (Loopback), and local dev can use localhost too
            // since the test server runs on the same machine.
            return "localhost";
        }

        /// <summary>
        /// Constructs a direct content URL for test assets.
        /// Use this instead of hardcoding URLs with {devlocalhost} placeholders.
        /// </summary>
        /// <param name="contentPath">The content path (e.g., "conditioned/tokyo/1x1/0_0_0/12/0_0_0-12.drop")</param>
        /// <param name="port">The server port (default: 3003)</param>
        static public string BuildDirectContentUrl(string contentPath, int port = 3003)
        {
            string host = GetTestServerHost();
            return $"http://{host}:{port}/content/{contentPath}";
        }
    }

    // Base class for all the rendering tests
    //
    // Reference images live under Tests/Resources/ReferenceImages.
    // I haven't figured out how to generate a new reference image directly yet, 
    // but ImageAssert.AreEqual(saveFailedImageToDisk: true) will save out a failed image which... you can use as reference.
    // Before the reference image can be used, select it and in the inspector:
    // 1. Set Compression to "None"
    // 2. Enable Advanced > Read/Write
#if UNITY_EDITOR && USING_URP
    [PrebuildSetup(typeof(UrpTestSetup))]
    [PostBuildCleanup(typeof(UrpTestSetup))]
#endif
    public class RendererTestBase
    {
        private const float c_metaQuest3Fov = 110.0f;
        // Create a camera that looking an object to render
        protected void SetupCamera(GameObject renderableObject, out Camera camera, out GraphicsTestSettings graphicsTestSettings)
        {
            // Create the main camera and transform it to face the test asset.
            GameObject cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.Translate(1.0f, 0.5f, -0.5f);
            cameraObject.transform.LookAt(renderableObject.transform.position);

            // Update background to solid black.
            camera = cameraObject.AddComponent(typeof(Camera)) as Camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

#if UNITY_EDITOR
            // When running tests on your desktop platform (via the Unity Editor), match our test camera FOV to Meta Quest 3's (110)
            camera.fieldOfView = c_metaQuest3Fov;
            
            // force the window/screen dimensions to a 1024, 1024 to prevent issue where non-standard editor layouts cause tests to fail
            var fullscreen = true;
            Screen.SetResolution(1024, 1024, fullscreen);
            var windows = (UnityEditor.EditorWindow[])Resources.FindObjectsOfTypeAll(typeof(UnityEditor.EditorWindow));
            foreach(var window in windows)
            {
                if(window != null && window.GetType().FullName == "UnityEditor.GameView")
                {
                    window.maximized = fullscreen;
                    break;
                }
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

#endif
            UnityEngine.Debug.Log($"Test camera field of view: {camera.fieldOfView}");
            UnityEngine.Debug.Assert(camera.fieldOfView == c_metaQuest3Fov, $"Camera's FOV should match Meta Quest 3's ({c_metaQuest3Fov})");

            // Add required graphics test settings       
            graphicsTestSettings = camera.gameObject.AddComponent<GraphicsTestSettings>();
            graphicsTestSettings.ImageComparisonSettings.TargetWidth = 1024;
            graphicsTestSettings.ImageComparisonSettings.TargetHeight = 1024;
            graphicsTestSettings.ImageComparisonSettings.AverageCorrectnessThreshold = 0.0005f;
        }


        // Performs a render and compares it against a reference image.
        protected IEnumerator RenderComparison(Camera camera, GraphicsTestSettings graphicsTestSettings, string referenceImagePath, int warmUpFrames = 2)
        {
            // Warm up
            for (int frameIndex = 0; frameIndex < warmUpFrames; ++frameIndex)
            {
                yield return new WaitForEndOfFrame();
            }

            // Create platform specific image path and attempt to load it before falling back to base path
            string platformReferenceImagePath = MirisTestUtils.PlatformSpecificPath(referenceImagePath);

            Texture2D referenceImage = Resources.Load<Texture2D>(platformReferenceImagePath);
            if (referenceImage == null)
                referenceImage = Resources.Load<Texture2D>(referenceImagePath);

            try
            {
                ImageAssert.AreEqual(
                    referenceImage,
                    camera,
                    settings: graphicsTestSettings.ImageComparisonSettings,
                    saveFailedImageToDisk: true
                );
            }
            catch (AssertionException ex)
            {
                // Save scene as prefab when the image comparison fails.
                string actualImagesDir = Path.Combine("Assets/ActualImages", TestUtils.GetCurrentTestResultsFolderPath());
                MirisTestUtils.SaveCurrentSceneAsPrefab(actualImagesDir);
                throw ex;
            }

            yield return null;
        }

        [TearDown]
        public void Teardown()
        {
            MirisTestUtils.ClearScene();
        }
    }



#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class OnLoad {
        static OnLoad()
        {
            bool isWindows = SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows;
            bool isMac = SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX;
            bool isLinux = SystemInfo.operatingSystemFamily == OperatingSystemFamily.Linux;
            bool isAndroid = Application.platform == RuntimePlatform.Android;
            bool isIOS = Application.platform == RuntimePlatform.IPhonePlayer;
        
            bool isCI = IsCIEnvironment();

            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreInWindows", isWindows);
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreInMacOS", isMac);
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreInLinux", isLinux);
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreInAndroid", isAndroid);
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreInIOS", isIOS);

            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreInCI", isCI);

            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreInWindowsCI", isWindows && isCI);
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreInMacOSCI", isMac && isCI);
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreInLinuxCI", isLinux && isCI);
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreInAndroidCI", isAndroid && isCI);
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreInIOSCI", isIOS && isCI);
        
        }
        public static bool IsCIEnvironment()
        {
            string[] ciEnvironmentVariables = {
                "CI",                     
                "CONTINUOUS_INTEGRATION", // Another generic CI flag
                "GITHUB_ACTIONS",          
                "GITLAB_CI",     
                "JENKINS_URL",   
                "TRAVIS",     
                "CIRCLECI",   
                "TEAMCITY_VERSION",                  
                "TF_BUILD",               // Azure DevOps / Team Foundation Build
                "BITBUCKET_COMMIT",                  
                "APPVEYOR",
                "BUILD_ID",               // Common in Jenkins, GitLab CI, Google Cloud Build etc.
                "UNITY_CLOUD_BUILD"       // This is more likely a _preprocessor define_ in Unity Cloud Build
                        
                // Add other CI-specific environment variables relevant to your setup
            };

            foreach (string envVar in ciEnvironmentVariables)
            {
                // Check if the environment variable exists and is not empty or "false"
                // Some CI systems set CI="true" or CI="false".
                // Checking for non-empty is a good general start.
                string value = Environment.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrEmpty(value) && !value.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
