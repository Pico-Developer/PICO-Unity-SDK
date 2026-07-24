using UnityEditor.ShaderGraph;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph.Math.Round
{
    internal class AbsoluteNodeConverter : NodeConverterBase<AbsoluteNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            Dictionary<string, string> portMap = new() { { "In", "in" } };
            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.AbsVal, shaderGraphNode, graph, stagingEdges, "Abs", portMap);
        }
    }
}