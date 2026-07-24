using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class BitangentVectorNodeConverter : NodeConverterBase<BitangentVectorNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            BitangentVectorNode node = (BitangentVectorNode)shaderGraphNode;

            string space = GeomHelpers.GetStringSpace(node.space);
            var nodeData = MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.GeomBitangent, shaderGraphNode, graph, stagingEdges, "Bitangent");
            nodeData.AddPortWithStringValue("space",  MaterialXDataType.String, space);
        }
    }
}