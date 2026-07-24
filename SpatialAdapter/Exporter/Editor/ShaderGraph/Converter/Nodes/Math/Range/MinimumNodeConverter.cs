using UnityEditor.ShaderGraph;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class MinimumNodeConverter : NodeConverterBase<MinimumNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph,
            StagingEdges stagingEdges)
        {
            Dictionary<string, string> portMap = new Dictionary<string, string>()
            {
                {"A", "in1" },
                {"B", "in2"},
            };

            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Min, shaderGraphNode, graph, stagingEdges,
                "Min", portMap);
        }
    }
}