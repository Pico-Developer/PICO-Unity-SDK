using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class Vector1NodeConverter : NodeConverterBase<Vector1Node>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            Dictionary<string, string> portMap = new() { { "X", "value" } };

            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Constant, shaderGraphNode, graph, stagingEdges, "Vector1", portMap);
        }
    }
}