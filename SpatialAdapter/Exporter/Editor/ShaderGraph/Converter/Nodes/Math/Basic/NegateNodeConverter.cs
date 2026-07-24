using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class NegateNodeConverter : NodeConverterBase<NegateNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var nodeData = MaterialXGraphUtil.AddUnaryOperatorNode(MaterialXNodeType.Multiply, shaderGraphNode, graph,
                stagingEdges, "in2");
            nodeData.AddPortWithValue("in1", nodeData.DataType, new float[] { -1f, -1f, -1f, -1f });
        }
    }
}
