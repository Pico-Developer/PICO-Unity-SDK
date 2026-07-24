using System.IO;
using UnityEditor;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    public class SpatialAdapterP2DSettings : ScriptableObject
    {
        internal const string k_LocalHost = "127.0.0.1";

        internal const int k_Port = 27182;

        public const string k_SettingsPath = "Assets/Settings/SpatialAdapterP2DSettings.asset";

        [SerializeField]
        [Tooltip("IP address of Play-to-PICO host server. This is displayed in your Pico HMD Device by running the server run.sh script")]
        internal string m_serverIPAddress = k_LocalHost;
		
        [SerializeField]
        [Tooltip("Whether rendering is enabled during Play To PICO. When set to true, the game will be rendered in Game View during Play Mode.")]
        internal bool m_enableRendering = true;

        [SerializeField]
        [Tooltip("Whether scenes can be loaded during Play-to-PICO. When set to true, you can load and unload scenes while in Play Mode. However, it will take longer to enter and exit Play Mode.")]
        internal bool m_enableLoadingFromScene = false;
        
        internal static SpatialAdapterP2DSettings GetOrCreateSettings()
        {
            string settingsDirectory = Path.GetDirectoryName(k_SettingsPath);
            if (!Directory.Exists(settingsDirectory)) {
                Directory.CreateDirectory(settingsDirectory);
            }
            var settings = AssetDatabase.LoadAssetAtPath<SpatialAdapterP2DSettings>(k_SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SpatialAdapterP2DSettings>();
                AssetDatabase.CreateAsset(settings, k_SettingsPath);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }
}