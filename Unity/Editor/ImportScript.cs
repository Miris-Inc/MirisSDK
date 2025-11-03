using UnityEditor;
using UnityEngine;
using System.IO;

namespace Aqua.Editor
{
    [InitializeOnLoad]
    public static class ImportScript 
    {

        private static void CopyDirectory(string sourceDir, string targetDir)
        {

            foreach(string file in Directory.GetFiles(sourceDir)){
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach(string dir in Directory.GetDirectories(sourceDir))
                {
                    string subDirName = Path.GetFileName(dir);
                    string targetSubDir = Path.Combine(targetDir, subDirName);
                    Directory.CreateDirectory(targetSubDir);
                    CopyDirectory(dir, targetSubDir);
                }
        }

        static ImportScript()
        {
            if(!SessionState.GetBool("MirisCoreInitialized", false)){

                string sourceFolder = Path.Combine(Application.dataPath, "../Packages/com.miris.sdk.core/usd~");
                string targetFolder = Path.Combine(Application.streamingAssetsPath, "usd");

                if(!Directory.Exists(sourceFolder)){
                    Debug.LogError("USD Source Directory does not exist");
                    return;
                }
                

                if(!Directory.Exists(targetFolder)){
                    Directory.CreateDirectory(targetFolder);
                }

                CopyDirectory(sourceFolder, targetFolder);

                SessionState.GetBool("MirisCoreInitialized", true);
            }

        }
    }
}
