using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    internal enum AssetType
    {
        None,
        Scene,
        Prefab,
        ShaderGraph,
        Material,
    }

    internal static class AssetExportHelpers
    {
        private static readonly string k_ProjectDirectory = Path.GetDirectoryName(Application.dataPath);
        private static readonly string k_TempDirectory = Path.Combine(k_ProjectDirectory, "Temp");
        private static readonly string k_ExportDirectory = Path.Combine(k_TempDirectory, "SpatialAdapterExport");
        private static readonly string k_AssetsDirectory = Application.dataPath;
        private static readonly string k_BackupDirectory = Path.Combine(k_TempDirectory, "SpatialAdapterExportBackup");
        private static readonly string k_StreamingAssetsDirectory = Application.streamingAssetsPath;
        private static readonly string k_FinalBuildDirectory = Path.Combine(Application.streamingAssetsPath, "SpatialAdapterExport");
        private static readonly string k_BuiltinPrefix = Path.Combine("Resources", "unity_builtin_extra");

        private static readonly string k_LastBuildTimeStamp = Path.Combine(k_ExportDirectory, "LastBuildTimeStamp.txt");

        public static string GetExportDirectory()
        {
            return k_ExportDirectory;
        }

        public static string GetExportedAssetPath(string assetPath, ExportFormat format = ExportFormat.GLB)
        {
            return GetExportedAssetPath(assetPath, GetAssetType(assetPath), format);
        }

        public static void StoreBackupAsset(string assetPath)
        {
            var backupPath = Path.Combine(k_BackupDirectory, assetPath);
            if (!Directory.Exists(Path.GetDirectoryName(backupPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
            }

            File.Copy(assetPath, backupPath, true);
        }

        public static void RestoreAllAssetsFromBackup()
        {
            var backupAssetsDirectory = Path.Combine(k_BackupDirectory, "Assets");

            if (Directory.Exists(backupAssetsDirectory))
            {
                RecursiveCopy(backupAssetsDirectory, k_AssetsDirectory);
            }
        }

        public static string Relativize(this string exportedAssetPath)
        {
            return Path.GetRelativePath(Path.GetDirectoryName(k_ExportDirectory), exportedAssetPath);
        }

        public static string Unrelativize(this string exportedAssetPath)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(k_ExportDirectory), exportedAssetPath));
        }

        public static string GetExtension(ExportFormat format)
        {
            switch (format)
            {
                case ExportFormat.GLB:
                    return ".glb";
                case ExportFormat.USDZ:
                    return ".usdz";
                default:
                    throw new ArgumentException("Invalid export format");
            }
        }

        public static string GetExportedAssetPath(string assetPath, AssetType assetType, ExportFormat format = ExportFormat.GLB)
        {
            switch (assetType)
            {
                case AssetType.Scene:
                case AssetType.Prefab:
                    assetPath += GetExtension(format);
                    break;

                case AssetType.Material:
                    assetPath += ".glb";
                    break;

                case AssetType.ShaderGraph:
                    assetPath += ".usda";
                    break;
            }

            return Path.Combine(k_ExportDirectory, assetPath);
        }

        public static string GetExportedAssetPathBuiltin(this string builtinAssetPath, ExportFormat format = ExportFormat.GLB)
        {
            return GetExportedAssetPath(Path.Combine(k_BuiltinPrefix, builtinAssetPath), format);
        }

        public static string MarkDirty(string exportedAssetPath)
        {
            return Path.ChangeExtension(exportedAssetPath, ".dirty" + Path.GetExtension(exportedAssetPath));
        }

        public static bool IsAssetTypeSupported(AssetType assetType)
        {
            switch (assetType)
            {
                case AssetType.Scene:
                case AssetType.Prefab:
                case AssetType.Material:
                    return true;
                case AssetType.ShaderGraph:
                case AssetType.None:
                default:
                    return false;
            }
        }

        public static string GetOriginalAssetPath(string exportedAssetPath)
        {
            var originalAssetPath = Path.GetRelativePath(k_ExportDirectory, exportedAssetPath);

            // Trim exported asset extension if it exists
            var extension = Path.GetExtension(originalAssetPath);
            if (extension == ".glb"
                || extension == ".usda"
                || extension == ".usdz")
            {
                var filename = Path.GetFileNameWithoutExtension(originalAssetPath);

                // Strip any "dirty" sub-extension
                if (Path.GetExtension(filename) == ".dirty")
                {
                    filename = Path.GetFileNameWithoutExtension(filename);
                }

                originalAssetPath = Path.Combine(
                    Path.GetDirectoryName(originalAssetPath),
                    filename
                );
            }

            return Path.Combine(k_ProjectDirectory, originalAssetPath);
        }

        public static List<string> GetAllResourcesDirectories()
        {
            // Get all subdirectories of Assets folder (and filter out StreamingAssets folder)
            List<string> assetDirectories = Directory.GetDirectories(k_AssetsDirectory).Where(
                relativePath =>
                {
                    var fullPath = Path.Combine(k_AssetsDirectory, relativePath);
                    return fullPath != k_StreamingAssetsDirectory && fullPath != k_BackupDirectory;
                }
            ).ToList();

            // Get any resources directories found in each asset directory
            var resourcesDirectories = new List<string>();
            foreach (var directory in assetDirectories)
            {
                if (string.Equals(Path.GetFileName(directory), "resources", StringComparison.OrdinalIgnoreCase))
                {
                    resourcesDirectories.Add(directory);
                }

                Directory.GetDirectories(directory, "resources", SearchOption.AllDirectories).ToList().ForEach(
                    directory => resourcesDirectories.Add(Path.GetRelativePath(k_ProjectDirectory, directory))
                );
            }

            return resourcesDirectories;
        }

        public static AssetType GetAssetType(string assetPath)
        {
            var extension = Path.GetExtension(assetPath);
            if (extension == ".unity")
            {
                return AssetType.Scene;
            }
            else if (extension == ".prefab")
            {
                return AssetType.Prefab;
            }
            else if (extension == ".shadergraph")
            {
                return AssetType.ShaderGraph;
            }
            else if (extension == ".mat")
            {
                return AssetType.Material;
            }
            else if (extension == ".asset")
            {
                return GetAssetTypeFromContents(assetPath);
            }
            else
            {
                return AssetType.None;
            }
        }

        private static AssetType GetAssetTypeFromContents(string assetPath)
        {
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (mainAsset is TMPro.TMP_Asset)
            {
                return AssetType.Material;
            }

            return AssetType.None;            
        }


        public static void CopyDirectory(string sourceDir, string destinationDir)
        {
            // Create destination directory if it doesn't exist
            Directory.CreateDirectory(destinationDir);

            // Copy all files
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // Copy all subdirectories recursively
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        public static void CopyExportedAssetsToFinalBuild()
        {
            CopyDirectory(k_ExportDirectory, k_FinalBuildDirectory);
        }

        public static void CleanFinalBuildDirectory()
        {
            if (Directory.Exists(k_FinalBuildDirectory))
            {
                Directory.Delete(k_FinalBuildDirectory, true);
            }
        }

        public static void CleanBackupDirectory()
        {
            if (Directory.Exists(k_BackupDirectory))
            {
                Directory.Delete(k_BackupDirectory, true);
            }
        }

        public static void SaveBuildTimeStamp()
        {
            if (!Directory.Exists(k_TempDirectory))
            {
                Directory.CreateDirectory(k_TempDirectory);
            }

            File.WriteAllText(k_LastBuildTimeStamp, DateTime.Now.AddSeconds(1).ToString());
        }

        public static DateTime? GetLastBuildTimeStamp()
        {
            if (File.Exists(k_LastBuildTimeStamp))
            {
                return DateTime.Parse(File.ReadAllText(k_LastBuildTimeStamp));
            }
            else
            {
                return null;
            }
        }

        public static void RecursiveCopy(string sourceDir, string destinationDir)
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                process.StartInfo.FileName = "robocopy";
                process.StartInfo.Arguments = $"\"{sourceDir}\" \"{destinationDir}\" /E /IS /IT";
            }
            else
            {
                process.StartInfo.FileName = "cp";
                process.StartInfo.Arguments = $"-R -f \"{sourceDir}/\" \"{destinationDir}/\"";
            }

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Error", $"Something went wrong while restoring assets: {error}", "OK");
                UnityEngine.Debug.LogError($"Error while restoring assets: {error}");
            }
        }

        public static void CleanCurrentScene()
        {
            var runtimes = UnityEngine.Object.FindObjectsOfType<SpatialAdapterRuntime>();
            foreach (var runtime in runtimes)
            {
                UnityEngine.Object.DestroyImmediate(runtime.gameObject);
            }

            var trackers = UnityEngine.Object.FindObjectsOfType<SpatialAdapterGameObjectTracker>();
            foreach (var tracker in trackers)
            {
                UnityEngine.Object.DestroyImmediate(tracker);
            }

            var prefabIds = UnityEngine.Object.FindObjectsOfType<PrefabId>();
            foreach (var prefabId in prefabIds)
            {
                UnityEngine.Object.DestroyImmediate(prefabId);
            }
        }
    }
}