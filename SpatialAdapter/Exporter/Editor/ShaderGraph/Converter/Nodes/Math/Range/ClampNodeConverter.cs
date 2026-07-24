using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ClampNodeConverter : NodeConverterBase<ClampNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            Dictionary<string, string> portMap = new Dictionary<string, string>()
            {
                {"In",  "in" },
                {"Min", "low"},
                {"Max", "high"},
            };
            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Clamp, shaderGraphNode, graph, stagingEdges, "Clamp", portMap);
        }
    }
}