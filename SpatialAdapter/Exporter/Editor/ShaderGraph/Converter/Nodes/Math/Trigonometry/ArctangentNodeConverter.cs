using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ArctangentNodeConverter : NodeConverterBase<ArctangentNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var nodeData = MaterialXGraphUtil.AddUnaryOperatorNode(
                MaterialXNodeType.Arctangent2, shaderGraphNode, graph, stagingEdges, "iny");
            nodeData.AddPortWithValue("inx", MaterialXDataType.Float, 1f);
        }
    }
}
