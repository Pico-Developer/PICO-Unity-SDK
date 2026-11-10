using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class SimpleNoiseNodeConverter : NodeConverterBase<NoiseNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var uvSlot = (UVMaterialSlot)ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "UV");
            var scaleSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Scale");
            var outputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(shaderGraphNode);

            var scaledUvNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "SimpleNoiseScale"),
                MaterialXNodeType.Multiply,
                MaterialXDataType.Vector2);
            MaterialXGraphUtil.HandleUVSlot(
                uvSlot,
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "SimpleNoiseUV"),
                scaledUvNode.Name,
                "in1",
                graph,
                stagingEdges);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, scaledUvNode, scaleSlot, "in2", MaterialXDataType.Float);

            var noiseNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "SimpleNoise"),
                MaterialXNodeType.Noise2D,
                MaterialXDataType.Float);
            noiseNode.AddPortWithValue("amplitude", MaterialXDataType.Float, 1.0f);
            noiseNode.AddPortWithValue("pivot", MaterialXDataType.Float, 0.0f);
            graph.AddPortAndEdge(scaledUvNode.Name, noiseNode.Name, "texcoord", MaterialXDataType.Vector2);

            stagingEdges.AddPort(outputSlot.slotReference, noiseNode.Name);
        }
    }
}
