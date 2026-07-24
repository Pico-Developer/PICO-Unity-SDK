using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class UVNodeConverter : NodeConverterBase<UVNode>
    {
        public override void Convert(AbstractMaterialNode node, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var uvNode = MaterialXGraphUtil.AddUVNode(
                graph, ShaderGraphUtil.NodeUtil.GetNodeName(node, "UV"), (int)(node as UVNode).uvChannel);

            var multiplyNode = graph.AddNode(ShaderGraphUtil.NodeUtil.GetNodeName(node, "Multiply"),
                MaterialXNodeType.Multiply, MaterialXDataType.Vector2);
            multiplyNode.AddPortWithValue("in1", MaterialXDataType.Vector2, new[] {1.0f, -1.0f});
            graph.AddPortAndEdge(uvNode.Name, multiplyNode.Name, "in2", MaterialXDataType.Vector2);

            var nodeData = MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Add, node, graph, stagingEdges, "Add");

            // MaterialX only supports 2-channel UVs?
            nodeData.DataType = MaterialXDataType.Vector2;

            nodeData.AddPortWithValue("in1", MaterialXDataType.Vector2, new[] {0.0f, 1.0f});
            graph.AddPortAndEdge(multiplyNode.Name, nodeData.Name, "in2", MaterialXDataType.Vector2);
        }
    }
}