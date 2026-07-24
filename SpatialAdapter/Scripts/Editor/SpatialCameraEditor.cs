using UnityEditor;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter
{
    [CustomEditor(typeof(SpatialCamera)), CanEditMultipleObjects]
    public class SpatialCameraEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Get the target object
            SpatialCamera camera = (SpatialCamera)target;

            // Draw default properties
            DrawDefaultInspector();

            if (camera.OutputConfiguration != null)
            {
                EditorGUI.BeginDisabledGroup(true);
                using (new EditorGUI.IndentLevelScope())
                {
                    SerializedObject serializedObject = new SerializedObject(camera.OutputConfiguration);
                    serializedObject.Update();
                    SerializedProperty property = serializedObject.GetIterator();
                    property.NextVisible(true); // Skip the generic field

                    // Loop through all visible properties and draw them
                    while (property.NextVisible(false))
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                }
                
                EditorGUI.EndDisabledGroup();

                // Apply changes made to the serialized object
                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                EditorGUILayout.HelpBox("No Spatial Camera Configuration assigned. This will use the Default Spatial Camera Configuration defined in Project Settings > SpatialAdapter", MessageType.Warning);
            }
        }
    }
}