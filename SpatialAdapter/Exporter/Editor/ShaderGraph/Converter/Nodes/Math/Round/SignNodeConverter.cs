using UnityEditor.ShaderGraph;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph.Math.Round
{
    internal class SignNodeConverter : NodeConverterBase<SignNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            Dictionary<string, string> portMap = new() { { "In", "in" } };
            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Sign, shaderGraphNode, graph, stagingEdges, "Sign", portMap);
        }
    }
}