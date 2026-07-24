using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ByteDance.PICO.SpatialAdapter.Component
{
    [CustomEditor(typeof(SpatialAdapterNativeText))]
    [CanEditMultipleObjects]
    public class SpatialAdapterNativeTextEditor : UnityEditor.Editor 
    {        
        public override void OnInspectorGUI()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };    

            serializedObject.Update();

            EditorGUILayout.LabelField("Text Properties", headerStyle);
            EditorGUILayout.Space();
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_text"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_textColor"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_textSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_justification"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Background Properties", headerStyle);
            EditorGUILayout.Space();
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_backgroundColor"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_backgroundSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_backgroundCornerRadius"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

}
