using UnityEditor.ShaderGraph;
using UnityEngine;
using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;

namespace ByteDance.PICO.MultiSpatial.Exporter.Editor.ShaderGraph
{
    internal class LerpNodeConvert : NodeConverterBase<LerpNode>
    {
        private static float[] CreateConstant(int channelCount, float value)
        {
            var values = new float[channelCount];
            for (int i = 0; i < values.Length; ++i)
            {
                values[i] = value;
            }

            return values;
        }

        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph,
            StagingEdges stagingEdges)
        {
            var outputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(shaderGraphNode);
            var outputType = TypeUtil.GetMaterialXDataType(outputSlot);

            var aSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "A");
            var bSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "B");
            var tSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "T");

            var aProxyNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "LerpAProxy"),
                MaterialXNodeType.Add,
                outputType);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, aProxyNode, aSlot, "in1", outputType);
            aProxyNode.AddPortWithValue("in2", outputType, CreateConstant(outputType.ChannelCount(), 0.0f));

            var deltaNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "LerpDelta"),
                MaterialXNodeType.Subtract,
                outputType);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, deltaNode, bSlot, "in1", outputType);
            graph.AddPortAndEdge(aProxyNode.Name, deltaNode.Name, "in2", outputType);

            var scaleNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "LerpScale"),
                MaterialXNodeType.Multiply,
                outputType);
            graph.AddPortAndEdge(deltaNode.Name, scaleNode.Name, "in1", outputType);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, scaleNode, tSlot, "in2", MaterialXDataType.Float);

            var addNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "LerpResult"),
                MaterialXNodeType.Add,
                outputType);
            graph.AddPortAndEdge(aProxyNode.Name, addNode.Name, "in1", outputType);
            graph.AddPortAndEdge(scaleNode.Name, addNode.Name, "in2", outputType);

            stagingEdges.AddPort(outputSlot.slotReference, addNode.Name);
        }
    }
}
