using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class NormalizeNodeConverter : NodeConverterBase<NormalizeNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            MaterialXGraphUtil.AddUnaryOperatorNode(MaterialXNodeType.Normalize, shaderGraphNode, graph, stagingEdges);
        }
    }
}
