using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Rendering;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    public class SpatialAdapterSetup : AssetPostprocessor
    {
        private const string SpatialAdapterRuntimePackageName = "com.bytedance.pico.spatialadapter";

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (!didDomainReload)
            {
                return;
            }
            if (File.Exists(SpatialAdapterRuntimeSettings.k_SettingsPath))
            {
                // runtime settings already exists, this is not 1st time package installed
                return;
            }
            EditorApplication.delayCall += Setup;
            SpatialAdapterRuntimeSettings.GetOrCreateSettings();
        }
        
        public static void Setup()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Confirm Project Setup",
                "Do you want to setup this project for SpatialAdapter? This will modify project settings, make sure to back them up if you want to revert them later.",
                "OK",
                "Cancel"
            );
            if (!confirm) return;
            
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29; // Android 10.0
            
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new [] { GraphicsDeviceType.Vulkan });

            PlayerSettings.SplashScreen.show = false;

#if UNITY_6000_0_OR_NEWER
             PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
#endif

            FindPackagePathAndCopyManifest();
        }

        public static bool IsValid
        {
            get
            {
                var graphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
                return EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android &&
                       PlayerSettings.Android.minSdkVersion == AndroidSdkVersions.AndroidApiLevel29 &&
                       PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) == ScriptingImplementation.IL2CPP &&
                       PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64 &&
                       graphicsAPIs[0] == GraphicsDeviceType.Vulkan &&
                       !PlayerSettings.SplashScreen.show &&
                       File.Exists(Path.Combine("Assets", "Plugins", "Android", "AndroidManifest.xml")) &&
                       File.Exists(Path.Combine("Assets", "Plugins", "Android", "LauncherManifest.xml"));
            }
        }
        
        private static void FindPackagePathAndCopyManifest()
        {
            ListRequest listRequest = Client.List(true); // List all installed packages, including hidden ones
            EditorApplication.update += () =>
            {
                if (listRequest.IsCompleted)
                {
                    if (listRequest.Status == StatusCode.Success)
                    {
                        foreach (var package in listRequest.Result)
                        {
                            if (package.name == SpatialAdapterRuntimePackageName)
                            {
                                string packagePath = package.resolvedPath;
                                
                                string sourcePath = Path.Combine(packagePath, "Manifest");
                                string destinationPath = Path.Combine("Assets","Plugins", "Android");

                                if (!Directory.Exists(destinationPath))
                                {
                                    Directory.CreateDirectory(destinationPath);
                                }

                                foreach (string filePath in Directory.GetFiles(sourcePath))
                                {
                                    string fileName = Path.GetFileName(filePath);
                                    string destFilePath = Path.Combine(destinationPath, fileName);
                                    File.Copy(filePath, destFilePath, true);
                                }
                                
                                break;
                            }
                        }
                    }
                    else if (listRequest.Status >= StatusCode.Failure)
                    {
                        Debug.LogError(listRequest.Error.message);
                    }

                    EditorApplication.update -= null;
                }
            };
        }
    }
}