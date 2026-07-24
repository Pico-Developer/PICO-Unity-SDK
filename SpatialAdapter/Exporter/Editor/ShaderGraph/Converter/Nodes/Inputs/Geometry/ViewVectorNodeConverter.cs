using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ViewVectorNodeConverter : NodeConverterBase<ViewVectorNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            ViewVectorNode node = (ViewVectorNode)shaderGraphNode;

            string space = GeomHelpers.GetStringSpace(node.space);
            var nodeData = MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.GeomViewDirection, shaderGraphNode, graph, stagingEdges, "ViewVector");
            nodeData.AddPortWithStringValue("space", MaterialXDataType.String, space);
        }
    }
}
