using System.Collections.Generic;
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
        
        internal static MaterialXGraphData Convert(GraphData shaderGraphData, string shaderGraphAssetPath, string shaderGraphNameOverride = null)
        {
            StagingEdges stagingEdges = new();
            string shaderGraphName = string.IsNullOrEmpty(shaderGraphNameOverride)
                ? Path.GetFileNameWithoutExtension(shaderGraphAssetPath)
                : shaderGraphNameOverride;
            MaterialXGraphData graphData = new(shaderGraphName, shaderGraphData.path, shaderGraphAssetPath);
            
            foreach (var node in shaderGraphData.GetNodes<AbstractMaterialNode>())
            {
                if (ConverterLookup.NodeCanConvert(node))
                {
                    ConverterLookup.ConvertNode(node, graphData, stagingEdges);
                }
                else
                {
                    if (!HasValidDefinition(node))
                    {
                        return null;
                    }
                }
            }

            ISurfaceConverter surfaceConverter = new USDPreviewSurfaceConverter();
            surfaceConverter.Convert(shaderGraphData, graphData, stagingEdges);
            
            stagingEdges.ResolvePortConnections(graphData);
            return graphData;
        }

        internal static IReadOnlyList<string> GetUnsupportedNodes(GraphData shaderGraphData)
        {
            List<string> unsupportedNodes = new();

            foreach (var node in shaderGraphData.GetNodes<AbstractMaterialNode>())
            {
                if (!ConverterLookup.NodeCanConvert(node) && !HasValidDefinition(node))
                {
                    unsupportedNodes.Add(node.name);
                }
            }

            return unsupportedNodes;
        }

        private static bool HasValidDefinition(AbstractMaterialNode node)
        {
            string[] nodeDefinition = node.name.Split(".");
            foreach (string definition in nodeDefinition)
            {
                if (VALID_NODE_DEFINITION.Contains(definition))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
