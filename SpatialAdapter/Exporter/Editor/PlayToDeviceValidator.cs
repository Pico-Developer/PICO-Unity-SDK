using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
	public static class PlayToDeviceValidator
	{
        private static HashSet<string> scenesOpenInEditor = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            var runtimeSettings = SpatialAdapterRuntimeSettings.GetOrCreateSettings();
            if (!runtimeSettings.m_enableSpatialAdapter)
            {
                return;
            }

            var p2dSettings = SpatialAdapterP2DSettings.GetOrCreateSettings();
            if (!p2dSettings.m_enableLoadingFromScene)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    scenesOpenInEditor.Add(SceneManager.GetSceneAt(i).path);
                }

                SceneManager.sceneLoaded += CheckValidSceneLoaded;
            }

            SpatialAdapterRuntime.OnSendResourceFiles += OnSendResourceFiles;
        }

        public static void CheckValidSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scenesOpenInEditor.Contains(scene.path))
            {
                if (EditorUtility.DisplayDialog("SpatialAdapter Play-to-Device",
                        "Detected loading from scene in SpatialAdapter Play-to-Device, but \"Loading From Scene\" setting isn't enabled. Do you want to enable it?", "Enable it", "Do not enable it"))
                {
                    EditorApplication.isPlaying = false;
                    var p2dSettings = SpatialAdapterP2DSettings.GetSerializedSettings();
                    
                    p2dSettings.FindProperty("m_enableLoadingFromScene").boolValue = true;
                    p2dSettings.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    Debug.LogError("Detected loading from scene in SpatialAdapter Play-to-Device, but loading from scene not enabled.");
                }
            }
        }

        static void OnSendResourceFiles(List<string> filenames)
        {
            using (var scope = new ProgressBarScope())
            {
                for (int i = 0; i < filenames.Count; ++i)
                {
                    scope.Display($"Streaming files to device... ({i} of {filenames.Count} sent)", i / (float)filenames.Count);
                    SpatialAdapterRuntime.SendFile(filenames[i]);
                }
            }
        }
    }
}