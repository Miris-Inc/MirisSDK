using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Networking;

namespace Miris.Editor
{
    public class MirisReleaseDownloader : EditorWindow
    {
        [Serializable]
        private class Release
        {
            public string tag_name;
            public Asset[] assets;
        }
        
        [Serializable]
        private class Asset
        {
            public int id;
            public string url;
            public string name;
            public string browser_download_url;
            public long size;
            public string content_type;
        }

        // Path constants
        private const string SaveRoot = "Assets/Plugins";
        private const string MirisFolder = "Assets/Plugins/Miris";
        private const string JsonFileName = "miris_installed.json";
        private const string PackageName = "com.miris.sdk.core";
        private static string JsonPath { get { return Path.Combine(MirisFolder, JsonFileName); } }
        
        // Util constants
        private static readonly string[] sizeLabels = { "B", "KB", "MB", "GB" };
        
        // Extract util functions
        private static bool IsZip(string name) { return name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase); }
        private static bool IsDmg(string name) { return name != null && name.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase); }

        // Config for fetching repo
        private static string owner = "miris-inc";
        private static string repo = "MirisSDK";
        private static string tag = "library@v0.0.9";
        private static string githubToken = ""; // optional
        private static bool hideToken = true;

        // Supported libraries to fetch (uses name in files)
        private enum Platform
        {
            windows,
            osx,
            android,
            ios,
            linux,
            none
        }

        // Selection + discovered data
        private static bool expressInstall = true;

        private static readonly Dictionary<Platform, bool> selected = new Dictionary<Platform, bool>
        {
            { Platform.windows, false },
            { Platform.osx,   false },
            { Platform.android, false },
            { Platform.ios,     false },
            { Platform.linux,   false }
        };
        private static readonly Dictionary<Platform, long> platformSizes = new Dictionary<Platform, long>();
        private static readonly Dictionary<Platform, List<Asset>> platformAssets = new Dictionary<Platform, List<Asset>>();

        // Installed record stored in JSON
        [Serializable]
        private class InstalledVersions
        {
            public string windows = "";
            public string osx = "";
            public string android = "";
            public string ios = "";
            public string linux = "";
        }
        private static InstalledVersions installed = new InstalledVersions();

        private static Release loadedRelease;
        private static Vector2 scroll;

        [MenuItem("Tools/Miris/Platform Downloader")]
        public static void Open() => GetWindow<MirisReleaseDownloader>("Miris Platform Downloader");
        
#if !MIRIS_INTERNAL
        // Attempt to download all packages for this release if the Miris Folder already exists
        // Or if you explicitly check to not download packages
        [InitializeOnLoadMethod]
        private static void DownloadAllPackages()
        {
            var packageVersion = GetPackageVersion();
            if (string.IsNullOrEmpty(packageVersion))
                throw new Exception("There was an issue trying to confirm the correct version of the Miris package.");
            tag = $"library@v{packageVersion}";
            
            // Todo: Once the repo is public, allow auto downloads
            //if(Directory.Exists(MirisFolder) || EditorPrefs.GetBool(StartupWindow.DoNotAutoDownloadPrefsKey))
            //    return;
            //LoadReleaseByTag();
            //SetAllPlatformsAsSelected(true);
            //InstallSelected();
        }
