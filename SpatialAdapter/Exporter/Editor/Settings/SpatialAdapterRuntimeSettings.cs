using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.Build;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    public class SpatialAdapterRuntimeSettings : ScriptableObject
    {
        public const string k_SettingsPath = "Assets/Settings/SpatialAdapterRuntimeSettings.asset";

        [SerializeField]
        [Tooltip("Whether SpatialAdapter is enabled.")]
        internal bool m_enableSpatialAdapter = true;
        
        [SerializeField]
        [Tooltip("Only colliders in these layers are synced and interacted with Backend and Spatial Engine. For better performance, applications should use this mask to reduce collision computations.")]

        internal LayerMask m_colliderLayerMask = 1 << 5 | 1 << 0; // UI | Default layer
        internal const string k_DefaultSpatialCameraConfigurationPath = "Packages/com.bytedance.pico.spatialadapter/Resources/Default Spatial Camera Configuration.asset";

        [SerializeField]
        internal SpatialCameraConfiguration m_defaultSpatialCameraConfiguration;

        [SerializeField]
        [Tooltip("When a new scene is loaded, automatically creates a Spatial Camera if none exist. If disabled, the developer should be expected to add a Spatial Camera to each scene or create one manually from script.")]
        internal bool m_autoCreateSpatialCamera = true;

        [SerializeField]
        [Tooltip("Features disabled for performance or debugging purposes.")]
        internal SpatialAdapterFeatureSet m_disabledFeatures = SpatialAdapterFeatureSet.None;

        [SerializeField]
        [Tooltip("Disable Tonemapping on Unlit Materials.")]
        internal bool m_disableUnlitTonemapping = false;

        [SerializeField]
        [Tooltip("Automatically adds SpatialAdapterHoverEffect to Unity UI Selectable objects at runtime.")]
        internal bool m_autoAddUIHoverEffect = false;

        public const string k_SpatialAdapterSDKDefine = "PICO_MS_SDK";
        public const string k_PicoSDKPackageName = "com.bytedance.pico.xr";
        internal static bool m_firstTimeCalled = true;

        internal static SpatialAdapterRuntimeSettings GetOrCreateSettings()
        {
            string settingsDirectory = Path.GetDirectoryName(k_SettingsPath);
            if (!Directory.Exists(settingsDirectory)) {
                Directory.CreateDirectory(settingsDirectory);
            }
            var settings = AssetDatabase.LoadAssetAtPath<SpatialAdapterRuntimeSettings>(k_SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SpatialAdapterRuntimeSettings>();
                AssetDatabase.CreateAsset(settings, k_SettingsPath);
                AssetDatabase.SaveAssets();
            }

            //Check whether PicoSDK has enabled SpatialAdapter
            string[] defineSymbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';');
            List<string> defineSymbolsList = new List<string>(defineSymbols);

            bool defineSymbolsContainMSSDK = defineSymbolsList.Contains(k_SpatialAdapterSDKDefine);
            bool isPicoSDKInstalled = IsPackageInstalled(k_PicoSDKPackageName);

            settings.m_enableSpatialAdapter = defineSymbolsContainMSSDK;

            return settings;
        }

        [InitializeOnLoadMethod]
        static void InitializeSpatialAdapterDefine()
        {
            bool isPicoSDKInstalled = IsPackageInstalled(k_PicoSDKPackageName);

            //Add PICO_MS_SDK symbol define if used as a standalone package without XR SDK
            if (!isPicoSDKInstalled)
            {
                string[] defineSymbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';');
                List<string> defineSymbolsList = new List<string>(defineSymbols);
                if (defineSymbolsList.Count > 0)
                {
                    if (!defineSymbolsList.Contains(k_SpatialAdapterSDKDefine))
                    {
                        string joinedString = string.Join(";", defineSymbolsList) + ";" + k_SpatialAdapterSDKDefine + ";";
                        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, joinedString);
                    }
                }
            }
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }

        protected void OnEnable()
        {
            Sanitize();
        }

        protected void OnValidate()
        {
            Sanitize();

            if (IsScriptReferenceBroken(this, nameof(m_defaultSpatialCameraConfiguration)))
            {
                Debug.LogError("m_defaultSpatialCameraConfiguration has a broken script reference!");
            }
        }

        internal void Sanitize()
        {
            // Clear any broken script reference first; otherwise the C# field is non-null
            // but unresolvable, and the null-coalescing fallback below won't trigger.
            ClearBrokenReference(this, nameof(m_defaultSpatialCameraConfiguration));

            // Use Unity's overloaded equality to also handle fake-null managed references.
            if (m_defaultSpatialCameraConfiguration == null)
            {
                m_defaultSpatialCameraConfiguration = AssetDatabase.LoadAssetAtPath<SpatialCameraConfiguration>(k_DefaultSpatialCameraConfigurationPath);
            }
        }

        private static bool IsScriptReferenceBroken(UnityEngine.Object owner, string propertyName)
        {
            var serializedObject = new SerializedObject(owner);
            var property = serializedObject.FindProperty(propertyName);

            if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
            {
                return property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0;
            }

            return false;
        }

        private static void ClearBrokenReference(UnityEngine.Object owner, string propertyName)
        {
            var serializedObject = new SerializedObject(owner);
            var property = serializedObject.FindProperty(propertyName);

            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            {
                return;
            }

            // Broken: managed reference resolves to null but instance id is non-zero.
            if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
            {
                property.objectReferenceInstanceIDValue = 0;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static bool IsPackageInstalled(string packageId)
        {
            if (!File.Exists("Packages/manifest.json"))
                return false;

            string jsonText = File.ReadAllText("Packages/manifest.json");
            return jsonText.Contains(packageId);
        }
    }
}
