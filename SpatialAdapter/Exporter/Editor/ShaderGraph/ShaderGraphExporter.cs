using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.ShaderGraph;
using UnityEngine;
using ByteDance.PICO.SpatialAdapter.Exporter.Editor;
using UnityEngine.Rendering;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    public static class ShaderGraphExporter
    {
        public static readonly string TexturePathPrefix = "/textures/";

        public static void ExportShaderGraph(
            string assetPath,
            string exportedFilePath,
            string assetExportRoot,
            Material inputMaterial = null,
            string shaderGraphNameOverride = null)
        {
            if (!ExportShaderGraph(
                    assetPath,
                    exportedFilePath,
                    assetExportRoot,
                    out string failureMessage,
                    inputMaterial,
                    shaderGraphNameOverride))
            {
                Debug.LogWarning(failureMessage);
            }
        }

        public static bool ExportShaderGraph(
            string assetPath,
            string exportedFilePath,
            string assetExportRoot,
            out string failureMessage,
            Material inputMaterial = null,
            string shaderGraphNameOverride = null)
        {
            GraphData shaderGraphData = ShaderGraphDeserializer.LoadGraphData(assetPath);
            var unsupportedNodes = MaterialXGraphConverter.GetUnsupportedNodes(shaderGraphData);
            if (unsupportedNodes.Count > 0)
            {
                failureMessage = FormatUnsupportedNodeMessage(assetPath, unsupportedNodes);
                return false;
            }

            MaterialXGraphData materialXGraphData = MaterialXGraphConverter.Convert(
                shaderGraphData,
                assetPath,
                shaderGraphNameOverride);

            if (materialXGraphData == null)
            {
                failureMessage =
                    $"SpatialAdapter ShaderGraph Exporter: Unable to export ShaderGraph '{Path.GetFileNameWithoutExtension(assetPath)}' from '{assetPath}'.";
                return false;
            }

            string outputString = MaterialXGraphSerializer.Serialize(materialXGraphData, inputMaterial);
            ExportTextures(materialXGraphData, assetExportRoot);
            FileInfo file = new(exportedFilePath);
            file.Directory.Create();
            File.WriteAllText(file.FullName, outputString);

            failureMessage = null;
            return true;
        }

        private static void ExportTextures(MaterialXGraphData graph, string assetExportRoot)
        {
            foreach (Texture tex in graph.InputTextures)
            {
                try
                {
                    string texPath = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(texPath))
                    {
                        Debug.LogWarningFormat("Failed to resolve asset path for texture {0}.",
                            tex != null ? tex.name : "null");
                        continue;
                    }

                    string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                    string sourcePath = Path.IsPathRooted(texPath)
                        ? texPath
                        : Path.Combine(projectRoot ?? string.Empty, texPath);
                    string outPath = Path.Combine(assetExportRoot, texPath);
                    string outDirectory = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(outDirectory))
                    {
                        Directory.CreateDirectory(outDirectory);
                    }

                    File.Copy(sourcePath, outPath, true);
                }
                catch (Exception e)
                {
                    Debug.LogErrorFormat(
                        "Failed to copy texture {0}! Exported ShaderGraph may not work as expected. ({1})", tex.name,
                        e);
                }
            }
        }

        private static string FormatUnsupportedNodeMessage(string assetPath, IReadOnlyCollection<string> unsupportedNodes)
        {
            string formattedNodeList = string.Join(", ", unsupportedNodes
                .GroupBy(nodeName => nodeName)
                .Select(group => group.Count() > 1 ? $"{group.Key} x{group.Count()}" : group.Key));

            return
                $"SpatialAdapter ShaderGraph Exporter: ShaderGraph '{Path.GetFileNameWithoutExtension(assetPath)}' from '{assetPath}' contains unsupported nodes and cannot be exported. Unsupported nodes: {formattedNodeList}.";
        }
    }
}
