using System.Collections.Generic;
using System.IO;
using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;
using NUnit.Framework;

public class ShaderGraphConvertabilityTest
{
    private const string PackageBasePath = "Packages/com.bytedance.pico.spatialadapter/Exporter/Editor";
    private const string ShaderGraphTestDataPath = PackageBasePath + "/ShaderGraph/Tests/Data/ShaderGraph/SourceAssets";
    private const string OutputUsdaPath = PackageBasePath + "/ShaderGraph/Tests/Data/ShaderGraph/Output";
    private List<string> shaderGraphPaths;

    private static void CollectShaderGraphPaths(string path, List<string> shaderGraphPaths)
    {
        string[] files = Directory.GetFiles(path);
        foreach (string file in files)
        {
            if (file.EndsWith(".shadergraph"))
            {
                shaderGraphPaths.Add(file);
            }
        }

        string[] directories = Directory.GetDirectories(path);
        foreach (string directory in directories)
        {
            CollectShaderGraphPaths(directory, shaderGraphPaths);
        }
    }

    [OneTimeSetUp]
    public void Setup()
    {
        shaderGraphPaths = new List<string>();
        CollectShaderGraphPaths(ShaderGraphTestDataPath, shaderGraphPaths);
    }

    [Test]
    public void ShaderGraphConvertabilityTestSimplePasses()
    {
        foreach (var shaderGraphPath in shaderGraphPaths)
        {
            SpatialAdapterShaderGraphExporter.ExportShaderGraph(shaderGraphPath, OutputUsdaPath);
        }
    }
}
