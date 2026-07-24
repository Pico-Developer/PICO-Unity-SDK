using System.Collections.Generic;
using UnityEditor.ShaderGraph;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class TimeNodeConverter : NodeConverterBase<TimeNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var outputSlots = new List<MaterialSlot>();
            shaderGraphNode.GetOutputSlots(outputSlots);

            MaterialXNodeData timeNode = null;

            MaterialXNodeData EnsureTimeNode()
            {
                if (timeNode != null)
                {
                    return timeNode;
                }

                var timeNodeName = ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "Time");
                timeNode = graph.AddNode(timeNodeName, MaterialXNodeType.Time, MaterialXDataType.Float);
                return timeNode;
            }

            foreach (var outputSlot in outputSlots)
            {
                if (!outputSlot.isConnected)
                {
                    continue;
                }

                switch (outputSlot.RawDisplayName())
                {
                    case "Time":
                        stagingEdges.AddPort(outputSlot.slotReference, EnsureTimeNode().Name);
                        break;
                    case "Sine Time":
                    {
                        var sineNodeName = ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "SineTime");
                        var sineNode = graph.AddNode(sineNodeName, MaterialXNodeType.Sine, MaterialXDataType.Float);
                        graph.AddPortAndEdge(EnsureTimeNode().Name, sineNode.Name, "in", MaterialXDataType.Float);
                        stagingEdges.AddPort(outputSlot.slotReference, sineNode.Name);
                        break;
                    }
                    case "Cosine Time":
                    {
                        var cosineNodeName = ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "CosineTime");
                        var cosineNode = graph.AddNode(cosineNodeName, MaterialXNodeType.Cosine, MaterialXDataType.Float);
                        graph.AddPortAndEdge(EnsureTimeNode().Name, cosineNode.Name, "in", MaterialXDataType.Float);
                        stagingEdges.AddPort(outputSlot.slotReference, cosineNode.Name);
                        break;
                    }
                    case "Delta Time":
                    case "Smooth Delta":
                    {
                        Debug.LogWarning($"{outputSlot.RawDisplayName()} output on Time node is not currently supported. Using temporary constant of 0.1.");
                        var defaultNodeName = ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, outputSlot.RawDisplayName());
                        var defaultNode = graph.AddNode(defaultNodeName, MaterialXNodeType.Constant, MaterialXDataType.Float);
                        defaultNode.AddPortWithValue("value", MaterialXDataType.Float, 0.1f);
                        stagingEdges.AddPort(outputSlot.slotReference, defaultNode.Name);
                        break;
                    }
                }
            }
        }
    }
}
