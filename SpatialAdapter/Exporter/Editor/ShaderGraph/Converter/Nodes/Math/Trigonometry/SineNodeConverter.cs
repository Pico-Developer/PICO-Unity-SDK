using UnityEditor.ShaderGraph;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph.Math.Trigonometry
{
    internal class SineNodeConverter : NodeConverterBase<SineNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            Dictionary<string, string> portMap = new() { { "In", "in" } };
            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Sine, shaderGraphNode, graph, stagingEdges, "Sine", portMap);
        }
    }
}