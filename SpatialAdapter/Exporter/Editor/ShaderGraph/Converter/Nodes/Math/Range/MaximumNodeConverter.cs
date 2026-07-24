using UnityEditor.ShaderGraph;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class MaximumNodeConverter : NodeConverterBase<MaximumNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            Dictionary<string, string> portMap = new Dictionary<string, string>()
            {
                {"A", "in1" },
                {"B", "in2"},
            };

            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Max, shaderGraphNode, graph, stagingEdges,
                "Max", portMap);
        }
    }
}