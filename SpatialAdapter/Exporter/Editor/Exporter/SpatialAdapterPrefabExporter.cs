using Plattar;
using System.Collections.Generic;
using System.IO;
using Unity.Formats.USD;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    internal class SpatialAdapterPrefabExporter : SpatialAdapterAssetExporter<int>
    {
        public SpatialAdapterPrefabExporter(SpatialAdapterExporterSettings inSettings) : base(inSettings) {}

        internal static void SanitizeRecursively(Transform xform)
        {
            var gameObject = xform.gameObject;
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
            }

            foreach (Transform child in xform)
            {
                SanitizeRecursively(child);
            }
        }

        internal static int PreprocessPrefab(GameObject prefab)
        {
            SanitizeRecursively(prefab.transform);
            
            var idComponent = prefab.GetComponent<PrefabId>();
            if (idComponent == null)
            {
                idComponent = prefab.AddComponent<PrefabId>();
            }
            idComponent.PrefabIdentifier = prefab.GetInstanceID();

            if (prefab.GetComponent<SpatialAdapterGameObjectTracker>() == null)
            {
                prefab.AddComponent<SpatialAdapterGameObjectTracker>();
            }

            return idComponent.PrefabIdentifier;
        }

        public override bool RegisterAsset(string assetPath, string exportedPath, bool isAssetDirty)
        {
            if (!assetPath.StartsWith("Assets/"))
            {
                return true;
            }

            AssetExportHelpers.StoreBackupAsset(assetPath);

            using (var editingScope = new PrefabUtility.EditPrefabContentsScope(assetPath))
            {
                var prefab = editingScope.prefabContentsRoot;
                int prefabId = PreprocessPrefab(prefab);

                AddExportedPath(prefabId, exportedPath);

                if (!isAssetDirty)
                {
                    return true;
                }

                switch(settings.m_Format)
                {
                    case ExportFormat.USDZ:
                    {
                        try
                        {
                            UsdzExporter.ExportObjectsToUsdz(exportedPath, new GameObject[] { prefab });
                        }
                        catch (System.Exception ex)
                        {
                            throw new BuildFailedException(
                                $"Multi Spatial Exporter: Failed to convert {ExportFormat.USDZ} for prefab {exportedPath} with error message {ex}");
                        }
                    }
                        break;
                
                    case ExportFormat.GLB:
                    {
                        // This ensures skinned mesh is exported
                        PlattarExporterOptions.ExportAnimations = true;
                        // Animation clips export based on settings
                        PlattarExporterOptions.ExportAnimationClips = settings.m_ExportAnimationClips;
                        GLTFTextureUtils.textureOption = settings.m_TextureExportOption;
                        var res = Plattar.Exporter.GenerateGLTF(new GameObject[] { prefab }, Path.GetFileNameWithoutExtension(exportedPath), exportedPath, true);
                        if (res == null)
                        {
                            throw new BuildFailedException(
                                $"Multi Spatial Exporter: Failed to convert {ExportFormat.GLB} for prefab {exportedPath}");
                        }
                    }
                        break;

                    default:
                        throw new BuildFailedException("Multi Spatial Exporter: Invalid export format. Format must be either GLB or USDZ");
                }
            }

            return true;
        }
    }
}
