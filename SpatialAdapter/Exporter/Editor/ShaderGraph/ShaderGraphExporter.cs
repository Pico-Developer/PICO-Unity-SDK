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
            GraphData shaderGraphData = ShaderGraphDeserializer.LoadGraphData(assetPath);
            MaterialXGraphData materialXGraphData = MaterialXGraphConverter.Convert(
                shaderGraphData,
                assetPath,
                shaderGraphNameOverride);

            if (materialXGraphData == null)
            {
                Debug.LogError("Unable to export " + Path.GetFileName(assetPath));
                return;
            }

            ExportTextures(materialXGraphData, assetExportRoot);

            string outputString = MaterialXGraphSerializer.Serialize(materialXGraphData, inputMaterial);
            string outFileRoot = Path.GetDirectoryName(exportedFilePath);
            string outFilePath = Path.Combine(outFileRoot, materialXGraphData.Name + ".usda");
            
            FileInfo file = new(outFilePath);
            file.Directory.Create();
            File.WriteAllText(file.FullName, outputString);
            
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
    }
}
