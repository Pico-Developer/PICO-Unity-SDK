using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class SquareRootNodeConverter : NodeConverterBase<SquareRootNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            MaterialXGraphUtil.AddUnaryOperatorNode(MaterialXNodeType.SquareRoot, shaderGraphNode, graph, stagingEdges);
        }
    }
}
