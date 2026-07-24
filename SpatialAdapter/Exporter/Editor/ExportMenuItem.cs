#if SPATIAL_ADAPTER_DEBUG
﻿using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    public class ExportMenuItem
    {
        [MenuItem("SpatialAdapter/Export Shader Graph", true)]
        private static bool ValidateExportShaderGraph()
        {
            // Validate if the selected object is a Shader Graph asset
            return Selection.activeObject && AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".shadergraph");
        }
        
        [MenuItem("SpatialAdapterExporter/Export Shader Graph")]
        private static void ExportShaderGraph()
        {
            // Opens a folder panel to select a directory
            string outputDir = EditorUtility.OpenFolderPanel(
                "Select a directory to save MaterialX USD files",   // Title of the panel
                "",                     // Default directory (empty means the last folder used)
                ""                      // Default name (empty since we're just selecting a folder)
            );
            
            if (string.IsNullOrEmpty(outputDir))
            {
                Debug.LogError("Invalid output directory");
                return;
            }
            
            for (int i = 0; i < Selection.objects.Length; i++)
            {
                var graphObj = Selection.objects[i];
                string shaderGraphPath = AssetDatabase.GetAssetPath(graphObj);
                ShaderGraphExporter.ExportShaderGraph(shaderGraphPath, Path.Combine(outputDir, Path.GetFileName(shaderGraphPath)),"");
            }
        }
        
        [MenuItem("SpatialAdapterExporter/Bundle Tool Test")]
        private static void RunBundleToolTest()
        {
            // Get the path of the selected Shader Graph
            string assetsRootPath = AssetExportHelpers.GetExportDirectory();
            
            // Opens a folder panel to select a directory
            string bundleOutputDir = "BundleOut/";
            bundleOutputDir = Path.Combine(bundleOutputDir, "SpatialBundleOutput");
            List<BundleExportFormat> formats = new List<BundleExportFormat>()
            {
                BundleExportFormat.glb,
                BundleExportFormat.usda
            };
            if (string.IsNullOrEmpty(bundleOutputDir))
            {
                Debug.LogError("Invalid output directory");
            }
            else
            {
                BundleToolExportManager.ExportSpatialBundle(assetsRootPath,
                    bundleOutputDir, 
                    formats);
            }

        }
    }
}
#endif