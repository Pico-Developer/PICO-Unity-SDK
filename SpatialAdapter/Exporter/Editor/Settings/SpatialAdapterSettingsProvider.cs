using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    public class SpatialAdapterSettingsProvider
    {
		[SettingsProvider]
		public static SettingsProvider CreateMyCustomSettingsProvider()
		{
			// First parameter is the path in the Settings window.
			// Second parameter is the scope of this setting: it only appears in the Project Settings window.
			var provider = new SettingsProvider("Project/SpatialAdapter", SettingsScope.Project)
			{
				// By default the last token of the path is used as display name if no label is provided.
				label = "PICO Spatial",
				// Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
				guiHandler = (searchContext) =>
				{
                    var headerStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 16,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleLeft
                    };

					var exporterSettings = SpatialAdapterExporterSettings.GetSerializedSettings();
					var runtimeSettings = SpatialAdapterRuntimeSettings.GetSerializedSettings();
					var p2dSettings = SpatialAdapterP2DSettings.GetSerializedSettings();

					// Adjust field label width to fit in longer names
					EditorGUIUtility.labelWidth = 250.0f;
					using (new EditorGUI.IndentLevelScope())
					{
						if (!SpatialAdapterSetup.IsValid)
							EditorGUILayout.HelpBox("The project needs to be configured to support SpatialAdapter",
								MessageType.Warning);
						
						GUILayout.BeginHorizontal();	
						GUILayout.Space(20);
						if (GUILayout.Button("Setup Project", GUILayout.Width(200f)))
						{
							SpatialAdapterSetup.Setup();
						}

						GUILayout.EndHorizontal();

						EditorGUILayout.Space();
						EditorGUILayout.LabelField("Runtime Settings", headerStyle);
						EditorGUILayout.Space();
						using (new EditorGUI.IndentLevelScope())
						{
							EditorGUILayout.PropertyField(runtimeSettings.FindProperty("m_enableSpatialAdapter"),
								new GUIContent("Enable SpatialAdapter Runtime"));
							EditorGUILayout.PropertyField(runtimeSettings.FindProperty("m_colliderLayerMask"),
								new GUIContent("SpatialAdapter Collider Layer Mask"));
							EditorGUILayout.PropertyField(runtimeSettings.FindProperty("m_defaultSpatialCameraConfiguration"),
								new GUIContent("Default Spatial Camera Configuration"));
							EditorGUILayout.PropertyField(runtimeSettings.FindProperty("m_autoCreateSpatialCamera"),
								new GUIContent("Auto-Create Spatial Camera"));
							EditorGUILayout.PropertyField(runtimeSettings.FindProperty("m_disabledFeatures"),
								new GUIContent("Disabled Features"));
							EditorGUILayout.PropertyField(runtimeSettings.FindProperty("m_disableUnlitTonemapping"),
								new GUIContent("Disable Unlit Material Tonemapping"));
							EditorGUILayout.PropertyField(runtimeSettings.FindProperty("m_autoAddUIHoverEffect"),
								new GUIContent("Auto Add UI Hover Effect"));
						}

						EditorGUILayout.Space();
						EditorGUILayout.LabelField("Play-to-PICO Settings", headerStyle);
						EditorGUILayout.Space();
						using (new EditorGUI.IndentLevelScope())
						{
							EditorGUILayout.PropertyField(p2dSettings.FindProperty("m_serverIPAddress"),
								new GUIContent("Host Server IP Address"));
							EditorGUILayout.PropertyField(p2dSettings.FindProperty("m_enableRendering"),
								new GUIContent("Enable Rendering"));
							EditorGUILayout.PropertyField(p2dSettings.FindProperty("m_enableLoadingFromScene"),
								new GUIContent("Enable Loading From Scene"));
						}

						EditorGUILayout.Space();
						EditorGUILayout.LabelField("Exporter Settings", headerStyle);
						EditorGUILayout.Space();
						using (new EditorGUI.IndentLevelScope())
						{
							EditorGUILayout.PropertyField(exporterSettings.FindProperty("m_Format"),
								new GUIContent("Export Format"));
							EditorGUILayout.PropertyField(exporterSettings.FindProperty("m_TextureExportOption"),
								new GUIContent("Texture Format"));
							EditorGUILayout.PropertyField(exporterSettings.FindProperty("m_exportMaterials"),
								new GUIContent("Export Materials"));
							EditorGUILayout.PropertyField(exporterSettings.FindProperty("m_exportShaderGraphs"),
								new GUIContent("Export ShaderGraphs"));
							EditorGUILayout.PropertyField(exporterSettings.FindProperty("m_exportAssetBundle"),
								new GUIContent("Export Spatial Asset Bundle"));
							
						}
					}

					exporterSettings.ApplyModifiedPropertiesWithoutUndo();
                    runtimeSettings.ApplyModifiedPropertiesWithoutUndo();
                    p2dSettings.ApplyModifiedPropertiesWithoutUndo();
				},

				// Populate the search keywords to enable smart search filtering and label highlighting:
				keywords = new HashSet<string>(new[] { "Export Scene", "Export Root", "Export Format", "Export Animations, Export Spatial Asset Bundle" })
			};

			return provider;
		}
    }
}
