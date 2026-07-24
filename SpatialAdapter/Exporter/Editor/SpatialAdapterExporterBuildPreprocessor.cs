using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    class SpatialAdapterExporterBuildPreprocessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        EditorApplication.CallbackFunction onEditorUpdate;

        public int callbackOrder { get { return 0; } }

        private bool errorExists = false;
        private bool buildInProgress = false;

        private static EditorApplication.CallbackFunction onBuildSucceededCallback = () => {
            EditorApplication.delayCall -= onBuildSucceededCallback;
            AssetExportManager.OnBuildEnded(false);
            AssetDatabase.AllowAutoRefresh();
            AssetDatabase.Refresh();
        };

        private static EditorApplication.CallbackFunction onBuildFailedCallback = () => {
            EditorApplication.delayCall -= onBuildFailedCallback;
            AssetExportManager.OnBuildEnded(false, false);
            AssetDatabase.AllowAutoRefresh();
            AssetDatabase.Refresh();
        };

        private void OnBuildError(string condition, string stacktrace, LogType type)
        {
            // If build failed, clean up any scene / prefab modifications
            if (type == LogType.Error)
            {
                Application.logMessageReceived -= OnBuildError;

                errorExists = true;

                if (AssetExportManager.GetSpatialAdapterEnabled()
                    && buildInProgress)
                {
                    buildInProgress = false;
                    AssetDatabase.DisallowAutoRefresh();
                    EditorApplication.delayCall += onBuildFailedCallback;
                }
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            // Stop listening for build errors
            Application.logMessageReceived -= OnBuildError;

            if (AssetExportManager.GetSpatialAdapterEnabled()
                && buildInProgress)
            {
                buildInProgress = false;
                AssetDatabase.DisallowAutoRefresh();
                EditorApplication.delayCall += onBuildSucceededCallback;
            }
        }
        
        public void OnPreprocessBuild(BuildReport report)
        {
            // Start listening for build errors
            Application.logMessageReceived += OnBuildError;

            if (AssetExportManager.GetSpatialAdapterEnabled()
                && report != null)
            {
                buildInProgress = true;
                AssetExportManager.Initialize(false);

                if (!errorExists)
                {
                    AssetExportManager.ExportAll(false);
                }

                if (!errorExists)
                {
                    AssetExportManager.OnBuildStarted(false);
                }
            }
        }
    }
}