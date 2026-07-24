using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class IntNodeConverter : NodeConverterBase<IntegerNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var graphNode = MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Constant, shaderGraphNode, graph, stagingEdges,
                "Integer");
            int value = ((IntegerNode)shaderGraphNode).value;
            graphNode.AddPortWithValue("value", MaterialXDataType.Float, (float)value);
        }
    }
}