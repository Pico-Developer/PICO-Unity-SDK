using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class BooleanNodeConverter : NodeConverterBase<BooleanNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var graphNode = MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Constant, shaderGraphNode, graph, stagingEdges,
                "Boolean");
            bool value = ((BooleanNode)shaderGraphNode).m_Value;
            graphNode.AddPortWithValue("value", MaterialXDataType.Boolean, value);
        }
    }
}