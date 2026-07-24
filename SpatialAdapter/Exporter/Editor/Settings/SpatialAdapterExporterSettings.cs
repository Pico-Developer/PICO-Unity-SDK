using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Serialization;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
	public class SpatialAdapterExporterSettings : ScriptableObject
	{
		public const string k_SettingsPath = "Assets/Editor/SpatialAdapterExporterSettings.asset";

		[SerializeField]
		internal ExportFormat m_Format = ExportFormat.GLB;

		[SerializeField]
		internal bool m_ExportAnimationClips = false;

		[SerializeField]
		internal GLTFTextureUtils.TextureExportOption m_TextureExportOption = GLTFTextureUtils.TextureExportOption.PNG;
		
		[SerializeField]
		[Tooltip("Whether to export material dependencies of the scenes as separate files, check this option only when you need to swap materials at runtime")]
		internal bool m_exportMaterials = true;

		[SerializeField]
		[Tooltip("Whether to export shader graphs, notice ShaderGraph is partially supported at this time")]
		internal bool m_exportShaderGraphs = false;
		
		[SerializeField]
		[Tooltip("Create Spatial Asset Bundle from exported files.")]
		internal bool m_exportAssetBundle = false;

		internal static SpatialAdapterExporterSettings GetOrCreateSettings()
		{
			string settingsDirectory = Path.GetDirectoryName(k_SettingsPath);
			if (!Directory.Exists(settingsDirectory)) {
				Directory.CreateDirectory(settingsDirectory);
			}
			var settings = AssetDatabase.LoadAssetAtPath<SpatialAdapterExporterSettings>(k_SettingsPath);
			if (settings == null)
			{
				settings = ScriptableObject.CreateInstance<SpatialAdapterExporterSettings>();
				AssetDatabase.CreateAsset(settings, k_SettingsPath);
				AssetDatabase.SaveAssets();
			}
			return settings;
		}

		internal static SerializedObject GetSerializedSettings()
		{
			return new SerializedObject(GetOrCreateSettings());
		}

		protected void OnEnable()
        {
			if (!Enum.IsDefined(typeof(ExportFormat), m_Format))
			{
				m_Format = ExportFormat.GLB;
			}
        }

        protected void OnValidate()
        {
			if (!Enum.IsDefined(typeof(ExportFormat), m_Format))
			{
				m_Format = ExportFormat.GLB;
			}
        }
	}
}
