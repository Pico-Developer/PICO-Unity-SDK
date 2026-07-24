using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class LengthNodeConverter : NodeConverterBase<LengthNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            MaterialXGraphUtil.AddUnaryOperatorNode(MaterialXNodeType.Magnitude, shaderGraphNode, graph, stagingEdges);
        }
    }
}
