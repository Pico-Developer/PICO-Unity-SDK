#if UNITY_EDITOR && !ENABLE_PICO_OPENXR_SDK
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using ByteDance.PICO.SecureMR;
using UnityEditor;
using UnityEngine;

namespace ByteDance.PICO.SecureMR.Editor
{
    public sealed class SpatialMLPipelineZooImporterWindow : EditorWindow
    {
        private const string DefaultUrl = "https://huggingface.co/picoxr/face-mediapipe-pipeline/resolve/main/face-mediapipe-pipeline.zip?download=true";
        private string packageUrl = DefaultUrl;
        private string destinationRoot = "Assets/SpatialMLPipelineZoo";

        [MenuItem("PICO/SpatialML/Import SpatialML Pipeline Zoo Package")]
        public static void Open()
        {
            GetWindow<SpatialMLPipelineZooImporterWindow>("SpatialML Pipeline Zoo");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Import a SpatialML pipeline zoo package", EditorStyles.boldLabel);
            packageUrl = EditorGUILayout.TextField("HuggingFace zip URL", packageUrl);
            destinationRoot = EditorGUILayout.TextField("Destination Root", destinationRoot);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Download and Import"))
                {
                    var asset = SpatialMLPipelineZooImporter.ImportFromUrl(packageUrl, destinationRoot);
                    Selection.activeObject = asset;
                }

                if (GUILayout.Button("Import Local Zip/Folder"))
                {
                    var choice = EditorUtility.DisplayDialogComplex(
                        "Import SpatialML Package",
                        "Choose a local zip file or a folder containing manifest.json.",
                        "Zip File",
                        "Cancel",
                        "Folder");

                    SpatialMLPipelineZooAsset asset = null;
                    if (choice == 0)
                    {
                        var zipPath = EditorUtility.OpenFilePanel("SpatialML pipeline package zip", string.Empty, "zip");
                        if (!string.IsNullOrEmpty(zipPath))
                        {
                            asset = SpatialMLPipelineZooImporter.ImportFromZip(zipPath, destinationRoot);
                        }
                    }
                    else if (choice == 2)
                    {
                        var folderPath = EditorUtility.OpenFolderPanel("SpatialML pipeline package folder", string.Empty, string.Empty);
                        if (!string.IsNullOrEmpty(folderPath))
                        {
                            asset = SpatialMLPipelineZooImporter.ImportFromFolder(folderPath, destinationRoot);
                        }
                    }

                    if (asset != null)
                    {
                        Selection.activeObject = asset;
                    }
                }
            }
        }
    }

    public static class SpatialMLPipelineZooImporter
    {
        public static SpatialMLPipelineZooAsset ImportFromUrl(string url, string destinationRoot)
        {
            var tempZip = Path.Combine(Path.GetTempPath(), $"spatialml_pipeline_{Guid.NewGuid():N}.zip");
            try
            {
                using (var client = new WebClient()) client.DownloadFile(url, tempZip);
                return ImportFromZip(tempZip, destinationRoot);
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }
        }

        public static SpatialMLPipelineZooAsset ImportFromZip(string zipPath, string destinationRoot)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"spatialml_pipeline_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                ZipFile.ExtractToDirectory(zipPath, tempDir);
                var packageRoot = FindPackageRoot(tempDir);
                return ImportFromFolder(packageRoot, destinationRoot);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        public static SpatialMLPipelineZooAsset ImportFromFolder(string packageFolder, string destinationRoot)
        {
            if (!File.Exists(Path.Combine(packageFolder, "manifest.json")))
            {
                throw new FileNotFoundException("SpatialML package folder must contain manifest.json", packageFolder);
            }

            var manifestPath = Path.Combine(packageFolder, "manifest.json");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            var rawPackageName = string.IsNullOrEmpty(manifest.id) ? new DirectoryInfo(packageFolder).Name : manifest.id;
            var packageName = SanitizePackageName(rawPackageName);
            var assetFolder = CombineAssetPath(destinationRoot, packageName);
            var absoluteAssetFolder = Path.GetFullPath(ToAbsolutePath(assetFolder));
            EnsurePathUnderAssets(absoluteAssetFolder, "Resolved import destination is outside the Unity project's Assets folder.");

            if (Directory.Exists(absoluteAssetFolder))
            {
                if (!EditorUtility.DisplayDialog(
                        "SpatialML Import",
                        $"Folder already exists and will be replaced:\n{assetFolder}\n\nContinue?",
                        "Replace",
                        "Cancel"))
                {
                    throw new OperationCanceledException("Import cancelled by user.");
                }

                if (!AssetDatabase.DeleteAsset(assetFolder) && Directory.Exists(absoluteAssetFolder))
                {
                    Directory.Delete(absoluteAssetFolder, true);
                }
            }

            Directory.CreateDirectory(absoluteAssetFolder);

            var copied = new Dictionary<string, string>();
            foreach (var file in Directory.GetFiles(packageFolder, "*", SearchOption.AllDirectories))
            {
                if (ShouldSkip(file)) continue;
                var relative = NormalizePath(Path.GetRelativePath(packageFolder, file));
                if (!IsSafeRelativePath(relative)) throw new InvalidDataException($"Invalid package file path '{relative}'.");
                var targetRelative = NeedsBytesExtension(relative) ? relative + ".bytes" : relative;
                var targetAssetPath = CombineAssetPath(assetFolder, targetRelative);
                var targetPath = Path.GetFullPath(ToAbsolutePath(targetAssetPath));
                EnsurePathUnderDirectory(targetPath, absoluteAssetFolder, $"Blocked path traversal attempt: '{relative}'.");
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(file, targetPath, true);
                copied[relative] = targetAssetPath;
            }

            AssetDatabase.Refresh();

            var packageAsset = ScriptableObject.CreateInstance<SpatialMLPipelineZooAsset>();
            packageAsset.packageId = manifest.id;
            packageAsset.manifestJson = AssetDatabase.LoadAssetAtPath<TextAsset>(copied["manifest.json"]);
            if (manifest.model != null && !string.IsNullOrEmpty(manifest.model.json_path) && copied.TryGetValue(manifest.model.json_path, out var modelJsonPath))
            {
                packageAsset.modelJson = AssetDatabase.LoadAssetAtPath<TextAsset>(modelJsonPath);
            }

            foreach (var pipeline in manifest.pipelines ?? Array.Empty<PipelineSpec>())
            {
                if (string.IsNullOrEmpty(pipeline.path) || !copied.TryGetValue(pipeline.path, out var jsonPath)) continue;
                packageAsset.pipelineJsonAssets.Add(new SpatialMLPipelineZooAsset.PipelineJsonAsset
                {
                    id = pipeline.id,
                    packagePath = pipeline.path,
                    json = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath)
                });
            }

            foreach (var pair in copied.Where(p => NeedsBytesExtension(p.Key)))
            {
                packageAsset.binaryAssets.Add(new SpatialMLPipelineZooAsset.BinaryAsset
                {
                    packagePath = pair.Key,
                    asset = AssetDatabase.LoadAssetAtPath<TextAsset>(pair.Value)
                });
            }

            var packageAssetPath = CombineAssetPath(assetFolder, packageName + ".asset");
            AssetDatabase.CreateAsset(packageAsset, packageAssetPath);
            EditorUtility.SetDirty(packageAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Imported SpatialML pipeline zoo package '{packageName}' to {assetFolder}");
            return packageAsset;
        }

        private static string FindPackageRoot(string extractedRoot)
        {
            if (File.Exists(Path.Combine(extractedRoot, "manifest.json"))) return extractedRoot;
            foreach (var dir in Directory.GetDirectories(extractedRoot))
            {
                if (Path.GetFileName(dir) == "__MACOSX") continue;
                if (File.Exists(Path.Combine(dir, "manifest.json"))) return dir;
            }
            throw new FileNotFoundException("No manifest.json found in extracted package.");
        }

        private static bool ShouldSkip(string file)
        {
            var normalized = NormalizePath(file);
            return normalized.Contains("/__MACOSX/") || Path.GetFileName(file).StartsWith("._") || Path.GetFileName(file) == ".DS_Store";
        }

        private static bool NeedsBytesExtension(string relativePath)
        {
            var ext = Path.GetExtension(relativePath).ToLowerInvariant();
            return ext != ".json" && ext != ".txt" && ext != ".js";
        }

        private static string SanitizePackageName(string rawPackageName)
        {
            var invalidCharacters = Path.GetInvalidFileNameChars();
            var packageName = new string(rawPackageName.Select(c =>
                c == '/' || c == '\\' || c == ':' || invalidCharacters.Contains(c) ? '_' : c).ToArray()).Trim();

            if (string.IsNullOrEmpty(packageName) || packageName == "." || packageName == ".." || packageName.Contains(".."))
            {
                throw new InvalidDataException($"Invalid package id '{rawPackageName}'.");
            }

            return packageName;
        }

        private static bool IsSafeRelativePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath)) return false;

            var segments = NormalizePath(relativePath).Split('/');
            return segments.All(segment => !string.IsNullOrEmpty(segment) && segment != "." && segment != "..");
        }

        private static void EnsurePathUnderAssets(string absolutePath, string message)
        {
            var assetsRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets"));
            EnsurePathUnderDirectory(absolutePath, assetsRoot, message);
        }

        private static void EnsurePathUnderDirectory(string absolutePath, string absoluteRoot, string message)
        {
            var fullPath = Path.GetFullPath(absolutePath);
            var fullRoot = Path.GetFullPath(absoluteRoot);
            var rootWithSeparator = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(message);
            }
        }

        private static string CombineAssetPath(string left, string right) => NormalizePath(left.TrimEnd('/') + "/" + right.TrimStart('/'));
        private static string NormalizePath(string path) => path.Replace('\\', '/');
        private static string ToAbsolutePath(string assetPath) => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));

        [Serializable]
        private sealed class Manifest
        {
            public string id;
            public ModelSpec model;
            public PipelineSpec[] pipelines;
        }

        [Serializable]
        private sealed class ModelSpec
        {
            public string bin_path;
            public string json_path;
            public string extra_json_path;
        }

        [Serializable]
        private sealed class PipelineSpec
        {
            public string id;
            public string path;
        }
    }
}
#endif
