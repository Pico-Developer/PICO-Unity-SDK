using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class TangentNodeConverter : NodeConverterBase<TangentVectorNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            TangentVectorNode node = (TangentVectorNode)shaderGraphNode;

            string space = GeomHelpers.GetStringSpace(node.space);
            var nodeData = MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Tangent, shaderGraphNode, graph, stagingEdges, "Tangent");
            nodeData.AddPortWithStringValue("space",  MaterialXDataType.String, space);
        }
    }
}