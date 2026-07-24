using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class VertexColorNodeConverter : NodeConverterBase<VertexColorNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.GeomColor, shaderGraphNode, graph, stagingEdges, "GeomColorVertexColor");
        }
    }
}
