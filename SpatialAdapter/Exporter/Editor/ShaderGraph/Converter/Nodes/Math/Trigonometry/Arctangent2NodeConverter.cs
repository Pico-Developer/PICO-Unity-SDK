using UnityEditor.ShaderGraph;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class Arctangent2NodeConverter : NodeConverterBase<Arctangent2Node>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            Dictionary<string, string> portMap = new()
            {
                { "A", "iny" },
                { "B", "inx" }
            };
            MaterialXGraphUtil.AddNaryOperatorNode(
                MaterialXNodeType.Arctangent2, shaderGraphNode, graph, stagingEdges, "Arctangent2", portMap);
        }
    }
}
