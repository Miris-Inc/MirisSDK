using UnityEngine;
using UnityEngine.Networking;

using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using System.IO;

namespace Aqua.Runtime
{
    

    public class StartupLoader  {
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            PreparePersistentDataDir();
            PreloadData();
        }

        static void PreparePersistentDataDir()
        {
            string dirPath = Path.Combine(Application.persistentDataPath, "miris");
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            AquaUnityApi.SetPersistentDataDirectory(dirPath);
        }

        static async Task PreloadData() {
            string rootUsdDir = "usd/";
            string projectRoot = Application.streamingAssetsPath;

            List<string> noSchemaDirectories = new List<string>{
                "sdf", "ar", "ndr"
            };

            List<string> subdirectoriesToMake = new List<string> {
                "usd", "usdGeom", "sdf", "ar", "ndr", 
                "usdHydra", "usdLux", "usdMedia", "usdPhysics",
                "usdProc", "usdRender", "usdRi", "usdSemantics",
                "usdShade", "usdSkel", "usdUI", "usdUtils", "usdVol"
            };

            List<string> directoriesToMake = new List<string>();
            directoriesToMake.Add(rootUsdDir);

            List<string> filePaths = new List<string>();
            filePaths.Add(rootUsdDir + "plugInfo.json");
            filePaths.Add(rootUsdDir + "usd/resources/usd/schema.usda");
            filePaths.Add(rootUsdDir + "usdGeom/resources/usdGeom/schema.usda");

            foreach(string subDirectory in subdirectoriesToMake) {
                directoriesToMake.Add(Path.Combine(rootUsdDir, subDirectory));
                directoriesToMake.Add(Path.Combine(rootUsdDir, subDirectory, "resources"));
                filePaths.Add(Path.Combine(rootUsdDir, subDirectory, "resources/plugInfo.json"));
                if( !noSchemaDirectories.Contains(subDirectory) ) {
                    filePaths.Add(Path.Combine(rootUsdDir, subDirectory, "resources/generatedSchema.usda"));
                }
            }
            directoriesToMake.Add(Path.Combine(rootUsdDir, "usd/resources/usd"));
            directoriesToMake.Add(Path.Combine(rootUsdDir, "usdGeom/resources/usdGeom"));
           
            
            foreach(string directory in directoriesToMake) {
                string directoryToMake = Path.Combine(Application.persistentDataPath, directory);
                if(!Directory.Exists(directoryToMake)){
                    Directory.CreateDirectory(directoryToMake);
                }
            }

            foreach(string filePath in filePaths){
                string uri = Path.Combine(Application.streamingAssetsPath, filePath);
                string writePath = Path.Combine(Application.persistentDataPath, filePath);

                #if UNITY_ANDROID
                    UnityWebRequest fileRequest = UnityWebRequest.Get(uri);
                    await fileRequest.SendWebRequest();
                    if(fileRequest.result == UnityWebRequest.Result.Success) {
                        File.WriteAllText(writePath, fileRequest.downloadHandler.text);
                    }
                #else 
                    if(File.Exists(uri)){
                        string fileContent = File.ReadAllText(uri);
                        File.WriteAllText(writePath, fileContent);
                    }
                #endif
            }
            AquaUnityApi.SetUsdPath(Application.persistentDataPath + "/" + rootUsdDir);
        }
    }
}
