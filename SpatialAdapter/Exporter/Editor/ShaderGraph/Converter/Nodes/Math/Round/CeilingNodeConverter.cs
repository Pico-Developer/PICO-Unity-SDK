using UnityEditor.ShaderGraph;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph.Math.Round
{
    internal class CeilingNodeConverter : NodeConverterBase<CeilingNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            Dictionary<string, string> portMap = new() { { "In", "in" } };
            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Ceil, shaderGraphNode, graph, stagingEdges, "Ceiling", portMap);
        }
    }
}