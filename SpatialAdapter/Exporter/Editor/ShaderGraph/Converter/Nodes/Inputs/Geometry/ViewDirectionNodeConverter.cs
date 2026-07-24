using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ViewDirectionNodeConverter : NodeConverterBase<ViewDirectionNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            ViewDirectionNode node = (ViewDirectionNode)shaderGraphNode;

            string space = GeomHelpers.GetStringSpace(node.space);
            var nodeData = MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.GeomViewDirection, shaderGraphNode, graph, stagingEdges, "ViewDirection");
            nodeData.AddPortWithStringValue("space",  MaterialXDataType.String, space);
        }
    }
}