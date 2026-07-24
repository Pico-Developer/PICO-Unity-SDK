using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class AddNodeConverter : NodeConverterBase<AddNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            MaterialXGraphUtil.AddBinaryOperatorNode(MaterialXNodeType.Add, shaderGraphNode, graph, stagingEdges);
        }
    }
}