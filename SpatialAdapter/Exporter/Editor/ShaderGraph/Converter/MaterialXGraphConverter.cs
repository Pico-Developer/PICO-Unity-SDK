using System;
using System.IO;
using System.Linq;
using UnityEditor.ShaderGraph;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal static class MaterialXGraphConverter
    {
        // Node descriptions that do not throw errors despite
        // being invalid conversions 
        internal static string[] VALID_NODE_DEFINITION = new[]
        {
            "VertexDescription",
            "SurfaceDescription"
        };
        
        internal static MaterialXGraphData Convert(GraphData shaderGraphData, string shaderGraphAssetPath)
        {
            StagingEdges stagingEdges = new();
            string shaderGraphName = Path.GetFileNameWithoutExtension(shaderGraphAssetPath);
            MaterialXGraphData graphData = new(shaderGraphName, shaderGraphData.path, shaderGraphAssetPath);
            
            foreach (var node in shaderGraphData.GetNodes<AbstractMaterialNode>())
            {
                if (ConverterLookup.NodeCanConvert(node))
                {
                    ConverterLookup.ConvertNode(node, graphData, stagingEdges);
                }
                else
                {
                    string[] nodeDefinition = node.name.Split(".");
                    bool hasValidDefinition = false;
                    foreach (string definition in nodeDefinition)
                    {
                        if (VALID_NODE_DEFINITION.Contains(definition))
                        {
                            hasValidDefinition = true;
                            break;
                        }
                    }

                    if (!hasValidDefinition)
                    {
                        Debug.LogError("No valid conversion for " + node.name);
                        return null;
                    }
                }
            }

            ISurfaceConverter surfaceConverter = new USDPreviewSurfaceConverter();
            surfaceConverter.Convert(shaderGraphData, graphData, stagingEdges);
            
            stagingEdges.ResolvePortConnections(graphData);
            return graphData;
        }
    }
}