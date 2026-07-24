using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph.UV
{
    internal class TilingAndOffsetNodeConverter : NodeConverterBase<TilingAndOffsetNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph,
            StagingEdges stagingEdges)
        {
            // Out = UV * Tiling + Offset;
            var uvSlot = (UVMaterialSlot)ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "UV");
            var tilingSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Tiling");
            var offsetSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Offset");
            var outputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(shaderGraphNode);

            // tiling operation
            var tilingNode = graph.AddNode(ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "TilingAndOffsetMul"),
                MaterialXNodeType.Multiply, MaterialXDataType.Vector2);

            // uv input
            MaterialXGraphUtil.HandleUVSlot(uvSlot,
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "TilingAndOffsetUV"), tilingNode.Name, "in1",
                graph,
                stagingEdges);

            // tiling input
            tilingNode.AddPortWithValue("in2", MaterialXDataType.Vector2,
                ShaderGraphUtil.SlotUtil.GetSlotDefaultValue(tilingSlot));
            stagingEdges.AddShaderGraphEdgeAndPort(tilingSlot, tilingNode.Name, "in2");

            // offset operation
            var offsetNode = graph.AddNode(ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "TilingAndOffsetAdd"), MaterialXNodeType.Add,
                MaterialXDataType.Vector2);
            graph.AddPortAndEdge(tilingNode.Name, offsetNode.Name, "in1", MaterialXDataType.Vector2);

            // offset input
            offsetNode.AddPortWithValue("in2", MaterialXDataType.Vector2, ShaderGraphUtil.SlotUtil.GetSlotDefaultValue(offsetSlot));
            stagingEdges.AddShaderGraphEdgeAndPort(offsetSlot, offsetNode.Name, "in2");

            stagingEdges.AddPort(outputSlot.slotReference, offsetNode.Name);
        }
    }
}