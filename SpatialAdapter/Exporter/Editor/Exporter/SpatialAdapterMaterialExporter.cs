using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityGLTF;
using Object = System.Object;
using TMPro;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    internal class SpatialAdapterMaterialExporter : SpatialAdapterAssetExporter<Material>
    {        
        public SpatialAdapterMaterialExporter(SpatialAdapterExporterSettings inSettings) : base(inSettings) {}

        public override bool RegisterAsset(string assetPath, string exportedPath, bool isAssetDirty)
        {
            Material material;
            
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset is Material)
            {
                material = asset as Material;
            }
            else if (asset is TMP_Asset)
            {
                material = (asset as TMP_Asset).material;
            }
            else
            {
                Debug.LogWarning($"Multi Spatial Material Exporter: Invalid material at {assetPath}");
                return true;
            }

            AddExportedPath(material, exportedPath);

            if (!isAssetDirty)
            {
                return true;
            }
            
            // Create an empty cube and change material to the export target
            GameObject materialQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            materialQuad.name = material.name;
            var renderer = materialQuad.GetComponent<Renderer>();
            Assert.IsNotNull(renderer);
            renderer.sharedMaterial = material;

            try
            {
                GLTFTextureUtils.textureOption = settings.m_TextureExportOption;
                if (Plattar.Exporter.GenerateGLTF(new GameObject[] { materialQuad }, Path.GetFileNameWithoutExtension(exportedPath), exportedPath,
                        true) == null)
                {
                    throw new BuildFailedException(
                        $"Multi Spatial Exporter: Failed to convert material {material.name} to {ExportFormat.GLB}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(materialQuad);
            }

            return true;
        }
    }
}