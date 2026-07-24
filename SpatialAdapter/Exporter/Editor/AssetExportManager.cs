using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    internal static class AssetExportManager
    {
        private static string k_ResourceDataPath = "Assets/Editor/SpatialAdapterResourceData.asset";

        private static ExportFormat exportFormat;
        private static bool exportShaderGraphs;
        private static bool exportAssetBundle;
        private static bool enableLoadingFromScene = false;

        internal static SpatialAdapterSceneExporter sceneExporter;
        internal static SpatialAdapterPrefabExporter prefabExporter;
        internal static SpatialAdapterShaderGraphExporter shaderGraphExporter;
        internal static SpatialAdapterMaterialExporter materialExporter;

        private static Dictionary<string, bool> exportResultCache;
        private static int exportedAssetCount = 0;
        private static DateTime? lastBuildTimeStamp;

        public static int remainingSceneCount = 0;

        private class ExportDependenciesScope : IDisposable
        {
            private string assetPath;

            public ExportDependenciesScope(string assetPath)
            {
                this.assetPath = assetPath;
                AssetExportManager.exportResultCache.Add(assetPath, true);
            }

            public void Dispose()
            {
                AssetExportManager.exportResultCache.Remove(this.assetPath);
            }
        }

        public static bool GetSpatialAdapterEnabled()
        {
            var settings = SpatialAdapterRuntimeSettings.GetOrCreateSettings();
            return settings.m_enableSpatialAdapter;
        }

        public static void Initialize(bool isRunningInEditor)
        {
            AssetDatabase.Refresh();

            var settings = SpatialAdapterExporterSettings.GetOrCreateSettings();
            exportFormat = settings.m_Format;
            exportShaderGraphs = settings.m_exportShaderGraphs;
            exportAssetBundle = settings.m_exportAssetBundle;
            
            sceneExporter = new(settings, isRunningInEditor);
            prefabExporter = new(settings);
            shaderGraphExporter = new(settings);
            materialExporter = new(settings);

            exportResultCache = new();
            exportedAssetCount = 0;
            lastBuildTimeStamp = AssetExportHelpers.GetLastBuildTimeStamp();
            remainingSceneCount = EditorBuildSettings.scenes.Count(scene => scene.enabled);
            PruneExportDirectory();
        }
        
        private static bool IsAssetDirty(string assetPath, string exportedPath, AssetType assetType)
        {
            // If there is no last build timestamp, then all assets are dirty
            if (lastBuildTimeStamp == null)
            {
                return true;
            }

            // If the exported file doesn't exist, the asset is considered dirty
            if (!File.Exists(exportedPath) && AssetExportHelpers.IsAssetTypeSupported(assetType))
            {
                return true;
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                return true;
            }
            
            // Compare last write times
            DateTime sourceLastModified = File.GetLastWriteTime(assetPath);
            DateTime exportedLastModified = File.GetLastWriteTime(exportedPath);
            
            // An asset is clean iff both the source and exported files are older than the last build timestamp
            return sourceLastModified > lastBuildTimeStamp.Value
                || exportedLastModified > lastBuildTimeStamp.Value;
        }

        public static void ExportAll(bool isRunningInEditor)
        {
            ExportAllScenes(isRunningInEditor);
            ExportAllResources();
            
            
            //  Clear existing Bundle files
            DeleteExistingBundleFiles();
            
            //  Run Bundle Tool if enabled
            //
            if (exportAssetBundle)
            {
                ExportAssetBundle();
            }
        }

        private static void ExportAllResources()
        {
            var resourcesDirectories = AssetExportHelpers.GetAllResourcesDirectories();

            using (var scope = new ProgressBarScope())
            {
                scope.Display("Exporting all resources...", 1.0f);
                foreach (var resourcesDirectory in resourcesDirectories)
                {
                    var assets = Directory.GetFiles(resourcesDirectory, "*", SearchOption.AllDirectories);
                    foreach (var asset in assets)
                    {
                        ExportAsset(asset);
                    }
                }
            }
        }

        private static void ExportAllScenes(bool isRunningInEditor)
        {
            using (var scope = new ProgressBarScope())
            {
                bool enableLoadingFromScene = true;

                if (isRunningInEditor)
                {
                    var p2dSettings = SpatialAdapterP2DSettings.GetOrCreateSettings();
                    enableLoadingFromScene = p2dSettings.m_enableLoadingFromScene;
                }

                if (enableLoadingFromScene)
                {
                    var scenes = EditorBuildSettings.scenes;
                    for (int i = 0; i < scenes.Length; ++i)
                    {
                        float progress = i / (float)(scenes.Length) * 100;
                        scope.Display("Exporting all scenes in build settings...", progress / 100f);

                        var scene = scenes[i];
                        if (!scene.enabled)
                        {
                            continue;
                        }

                        ExportAsset(scene.path);
                    }
                }

                // Also make sure to export all open scenes
                if (isRunningInEditor)
                {
                    for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                    {
                        float progress = i / (float)(EditorSceneManager.sceneCount) * 100;
                        scope.Display("Exporting all scenes currently open...", progress / 100f);
                        Scene scene = EditorSceneManager.GetSceneAt(i);
                        
                        ExportAsset(scene.path);
                    }
                }

                AssetDatabase.Refresh();
            }
        }

        public static bool ExportAsset(string assetPath)
        {
            if (exportResultCache.TryGetValue(assetPath, out bool result))
            {
                return result;
            }

            // Determine whether this asset is dirty
            var assetType = AssetExportHelpers.GetAssetType(assetPath);
            var exportedPath = AssetExportHelpers.GetExportedAssetPath(assetPath, assetType, exportFormat);
            var isDirty = IsAssetDirty(assetPath, exportedPath, assetType);
            
            // Using ExportDependenciesScope avoids circular dependencies. Unfortunately, this can result in unnecessary re-exports.
            using (var scope = new ExportDependenciesScope(assetPath))
            {
                var dependencies = AssetDatabase.GetDependencies(assetPath, false);
                foreach (var dependency in dependencies)
                {
                    var isDependencyDirty = ExportAsset(dependency);
                    isDirty = isDirty || isDependencyDirty;
                }                
            }

            if (isDirty && assetType != AssetType.None)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(exportedPath));
                ++exportedAssetCount;
            }

            // TODO: Consider using AssetDatabase.GetMainAssetTypeAtPath
            bool shouldAddToCache = false;
            switch (assetType)
            {
                case AssetType.Scene:
                    shouldAddToCache = sceneExporter.RegisterAsset(assetPath, exportedPath, isDirty);
                    break;
                case AssetType.Prefab:
                    shouldAddToCache = prefabExporter.RegisterAsset(assetPath, exportedPath, isDirty);
                    break;
                case AssetType.ShaderGraph:
                    shouldAddToCache = shaderGraphExporter.RegisterAsset(assetPath, exportedPath, isDirty);
                    break;
                case AssetType.Material:
                    shouldAddToCache = materialExporter.RegisterAsset(assetPath, exportedPath, isDirty);
                    break;
                default:
                    break;
            }

            if (shouldAddToCache)
            {
                exportResultCache[assetPath] = isDirty;
            }

            return isDirty;
        }

        public static void SaveResourceData()
        {
            if (!Directory.Exists(AssetExportHelpers.GetExportDirectory()))
            {
                Directory.CreateDirectory(AssetExportHelpers.GetExportDirectory());
            }
            
            AssetDatabase.CreateAsset(GetResourceData(false), k_ResourceDataPath);
            AssetDatabase.SaveAssets();
        }

        public static void CleanResourceData()
        {
            AssetDatabase.DeleteAsset(k_ResourceDataPath);
        }

        public static SpatialAdapterResourceData GetResourceData(bool loadFromAsset)
        {
            if (loadFromAsset)
            {
                return AssetDatabase.LoadAssetAtPath<SpatialAdapterResourceData>(k_ResourceDataPath);
            }
            else
            {
                SpatialAdapterResourceData data = ScriptableObject.CreateInstance<SpatialAdapterResourceData>();
                data.PrefabLookUps = prefabExporter.PathLookUps;
                data.MaterialLookUps = materialExporter.PathLookUps;
                data.ShaderGraphLookUps = shaderGraphExporter.PathLookUps;
                data.DefaultSpatialCameraConfiguration = SpatialAdapterRuntimeSettings.GetOrCreateSettings().m_defaultSpatialCameraConfiguration;
                return data;
            }
        }

        // TODO: Clean when clean build
        public static void CleanExportDirectory()
        {
            Directory.Delete(AssetExportHelpers.GetExportDirectory());
        }

        public static void PruneExportDirectory()
        {
            if (!File.Exists(AssetExportHelpers.GetExportDirectory()))
            {
                return;
            }

            // Get all exported asset paths
            var exportedAssetPaths = Directory.GetFiles(AssetExportHelpers.GetExportDirectory(), "*", SearchOption.AllDirectories);

            foreach (var exportedAssetPath in exportedAssetPaths)
            {
                if (Path.GetExtension(exportedAssetPath) == ".meta")
                {
                    continue;
                }

                var originalAssetPath = AssetExportHelpers.GetOriginalAssetPath(exportedAssetPath);

                // If the original asset path doesn't exist, delete the exported asset
                if (!File.Exists(originalAssetPath))
                {
                    Debug.Log($"Pruning {exportedAssetPath} since original asset {originalAssetPath} no longer exists!");
                    File.Delete(exportedAssetPath);
                }
            }
        }

        public static void OnBuildStarted(bool isRunningInEditor)
        {
            Debug.Log($"Export finished. Re-exported {exportedAssetCount} assets.");

            AssetExportManager.SaveResourceData();

            if (!isRunningInEditor)
            {
                AssetExportHelpers.CopyExportedAssetsToFinalBuild();
            }
        }

        public static void OnBuildEnded(bool isRunningInEditor, bool wasBuildSuccessful = true)
        {
            var settings = SpatialAdapterP2DSettings.GetOrCreateSettings();
            enableLoadingFromScene = settings.m_enableLoadingFromScene;

            using (var scope = new ProgressBarScope())
            {
                scope.Display("Restoring all assets...", 1.0f);
                AssetExportHelpers.RestoreAllAssetsFromBackup();
            }

            if (!isRunningInEditor)
            {
                AssetExportHelpers.CleanFinalBuildDirectory();
            }
            else
            {
                AssetExportHelpers.CleanCurrentScene();
            }

            AssetExportHelpers.CleanBackupDirectory();
            AssetExportManager.CleanResourceData();
        }

        public static void OnSceneProcessed()
        {
            if (--remainingSceneCount == 0)
            {
                OnBuildStarted(false);
            }
        }

        private static void ExportAssetBundle()
        {
            AssetExportHelpers.SaveBuildTimeStamp();
            
            string bundleToolInputRoot = AssetExportHelpers.GetExportDirectory();
            string bundleToolOutputFile = Path.Join(AssetExportHelpers.GetExportDirectory(), "SpatialAssetBundle");

            List<BundleExportFormat> bundleExports = new List<BundleExportFormat>();
            switch (exportFormat)
            {
                case ExportFormat.GLB:
                    bundleExports.Add(BundleExportFormat.glb);
                    break;
                case ExportFormat.USDZ:
                    bundleExports.Add(BundleExportFormat.usdz);
                    break;
            }
            
            //  If exporting ShaderGraphs we also need to export .usda
            if (exportShaderGraphs)
            {
                bundleExports.Add(BundleExportFormat.usda);
            }
            
            
            bool bundleSuccess = BundleToolExportManager.ExportSpatialBundle(bundleToolInputRoot,
                bundleToolOutputFile,
                bundleExports);
            
        }
        
        private static void DeleteExistingBundleFiles()
        {
            string[] bundleFiles = Directory.GetFiles(
                AssetExportHelpers.GetExportDirectory(), 
                "*.bundle", 
                SearchOption.AllDirectories);
            
            foreach (string bundleFile in bundleFiles)
            {
                File.Delete(bundleFile);
            }
        }
    }
}