using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ModuloNodeConverter : NodeConverterBase<ModuloNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            MaterialXGraphUtil.AddBinaryOperatorNode(MaterialXNodeType.Modulo, shaderGraphNode, graph, stagingEdges);
        }
    }
}
