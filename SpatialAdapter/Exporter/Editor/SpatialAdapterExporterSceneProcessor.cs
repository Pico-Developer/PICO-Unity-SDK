using Plattar;
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
using UnityEngine.UI;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    class SpatialAdapterExporterSceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // P2D Scene processing is handled separately
            bool isRunningInEditor = (report == null);            
            if (isRunningInEditor)
            {
                return;
            }

            var runtimeSettings = SpatialAdapterRuntimeSettings.GetOrCreateSettings();
            if (!runtimeSettings.m_enableSpatialAdapter)
            {
                return;
            }

            var settings = SpatialAdapterExporterSettings.GetOrCreateSettings();
            var exportedPath = AssetExportHelpers.GetExportedAssetPath(scene.path, settings.m_Format);
            
            // Make sure scene has been exported.
            AssetExportManager.ExportAsset(scene.path);
            SpatialAdapterSceneExporter.ProcessScene(scene, exportedPath, settings);

            AssetExportManager.OnSceneProcessed();
        }
    }
}