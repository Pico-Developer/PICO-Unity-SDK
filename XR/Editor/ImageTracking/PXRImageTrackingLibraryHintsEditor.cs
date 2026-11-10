#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Collections.Generic;
using System.IO;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR.Editor
{
    internal static class PXRImageTrackingLibraryHintsEditorUtility
    {
        internal static void SyncFromReferenceLibrary(
            PXR_ImageTrackingLibraryHints hints,
            bool removeMissing)
        {
            if (hints == null || hints.ReferenceLibrary == null)
            {
                return;
            }

            var previous = new Dictionary<Guid, bool>();
            IReadOnlyList<PXR_ImageTrackingReferenceHint> current = hints.Images;
            for (int index = 0; index < current.Count; index++)
            {
                PXR_ImageTrackingReferenceHint hint = current[index];
                if (hint.ImageGuid != Guid.Empty && !previous.ContainsKey(hint.ImageGuid))
                {
                    previous.Add(hint.ImageGuid, hint.OptimizeForStaticImage);
                }
            }

            var next = new List<PXR_ImageTrackingReferenceHint>();
            XRReferenceImageLibrary library = hints.ReferenceLibrary;
            for (int index = 0; index < library.count; index++)
            {
                XRReferenceImage image = library[index];
                previous.TryGetValue(image.guid, out bool optimizeForStaticImage);
                next.Add(new PXR_ImageTrackingReferenceHint(
                    image.name,
                    image.guid,
                    optimizeForStaticImage));
            }

            if (!removeMissing)
            {
                var nextGuids = new HashSet<Guid>();
                for (int index = 0; index < next.Count; index++)
                {
                    nextGuids.Add(next[index].ImageGuid);
                }

                for (int index = 0; index < current.Count; index++)
                {
                    PXR_ImageTrackingReferenceHint hint = current[index];
                    if (hint.ImageGuid != Guid.Empty && !nextGuids.Contains(hint.ImageGuid))
                    {
                        next.Add(hint);
                    }
                }
            }

            Undo.RecordObject(hints, "Sync PICO Image Tracking Hints");
            SetHintsForEditor(hints, next);
            EditorUtility.SetDirty(hints);
        }

        internal static void SetAllStatic(
            PXR_ImageTrackingLibraryHints hints,
            bool optimizeForStaticImage)
        {
            if (hints == null)
            {
                return;
            }

            IReadOnlyList<PXR_ImageTrackingReferenceHint> current = hints.Images;
            var next = new List<PXR_ImageTrackingReferenceHint>(current.Count);
            for (int index = 0; index < current.Count; index++)
            {
                PXR_ImageTrackingReferenceHint hint = current[index];
                next.Add(new PXR_ImageTrackingReferenceHint(
                    hint.ImageName,
                    hint.ImageGuid,
                    optimizeForStaticImage));
            }

            Undo.RecordObject(hints, optimizeForStaticImage ? "Set All PICO Images Static" : "Set All PICO Images Dynamic");
            SetHintsForEditor(hints, next);
            EditorUtility.SetDirty(hints);
        }

        private static void SetReferenceLibraryForEditor(
            PXR_ImageTrackingLibraryHints hints,
            XRReferenceImageLibrary referenceLibrary)
        {
            var serializedObject = new SerializedObject(hints);
            SerializedProperty referenceLibraryProperty = serializedObject.FindProperty("m_ReferenceLibrary");
            referenceLibraryProperty.objectReferenceValue = referenceLibrary;
            serializedObject.ApplyModifiedProperties();
        }

        private static void SetHintsForEditor(
            PXR_ImageTrackingLibraryHints hints,
            IReadOnlyList<PXR_ImageTrackingReferenceHint> imageHints)
        {
            var serializedObject = new SerializedObject(hints);
            SerializedProperty imagesProperty = serializedObject.FindProperty("m_Images");
            imagesProperty.arraySize = imageHints == null ? 0 : imageHints.Count;

            for (int index = 0; index < imagesProperty.arraySize; index++)
            {
                PXR_ImageTrackingReferenceHint imageHint = imageHints[index];
                SerializedProperty elementProperty = imagesProperty.GetArrayElementAtIndex(index);
                elementProperty.FindPropertyRelative("m_ImageName").stringValue = imageHint.ImageName;
                elementProperty.FindPropertyRelative("m_ImageGuid").stringValue = imageHint.ImageGuid.ToString("D");
                elementProperty.FindPropertyRelative("m_OptimizeForStaticImage").boolValue = imageHint.OptimizeForStaticImage;
            }

            serializedObject.ApplyModifiedProperties();
        }
        internal static PXR_ImageTrackingLibraryHints CreateHintsAsset(
            XRReferenceImageLibrary library,
            string path)
        {
            var hints = ScriptableObject.CreateInstance<PXR_ImageTrackingLibraryHints>();
            SetReferenceLibraryForEditor(hints, library);
            SyncFromReferenceLibrary(hints, removeMissing: true);
            AssetDatabase.CreateAsset(hints, path);
            AssetDatabase.SaveAssets();
            return hints;
        }
    }

    [CustomEditor(typeof(PXR_ImageTrackingLibraryHints))]
    internal sealed class PXRImageTrackingLibraryHintsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var hints = (PXR_ImageTrackingLibraryHints)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync From Reference Library"))
                {
                    PXRImageTrackingLibraryHintsEditorUtility.SyncFromReferenceLibrary(
                        hints,
                        removeMissing: true);
                    AssetDatabase.SaveAssets();
                }

                if (GUILayout.Button("Set All Static"))
                {
                    PXRImageTrackingLibraryHintsEditorUtility.SetAllStatic(hints, true);
                    AssetDatabase.SaveAssets();
                }

                if (GUILayout.Button("Set All Dynamic"))
                {
                    PXRImageTrackingLibraryHintsEditorUtility.SetAllStatic(hints, false);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        [MenuItem("Assets/PICO/Create Image Tracking Library Hints", true)]
        private static bool CanCreateHintsFromSelection()
        {
            return Selection.activeObject is XRReferenceImageLibrary;
        }

        [MenuItem("Assets/PICO/Create Image Tracking Library Hints")]
        private static void CreateHintsFromSelection()
        {
            var library = (XRReferenceImageLibrary)Selection.activeObject;
            string libraryPath = AssetDatabase.GetAssetPath(library);
            string directory = Path.GetDirectoryName(libraryPath);
            string fileName = Path.GetFileNameWithoutExtension(libraryPath) + "_PicoHints.asset";
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(directory ?? "Assets", fileName).Replace('\\', '/'));

            PXR_ImageTrackingLibraryHints hints =
                PXRImageTrackingLibraryHintsEditorUtility.CreateHintsAsset(library, path);
            Selection.activeObject = hints;
            EditorGUIUtility.PingObject(hints);
        }
    }
}
#endif

