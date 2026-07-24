using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class Vector2NodeConverter : NodeConverterBase<Vector2Node>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            Dictionary<string, string> portMap = new() {
                { "X", "in1" } ,
                { "Y", "in2" } ,
                };
            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Combine2, shaderGraphNode, graph, stagingEdges, "Vector2", portMap);
        }
    }
}