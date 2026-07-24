using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ArccosineNodeConverter : NodeConverterBase<ArccosineNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            MaterialXGraphUtil.AddUnaryOperatorNode(MaterialXNodeType.Arccosine, shaderGraphNode, graph, stagingEdges);
        }
    }
}
