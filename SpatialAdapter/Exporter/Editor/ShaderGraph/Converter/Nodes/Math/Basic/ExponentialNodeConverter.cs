using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ExponentialNodeConverter : NodeConverterBase<ExponentialNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            MaterialXGraphUtil.AddUnaryOperatorNode(MaterialXNodeType.Exponential, shaderGraphNode, graph, stagingEdges);
        }
    }
}
