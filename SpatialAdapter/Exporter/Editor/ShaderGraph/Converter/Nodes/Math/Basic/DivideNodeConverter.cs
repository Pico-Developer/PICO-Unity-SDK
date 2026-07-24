using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;
using UnityEditor.ShaderGraph;

namespace Exporter.Editor.ShaderGraph.Converter.Nodes.Math.Basic
{
    internal class DivideNodeConverter : NodeConverterBase<DivideNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            MaterialXGraphUtil.AddBinaryOperatorNode(MaterialXNodeType.Divide, shaderGraphNode, graph, stagingEdges);
        }
    }
}