using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using NUnit.Framework;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class PositionNodeConverter : NodeConverterBase<PositionNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            PositionNode positionNode = (PositionNode)shaderGraphNode;
            string space = GeomHelpers.GetStringSpace(positionNode.space);
            var outputSlots = new List<MaterialSlot>();
            shaderGraphNode.GetOutputSlots(outputSlots);
            var outputSlot = outputSlots[0];

            string positionNodeName = ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "Position");
            var positionData = graph.AddNode(positionNodeName, MaterialXNodeType.GeomPosition, MaterialXDataType.Vector3);
            positionData.AddPortWithStringValue("space", MaterialXDataType.String, space);

            if (positionNode.space == CoordinateSpace.World)
            {
                var flipNode = graph.AddNode(
                    ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "PositionFlipZ"),
                    MaterialXNodeType.Multiply,
                    MaterialXDataType.Vector3);
                graph.AddPortAndEdge(positionData.Name, flipNode.Name, "in1", MaterialXDataType.Vector3);
                flipNode.AddPortWithValue("in2", MaterialXDataType.Vector3, new[] { 1.0f, 1.0f, -1.0f });
                stagingEdges.AddPort(outputSlot.slotReference, flipNode.Name);
                return;
            }

            stagingEdges.AddPort(outputSlot.slotReference, positionData.Name);
        }
    }
}
