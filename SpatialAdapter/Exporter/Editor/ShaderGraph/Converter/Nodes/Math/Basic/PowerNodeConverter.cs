using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class PowerNodeConverter : NodeConverterBase<PowerNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            MaterialXGraphUtil.AddBinaryOperatorNode(MaterialXNodeType.Power, shaderGraphNode, graph, stagingEdges);
        }
    }
}
