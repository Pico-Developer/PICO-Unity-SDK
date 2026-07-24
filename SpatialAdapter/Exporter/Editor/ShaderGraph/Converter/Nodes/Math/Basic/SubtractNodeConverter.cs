using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;
using UnityEditor.ShaderGraph;

namespace Exporter.Editor.ShaderGraph.Converter.Nodes.Math.Basic
{
    internal class SubtractNodeConverter : NodeConverterBase<SubtractNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            MaterialXGraphUtil.AddBinaryOperatorNode(MaterialXNodeType.Subtract, shaderGraphNode, graph, stagingEdges);
        }
    }
}