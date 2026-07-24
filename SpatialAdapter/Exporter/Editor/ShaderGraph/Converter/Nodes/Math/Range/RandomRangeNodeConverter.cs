using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class RandomRangeNodeConverter : NodeConverterBase<RandomRangeNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var portMap = new Dictionary<string, string>
            {
                { "Min", "bg" },
                { "Max", "fg" }
            };
            var outputNode = MaterialXGraphUtil.AddNaryOperatorNode(
                MaterialXNodeType.Mix, shaderGraphNode, graph, stagingEdges, "RandomRange", portMap);

            var seedSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Seed");
            var dotNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RandomRangeDot"),
                MaterialXNodeType.DotProduct,
                MaterialXDataType.Float);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, dotNode, seedSlot, "in1", MaterialXDataType.Vector2);
            dotNode.AddPortWithValue("in2", MaterialXDataType.Vector2, new float[] { 12.9898f, 78.233f });

            var sineNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RandomRangeSin"),
                MaterialXNodeType.Sine,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(dotNode.Name, sineNode.Name, "in", MaterialXDataType.Float);

            var multiplyNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RandomRangeMul"),
                MaterialXNodeType.Multiply,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(sineNode.Name, multiplyNode.Name, "in1", MaterialXDataType.Float);
            multiplyNode.AddPortWithValue("in2", MaterialXDataType.Float, 43758.5453f);

            var floorNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RandomRangeFloor"),
                MaterialXNodeType.Floor,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(multiplyNode.Name, floorNode.Name, "in", MaterialXDataType.Float);

            var fractionNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RandomRangeFrac"),
                MaterialXNodeType.Subtract,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(multiplyNode.Name, fractionNode.Name, "in1", MaterialXDataType.Float);
            graph.AddPortAndEdge(floorNode.Name, fractionNode.Name, "in2", MaterialXDataType.Float);

            graph.AddPortAndEdge(fractionNode.Name, outputNode.Name, "mix", MaterialXDataType.Float);
        }
    }
}
