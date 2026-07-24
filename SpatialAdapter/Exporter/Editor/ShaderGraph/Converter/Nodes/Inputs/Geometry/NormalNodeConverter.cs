using System;
using NUnit.Framework;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class NormalNodeConverter : NodeConverterBase<NormalVectorNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            NormalVectorNode normalNode = (NormalVectorNode)shaderGraphNode;

            string space = GeomHelpers.GetStringSpace(normalNode.space);
            var nodeData = MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.GeomNormal, shaderGraphNode, graph, stagingEdges, "Normal");
            nodeData.AddPortWithStringValue("space",  MaterialXDataType.String, space);
        }
    }
}