#endif

        private void OnEnable()
        {
            EnsureMirisFolder();
            LoadInstalledJson(); // load on open so statuses show even before loading a release
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("GitHub Repository", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                owner = EditorGUILayout.TextField("Owner", owner);
                repo  = EditorGUILayout.TextField("Repo",  repo);
                tag   = EditorGUILayout.TextField(new GUIContent("Tag", "Required. e.g. library@v0.0.9"), tag);

                using (new EditorGUILayout.HorizontalScope())
                {
                    hideToken = EditorGUILayout.ToggleLeft("Hide token", hideToken, GUILayout.Width(90));
                    githubToken = hideToken
                        ? EditorGUILayout.PasswordField(new GUIContent("GitHub Token"), githubToken)
                        : EditorGUILayout.TextField(new GUIContent("GitHub Token"), githubToken);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    expressInstall = EditorGUILayout.ToggleLeft("Express Install", expressInstall, GUILayout.Width(120));
                    EditorGUILayout.LabelField(new GUIContent("If checked, all compatible platforms will be installed."), EditorStyles.miniLabel);
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo) || string.IsNullOrEmpty(tag)))
                {
                    string label = $"Load Release {tag}";
                    if (expressInstall)
                    {
                        label = $"Install {tag}";
                    }
                    
                    if (GUILayout.Button(label))
                    {
                        LoadReleaseByTag();
                        // When this button is clicked, select all platforms by default
                        SetAllPlatformsAsSelected(true);
                        if (expressInstall)
                        {
                            InstallSelected();
                        }
                    }
                }
            }

            if (loadedRelease == null)
            {
                DrawInstalledSummaryOnly();
                EditorGUILayout.HelpBox("Enter Owner/Repo/Tag, then click 'Load Release by Tag'.", MessageType.Info);
                return;
            }

            DrawPlatformSection();

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select All")) SetAllPlatformsAsSelected(true);
                if (GUILayout.Button("Select None")) SetAllPlatformsAsSelected(false);

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!AnySelectedWithAssets()))
                {
                    if (GUILayout.Button("Install Selected", GUILayout.Height(28)))
                    {
                        InstallSelected();
                    }
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Matched Assets", EditorStyles.boldLabel);
            using (var sv = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.Height(240)))
            {
                scroll = sv.scrollPosition;
                foreach (var entry in platformAssets)
                {
                    var plat = entry.Key;
                    var list = entry.Value;
                    if (list == null || list.Count == 0) continue;

                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        long sz = 0;
                        if (platformSizes.ContainsKey(plat)) sz = platformSizes[plat];
                        EditorGUILayout.LabelField($"{plat} ({FormatSize(sz)})", EditorStyles.miniBoldLabel);

                        for (int i = 0; i < list.Count; i++)
                        {
                            var file = list[i];
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Label(file.name, GUILayout.ExpandWidth(true));
                                GUILayout.Label(FormatSize(file.size), GUILayout.Width(90));
                            }
                        }
                    }
                }
            }
            Repaint();
        }

        private void DrawInstalledSummaryOnly()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Installed (from miris_installed.json)", EditorStyles.boldLabel);
                foreach (Platform p in Enum.GetValues(typeof(Platform)))
                {
                    string v = GetInstalledVersion(p);
                    EditorGUILayout.LabelField($"{p}: {(string.IsNullOrEmpty(v) ? "Not installed" : v)}");
                }
                EditorGUILayout.LabelField($"JSON Path: {JsonPath}", EditorStyles.miniLabel);
            }
        }

        // UI on which platforms to download
        private void DrawPlatformSection()
        {
            EditorGUILayout.LabelField("Platforms (sdk-binaries-PLATFORM*.zip)", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField($"Release: {loadedRelease.tag_name}", EditorStyles.miniBoldLabel);

                EditorGUILayout.Space(2);
                if (expressInstall)
                {
                    EditorGUILayout.LabelField("All compatible platforms will be installed.", EditorStyles.boldLabel);
                }
                else
                {
                    DrawPlatformRow(Platform.windows, Platform.osx, Platform.android);
                    DrawPlatformRow(Platform.ios, Platform.linux, Platform.none);
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField($"Destination: {MirisFolder}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Manifest: {JsonFileName}", EditorStyles.miniLabel);
            }
        }

        // Draw a row of selectable buttons for each platform (for which to download)
        private void DrawPlatformRow(Platform a, Platform b, Platform c)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (a != Platform.none)
                    DrawPlatformToggle(a);
                if (b != Platform.none)
                    DrawPlatformToggle(b);
                if (c != Platform.none)
                    DrawPlatformToggle(c);
            }
        }

        // Draw toggle buttons that show which platforms are currently installed
        // And which are selected to install
        private void DrawPlatformToggle(Platform p)
        {
            long size = 0;
            if (platformSizes.ContainsKey(p)) size = platformSizes[p];

            // Installed status messaging
            string installedVer = GetInstalledVersion(p);
            string status;
            if (string.IsNullOrEmpty(installedVer))
                status = "Not installed";
            else if (installedVer == tag)
                status = $"{installedVer} already installed";
            else
                status = $"Currently {installedVer} → will overwrite to {tag}";

            // Shows the size of the asset that will be downloaded (if asset exists)
            // Otherwise shows "no match" if the binary is not present in the release
            var label = $"{p} {(size > 0 ? $"({FormatSize(size)})" : "(no match)")}\n<size=10>{status}</size>";
            var style = new GUIStyle("Button");
            style.alignment = TextAnchor.MiddleLeft;
            style.richText = true;

            selected[p] = GUILayout.Toggle(selected[p], label, style, GUILayout.Height(36));
        }

        private static void SetAllPlatformsAsSelected(bool v)
        {
            var keys = new List<Platform>(selected.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] == Platform.osx && v)
                {
                    if (Application.platform != RuntimePlatform.OSXEditor)
                    {
                        continue;
                    }
                }
                selected[keys[i]] = v;
            }
        }

        private bool AnySelectedWithAssets()
        {
            foreach (var kv in selected)
            {
                if (kv.Value && platformAssets.ContainsKey(kv.Key) && platformAssets[kv.Key].Count > 0)
                    return true;
            }
            return false;
        }
        
        private static string GetPackageVersion()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            if (packageInfo != null)
            {
                return packageInfo.version;
            }
            else
            {
                Debug.LogError($"Package '{PackageName}' not found.");
                return "";
            }
        }

        // Load the release files by tag
        private static void LoadReleaseByTag()
        {
            try
            {
                EnsureMirisFolder();
                LoadInstalledJson(); // refresh from disk

                string url = $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";
                EditorUtility.DisplayProgressBar("GitHub", "Fetching release…", 0.25f);
                var json = HttpGet(url, githubToken);
                if (string.IsNullOrEmpty(json)) throw new Exception("Empty response.");

                loadedRelease = JsonUtility.FromJson<Release>(json);
                if (loadedRelease == null) throw new Exception("Could not parse release JSON.");
                if (loadedRelease.assets == null || loadedRelease.assets.Length == 0)
                    Debug.LogWarning("[GithubReleaseDownloader] No assets found on this release.");

                IndexAssetsByPlatform();
                EditorUtility.ClearProgressBar();
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();

                if (ex.Message.Contains("HTTP 401"))
                {
                    Debug.LogError($"[GithubReleaseDownloader] Load error: {ex.Message}\n{ex}");
                    EditorUtility.DisplayDialog("Unauthorized", "The GitHub API returned a 401 Unauthorized error. Double-check the permissions on your GitHub token, set a new one and try again.", "OK");
                    return;
                }

                if (ex.Message.Contains("HTTP 404"))
                {
                    Debug.LogError($"[GithubReleaseDownloader] Load error: {ex.Message}\n{ex}");
                    EditorUtility.DisplayDialog("Release Not Found", $"Release with tag '{tag}' not found in {owner}/{repo}. If the release definitely exists, try setting your GitHub Token and try again.", "OK");
                    return;
                }

                Debug.LogError($"[GithubReleaseDownloader] Load error: {ex.Message}\n{ex}");
                EditorUtility.DisplayDialog("Load Error", ex.Message, "OK");
            }
        }

        private static void IndexAssetsByPlatform()
        {
            platformAssets.Clear();
            platformSizes.Clear();

            foreach (Platform p in Enum.GetValues(typeof(Platform)))
            {
                var list = new List<Asset>();
                if (loadedRelease.assets != null)
                {
                    for (int i = 0; i < loadedRelease.assets.Length; i++)
                    {
                        var asset = loadedRelease.assets[i];
                        if (asset == null || string.IsNullOrEmpty(asset.name)) continue;
                        
                        bool isZip = IsZip(asset.name);
                        bool isDmg = IsDmg(asset.name);

                        // osx can be .dmg or .zip; others remain .zip
                        bool allowedExt = false;
                        if (p == Platform.osx) { allowedExt = isZip || isDmg; }
                        else { allowedExt = isZip; }

                        if (!allowedExt) continue;

                        if (IsPlatformAsset(asset.name, p))
                            list.Add(asset);
                    }
                }
                platformAssets[p] = list;

                long sum = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    long s = list[i].size;
                    if (s > 0) sum += s;
                }
                platformSizes[p] = sum;
            }
        }

        // Match: sdk-binaries-PLATFORM*  (case-insensitive)
        // Supports .zip and .dmg (mac only) file extensions
        private static bool IsPlatformAsset(string filename, Platform p)
        {
            var f = filename.ToLowerInvariant();
            if (!f.StartsWith("sdk-binaries-")) return false;

            string token = p.ToString();
            if (string.IsNullOrEmpty(token) || token == "none") return false;

            var afterPrefix = f.Substring("sdk-binaries-".Length);

            bool isMac = (p == Platform.osx);
            bool endsOk = isMac ? (f.EndsWith(".zip") || f.EndsWith(".dmg")) : f.EndsWith(".zip");

            return afterPrefix.StartsWith(token) && endsOk;
        }

        // Install selected platforms
        private void InstallSelected()
        {
            EnsureMirisFolder();

            if (expressInstall)
            {
                SetAllPlatformsAsSelected(true);
            }

            var toDownload = new List<KeyValuePair<Platform, Asset>>();
            foreach (var kv in selected)
            {
                if (!kv.Value) continue;
                if (platformAssets.ContainsKey(kv.Key))
                {
                    var list = platformAssets[kv.Key];
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                            toDownload.Add(new KeyValuePair<Platform, Asset>(kv.Key, list[i]));
                    }
                }
            }

            if (toDownload.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing to Install", "No matching assets found for selected platforms.", "OK");
                return;
            }

            Directory.CreateDirectory(SaveRoot);
            Directory.CreateDirectory(MirisFolder);

            int total = toDownload.Count;
            int done = 0;

            string tempRoot = Path.Combine(Path.GetTempPath(), "MirisReleaseDownloader", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                for (int i = 0; i < toDownload.Count; i++)
                {
                    var item = toDownload[i];
                    Platform plat = item.Key;
                    var fileInfo = item.Value;
                    
                    if (IsDmg(fileInfo.name) && Application.platform != RuntimePlatform.OSXEditor)
                    {
                        Debug.Log("Mac Miris binaries were not downloaded because they can only be downloaded on MacOS");
                        continue;
                    }

                    string url = fileInfo.browser_download_url;
                    if (string.IsNullOrEmpty(url))
                    {
                        Debug.LogWarning($"Asset has no download url: {fileInfo.name}");
                        done++; continue;
                    }

                    float progress = (float)done / Math.Max(1, total);
                    EditorUtility.DisplayProgressBar("Downloading", $"{fileInfo.name}", progress);

                    string tmpPath = Path.Combine(tempRoot, fileInfo.name);
                    bool haveToken = !string.IsNullOrEmpty(githubToken);
                    string downloadUrl = (haveToken && !string.IsNullOrEmpty(fileInfo.url))
                        ? fileInfo.url                                // private (or any) via API
                        : fileInfo.browser_download_url;              // public CDN

                    DownloadFile(downloadUrl, tmpPath, githubToken);

                    // Extract into Miris folder (overwrite files if present)
                    EditorUtility.DisplayProgressBar("Installing", $"Extracting {fileInfo.name}", progress + 0.02f);

                    var platformFolder = $"{MirisFolder}/{plat}";
                    Directory.CreateDirectory(platformFolder);

                    if (IsZip(fileInfo.name))
                    {
                        ExtractZipSafe(tmpPath, platformFolder);
                    }
                    else if (IsDmg(fileInfo.name))
                    {
                        if (Application.platform != RuntimePlatform.OSXEditor)
                        {
                            Debug.Log("Skipping dmg file on non-mac platform. Please use a mac to download this.");
                            continue;
                        }
                        ExtractDmgOnMac(tmpPath, platformFolder);
                        TryClearQuarantineOnMac(platformFolder);
                    }
                    else
                    {
                        throw new Exception("Unsupported asset type. Expected .zip (all) or .dmg (macOS).");
                    }
                    
                    // Fix plugin import settings to allow for multiple libraries installed
                    //FixPluginImportSettings(platformFolder, plat);

                    // Update installed record for this platform to the current tag
                    SetInstalledVersion(plat, tag);
                    SaveInstalledJson();

                    done++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GithubReleaseDownloader] Install error: {ex.Message}\n{ex}");
                EditorUtility.DisplayDialog("Install Error", ex.Message, "OK");
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch {}
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            // Reload so UI reflects new versions
            LoadInstalledJson();

            EditorUtility.DisplayDialog("Done", "Selected platforms installed into Assets/Plugins/Miris.", "OK");
        }
        
        // todo: do we need to set the import settings for the binaries when downloading all of them?
        private static void FixPluginImportSettings(string folder, Platform plat)
        {
            string[] plugins = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
            foreach (var path in plugins)
            {
                if (!path.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(".so", StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                string assetPath = path.Replace(Application.dataPath, "Assets");

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var baseImp = AssetImporter.GetAtPath(assetPath);  // should not be null if Unity sees the asset
                var importer = baseImp as PluginImporter;         // may still be null if Unity didn't classify it as a plugin
                // Clear all compatibility flags
                importer.ClearSettings();

                // Set compatibility per platform
                importer.SetCompatibleWithAnyPlatform(false);
                importer.SetCompatibleWithEditor(false);
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, plat == Platform.windows);
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, plat == Platform.windows);
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, plat == Platform.osx);
                importer.SetCompatibleWithPlatform(BuildTarget.Android, plat == Platform.android);
                importer.SetCompatibleWithPlatform(BuildTarget.iOS, plat == Platform.ios);
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, plat == Platform.linux);

                importer.SaveAndReimport();
            }
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            string[] dirs = Directory.GetDirectories(sourceDir);
            for (int i = 0; i < dirs.Length; i++)
            {
                string name = Path.GetFileName(dirs[i]);
                if (string.IsNullOrEmpty(name)) continue;
                CopyDirectoryRecursive(dirs[i], Path.Combine(destDir, name));
            }

            string[] files = Directory.GetFiles(sourceDir);
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                if (string.IsNullOrEmpty(name)) continue;
                string dst = Path.Combine(destDir, name);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(files[i], dst, true);
            }
        }
        
        // Mac platform only
        // Extract the dmg instead of the zip
        private static void ExtractDmgOnMac(string dmgPath, string destDir)
        {
            string mountPoint = Path.Combine(Path.GetTempPath(), "MirisMount_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(mountPoint);

            int attach = RunProcess("/usr/bin/hdiutil",
                "attach -nobrowse -readonly -mountpoint \"" + mountPoint + "\" \"" + dmgPath + "\"", 120, null);
            if (attach != 0)
            {
                TryDetachDmg(mountPoint);
                throw new Exception("Failed to attach DMG: " + dmgPath);
            }

            try
            {
                CopyDirectoryRecursive(mountPoint, destDir);
            }
            finally
            {
                TryDetachDmg(mountPoint);
                try { if (Directory.Exists(mountPoint)) Directory.Delete(mountPoint, true); } catch {}
            }
        }

        // Mac platform only
        // Clear the quarantine attribute on the files pulled in
        private static void TryClearQuarantineOnMac(string targetDir)
        {
            if (Application.platform != RuntimePlatform.OSXEditor) return;
            if (!Directory.Exists(targetDir)) return;
            RunProcess("/usr/bin/xattr", "-dr com.apple.quarantine \"" + targetDir + "\"", 60, null);
        }
        
        // Mac platform only
        // Remove the dmg after opening it
        private static void TryDetachDmg(string mountPoint)
        {
            RunProcess("/usr/bin/hdiutil", "detach \"" + mountPoint + "\" -force", 60, null);
        }

        // Run a separate process and wait for it to finish
        private static int RunProcess(string fileName, string args, int timeoutSecs, string workingDir)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = fileName;
                psi.Arguments = args;
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = false;
                psi.RedirectStandardError = false;
                if (!string.IsNullOrEmpty(workingDir)) psi.WorkingDirectory = workingDir;

                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    if (p == null) return -1;
                    if (!p.WaitForExit(timeoutSecs * 1000))
                    {
                        try { p.Kill(); } catch {}
                        return -2;
                    }
                    return p.ExitCode;
                }
            }
            catch { return -3; }
        }

        // Json install-file helpers
        private static void EnsureMirisFolder()
        {
            if (!Directory.Exists(SaveRoot)) Directory.CreateDirectory(SaveRoot);
            if (!Directory.Exists(MirisFolder)) Directory.CreateDirectory(MirisFolder);
        }

        // Use json to keep track of which platforms are installed
        private static void LoadInstalledJson()
        {
            try
            {
                if (!File.Exists(JsonPath))
                {
                    installed = new InstalledVersions();
                    // create empty json file if it doesn't exist
                    SaveInstalledJson();
                    return;
                }
                string json = File.ReadAllText(JsonPath, Encoding.UTF8);
                if (string.IsNullOrEmpty(json))
                {
                    installed = new InstalledVersions();
                    return;
                }
                var parsed = JsonUtility.FromJson<InstalledVersions>(json);
                if (parsed != null) installed = parsed;
                else installed = new InstalledVersions();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MirisReleaseDownloader] Failed to load {JsonPath}: {ex.Message}");
                installed = new InstalledVersions();
            }
        }

        // Write to json what platforms are installed
        private static void SaveInstalledJson()
        {
            try
            {
                string json = JsonUtility.ToJson(installed, true);
                File.WriteAllText(JsonPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MirisReleaseDownloader] Failed to save {JsonPath}: {ex.Message}");
            }
        }

        private string GetInstalledVersion(Platform p)
        {
            switch (p)
            {
                case Platform.windows: return installed.windows;
                case Platform.osx:   return installed.osx;
                case Platform.android: return installed.android;
                case Platform.ios:     return installed.ios;
                case Platform.linux:   return installed.linux;
                default: return "";
            }
        }

        private static void SetInstalledVersion(Platform p, string version)
        {
            switch (p)
            {
                case Platform.windows: installed.windows = version; break;
                case Platform.osx:   installed.osx   = version; break;
                case Platform.android: installed.android = version; break;
                case Platform.ios:     installed.ios     = version; break;
                case Platform.linux:   installed.linux   = version; break;
            }
        }

        // Get request for github to see contents of release
        private static string HttpGet(string url, string token = "")
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.redirectLimit = 32;
                req.timeout = 60;
                req.SetRequestHeader("User-Agent", "UnityEditor-MirisReleaseDownloader/1.0");
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

                if (!string.IsNullOrEmpty(token) && IsGitHubApiHost(url))
                    req.SetRequestHeader("Authorization", $"Bearer {token}");

                var op = req.SendWebRequest();
                while (!op.isDone) {}

                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"GET {url}\n{req.error}\nHTTP {(int)req.responseCode}");

                return req.downloadHandler.text;
            }
        }

        // Download request for github release contents
        private static void DownloadFile(string url, string path, string token = "")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var req = UnityWebRequest.Get(url))
            {
                req.redirectLimit = 32;
                req.timeout = 180;
                req.SetRequestHeader("User-Agent", "UnityEditor-MirisReleaseDownloader/1.0");
                req.SetRequestHeader("Accept", "application/octet-stream");
                req.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
                
                if (!string.IsNullOrEmpty(token) && IsGitHubApiHost(url))
                    req.SetRequestHeader("Authorization", $"Bearer {token}");

                var dh = new DownloadHandlerFile(path) { removeFileOnAbort = true };
                req.downloadHandler = dh;

                var op = req.SendWebRequest();
                while (!op.isDone) {}

                bool ok = req.result == UnityWebRequest.Result.Success;
                if (!ok)
                    throw new Exception($"Download failed: {Path.GetFileName(path)}\nURL: {url}\nHTTP {(int)req.responseCode} {req.error}");
            }
        }
        
        private static bool IsGitHubApiHost(string url)
        {
            try { return new Uri(url).Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        // Zip extraction 
        private static void ExtractZipSafe(string zipPath, string destDir)
        {
            using (var fs = File.OpenRead(zipPath))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                string destFull = Path.GetFullPath(destDir);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.FullName)) continue;

                    if (entry.FullName.StartsWith("__MACOSX/"))
                        continue; // skip macOS resource forks

                    string outPath = Path.Combine(destDir, entry.FullName);
                    string outFull = Path.GetFullPath(outPath);

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(outFull);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(outFull));

                    using (var inStream = entry.Open())
                    using (var outStream = File.Create(outFull))
                    {
                        inStream.CopyTo(outStream);
                    }
                }
            }
        }

        // Used to display size of files
        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "—";
            double len = bytes;
            int order = 0;
            while (len >= 1000 && order < sizeLabels.Length - 1)
            {
                order++;
                len /= 1000;
            }
            return $"{len:0.#} {sizeLabels[order]}";
        }
    }
}