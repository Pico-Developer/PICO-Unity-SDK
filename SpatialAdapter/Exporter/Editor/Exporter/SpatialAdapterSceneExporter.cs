using Plattar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Formats.USD;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    internal class SpatialAdapterSceneExporter : SpatialAdapterAssetExporter<string>
    {
        private bool isRunningInEditor = false;

        public SpatialAdapterSceneExporter(SpatialAdapterExporterSettings inSettings, bool inIsRunningInEditor = false) : base(inSettings) {
            isRunningInEditor = inIsRunningInEditor;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnableAllSpatialAdapterRuntimes()
        {
            SpatialAdapterRuntime[] runtimes = UnityEngine.Object.FindObjectsOfType<SpatialAdapterRuntime>();
            foreach (var runtime in runtimes)
            {
                runtime.enabled = true;
            }
        }

        private struct EditSceneContentsScope : IDisposable
        {
            public Scene scene;

            public EditSceneContentsScope(string scenePath)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            public void Dispose()
            {
                if (scene != null)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static EditSceneContentsScope? TryOpenScene(string scenePath, out Scene scene, bool isRunningInEditor)
        {
            // Checking if scene is currently loaded
            scene = EditorSceneManager.GetSceneByPath(scenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                return null;
            }

            // Otherwise, open the scene
            var scope = new EditSceneContentsScope(scenePath);
            scene = scope.scene;
            return scope;
        }

        public override bool RegisterAsset(string assetPath, string exportedPath, bool isAssetDirty)
        {
            using (var scope = TryOpenScene(assetPath, out var scene, isRunningInEditor))
            {                
                if (scene.isDirty)
                {
                    // Wait until the ProcessScene callback to re-export.
                    if (!isRunningInEditor)
                    {
                        return false;
                    }

                    isAssetDirty = true;
                    exportedPath = AssetExportHelpers.MarkDirty(exportedPath);
                }

                Debug.Log($"Exporting scene {assetPath} to {exportedPath} (dirty: {isAssetDirty}, scene.isDirty: {scene.isDirty}, isRunningInEditor: {isRunningInEditor}, scope: {scope})");

                if (isAssetDirty)
                {
                    TemporaryTextMeshProExportScope.SetTextMeshProsTemporaryData(scene.GetRootGameObjects());
                    ExportScene(scene, exportedPath, settings);
                }
                
                // Handle P2D case
                if (isRunningInEditor)
                {
                    ProcessScene(scene, exportedPath, settings);

                    // If the scene is not currently open, then save the scene
                    if (scope != null)
                    {
                        AssetExportHelpers.StoreBackupAsset(assetPath);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
                TemporaryTextMeshProExportScope.RestoreTextMeshProsFromTemporaryData();
            }

            return true;
        }

        private static SpatialAdapterSceneData GetSceneData(Scene scene, string exportedPath)
        {
            List<GameObject> exportRootsList = scene.GetRootGameObjects().ToList().Where(
                root => !root.TryGetComponent<SpatialAdapterRuntime>(out var _)
            ).ToList();

            return new SpatialAdapterSceneData {
                scenePath = scene.path,
                exportedScenePath = exportedPath.Relativize(),
                rootObjects = exportRootsList
            };
        }

        internal static void ProcessScene(Scene scene, string exportedPath, SpatialAdapterExporterSettings settings)
        {
            var runtimeSettings = SpatialAdapterRuntimeSettings.GetOrCreateSettings();
            var p2dSettings = SpatialAdapterP2DSettings.GetOrCreateSettings();

            if (!runtimeSettings.m_enableSpatialAdapter)
            {
                return;
            }
            
            Debug.Log($"Processing scene={scene.name}, exportedPath={exportedPath}");

            // Find the scene's MSR instance
            SpatialAdapterRuntime[] runtimes = UnityEngine.Object.FindObjectsOfType<SpatialAdapterRuntime>();
            var runtime = runtimes.Where(x => x.gameObject.scene == scene).FirstOrDefault();
            if (runtime == null)
            {
                runtime = new GameObject("SpatialAdapter").AddComponent<SpatialAdapterRuntime>();
                SceneManager.MoveGameObjectToScene(runtime.gameObject, scene);
            }

            // Configure the scene's MSR instance
            runtime.EnableSpatialAdapter = runtimeSettings.m_enableSpatialAdapter;
            runtime.EnableRendering = p2dSettings.m_enableRendering;
            
            runtime.autoCreateSpatialCamera = runtimeSettings.m_autoCreateSpatialCamera;
            runtime.colliderLayerMask = runtimeSettings.m_colliderLayerMask;
            runtime.disabledFeatures = runtimeSettings.m_disabledFeatures;
            runtime.exportFormat = settings.m_Format;
            runtime.disableUnlitTonemapping = runtimeSettings.m_disableUnlitTonemapping;
            runtime.autoAddUIHoverEffect = runtimeSettings.m_autoAddUIHoverEffect;

            runtime.resourceData = AssetExportManager.GetResourceData(false);
            runtime.sceneData = GetSceneData(scene, exportedPath);
            
        }

        public static void ExportScene(Scene scene, string exportedPath, SpatialAdapterExporterSettings settings)
        {
            var exportRoots = scene.GetRootGameObjects().ToList().Where(
                x => x.GetComponent<SpatialAdapterRuntime>() == null
            ).ToArray();

            Debug.Log($"Exporting {exportRoots.Length} roots for scene {scene.name} to {exportedPath}");
            switch (settings.m_Format)
            {
                case ExportFormat.USDZ:
                    {
                        try
                        {
                            UsdzExporter.ExportObjectsToUsdz(exportedPath, exportRoots);
                        }
                        catch (System.Exception ex)
                        {
                            throw new BuildFailedException($"Multi Spatial Exporter: Failed to convert {ExportFormat.USDZ} in scene {scene.name} with error message {ex}");
                        }
                    }
                    break;

                case ExportFormat.GLB:
                    {
                        // this ensures skinned mesh is exported
                        PlattarExporterOptions.ExportAnimations = true;
                        // animation clips export based on settings
                        PlattarExporterOptions.ExportAnimationClips = settings.m_ExportAnimationClips;
                        GLTFTextureUtils.textureOption = settings.m_TextureExportOption;
                        if (Plattar.Exporter.GenerateGLTF(exportRoots, Path.GetFileNameWithoutExtension(exportedPath), exportedPath, true) == null)
                        {
                            throw new BuildFailedException($"Multi Spatial Exporter: Failed to convert {ExportFormat.GLB} in scene {scene.name}");
                        }
                    }
                    break;

                default:
                    throw new BuildFailedException("Multi Spatial Exporter: Invalid export format. Format must be either GLB or USDZ");
            }
        }

        public static void RestoreAsset(string assetPath, bool isRunningInEditor)
        {
            // TODO: Restore assets with file copy instead of manually deleting the MSR instance
            using (var scope = TryOpenScene(assetPath, out var scene, isRunningInEditor))
            {
                var runtimes = UnityEngine.Object.FindObjectsOfType<SpatialAdapterRuntime>().Where(x => x.gameObject.scene == scene);

                foreach (var runtime in runtimes)
                {
                    UnityEngine.Object.DestroyImmediate(runtime.gameObject);
                }

                if (!scene.isDirty && scope != null)
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }
    }
}
