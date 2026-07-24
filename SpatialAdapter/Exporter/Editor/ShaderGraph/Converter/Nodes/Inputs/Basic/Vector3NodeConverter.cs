using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class Vector3NodeConverter : NodeConverterBase<Vector3Node>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            Dictionary<string, string> portMap = new() {
                { "X", "in1" } ,
                { "Y", "in2" } ,
                { "Z", "in3" } ,
                };

            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Combine3, shaderGraphNode, graph, stagingEdges, "Vector3", portMap);
        }
    }
}