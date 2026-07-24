using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    [InitializeOnLoad]
    public static class PlayToDeviceExporter
    {
        static bool compileErrorsExist = false;

        static PlayToDeviceExporter()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            CompilationPipeline.assemblyCompilationFinished += ProcessBatchModeCompileFinish;
        }

        private static void ProcessBatchModeCompileFinish(string s, CompilerMessage[] compilerMessages)
        {
            var numCompileErrors = compilerMessages.Count(m => m.type == CompilerMessageType.Error);
            compileErrorsExist = numCompileErrors > 0;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (compileErrorsExist)
            {
                return;
            }

            if (!AssetExportManager.GetSpatialAdapterEnabled())
            {
                return;
            }

            if (state == PlayModeStateChange.ExitingEditMode)
            {
                AssetExportManager.Initialize(true);
                AssetExportManager.ExportAll(true);
                AssetExportManager.OnBuildStarted(true);
                StartPlayToDevice();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                AssetExportManager.OnBuildEnded(true);
            }
        }

        public static void StartPlayToDevice()
        {
            var p2dSettings = SpatialAdapterP2DSettings.GetOrCreateSettings();
            string serverIPAddress = p2dSettings.m_serverIPAddress;
            int port = SpatialAdapterP2DSettings.k_Port;

            using (var scope = new ProgressBarScope())
            {
                scope.Display("Connecting to PICO...", 1.0f);

                // If ADB connection exists, then use that for P2D
                if (ADBCommands.TryGetDevices(out var deviceSerials))
                {
                    foreach (var deviceSerial in deviceSerials)
                    {
                        if (!ADBCommands.IsServerRunningOnDevice(deviceSerial))
                        {
                            // Don't open server for user! 
                            continue;
                        }
                        
                        ADBCommands.ResetPortForwarding(deviceSerial);
                        ADBCommands.ForwardPort(port, port, deviceSerial);
                        
                        if (SpatialAdapterRuntime.ConnectDevice(SpatialAdapterP2DSettings.k_LocalHost, port.ToString()) == 0)
                        {
                            Debug.Log($"Starting Play-to-PICO over USB connection with Device {deviceSerial}...");
                            return;
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(serverIPAddress)) 
                {
                    if (SpatialAdapterRuntime.ConnectDevice(serverIPAddress, port.ToString()) == 0)
                    {
                        Debug.Log($"Starting Play-to-PICO over Wireless connection with IP {serverIPAddress}...");
                        return;
                    }
                }
                
                Debug.LogError("SpatialAdapter Play-to-PICO: Failed to connect to device");
            }
        }
    }
}
