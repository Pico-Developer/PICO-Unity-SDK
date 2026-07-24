using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class OneMinusNodeConverter : NodeConverterBase<OneMinusNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var nodeData = MaterialXGraphUtil.AddUnaryOperatorNode(MaterialXNodeType.Subtract, shaderGraphNode, graph,
                stagingEdges, "in2");
            nodeData.AddPortWithValue("in1", nodeData.DataType, new float[] { 1f, 1f, 1f, 1f });
        }
    }
}