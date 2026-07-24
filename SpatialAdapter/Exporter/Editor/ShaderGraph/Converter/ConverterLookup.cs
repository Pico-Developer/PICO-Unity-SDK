using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    public static class ConverterLookup
    {
        private static Dictionary<Type, INodeConverter> _converterLookUp;

        private static void Setup()
        {
            _converterLookUp = new();
            foreach (var type in TypeCache.GetTypesDerivedFrom(typeof(INodeConverter)).Where(e => !e.IsGenericType))
            {
                var nodeConverter = (INodeConverter)Activator.CreateInstance(type);
                if (typeof(AbstractMaterialNode).IsAssignableFrom(nodeConverter.GetShaderGraphNodeType()))
                {
                    _converterLookUp.Add(nodeConverter.GetShaderGraphNodeType(), nodeConverter);
                }
                else
                {
                    throw new Exception($"{type}.GetSupportedNodeType() must be a type inherited from AbstractMaterialNode.");
                }
            }
        }

        private static Dictionary<Type, INodeConverter> ConverterLookUp
        {
            get
            {
                if (_converterLookUp == null)
                {
                    Setup();
                }

                return _converterLookUp;
            }
        }

        internal static bool NodeCanConvert(AbstractMaterialNode shaderGraphNode)
        {
            Type nodeType = shaderGraphNode.GetType();
            return ConverterLookUp.ContainsKey(nodeType) &&
                   ConverterLookUp[nodeType].NodeCanConvert(shaderGraphNode);
        }

        internal static void ConvertNode(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graphData,
            StagingEdges stagingEdges)
        {
            if (NodeCanConvert(shaderGraphNode))
            {
                ConverterLookUp[shaderGraphNode.GetType()].Convert(shaderGraphNode, graphData, stagingEdges);
            }
        }

    }
}