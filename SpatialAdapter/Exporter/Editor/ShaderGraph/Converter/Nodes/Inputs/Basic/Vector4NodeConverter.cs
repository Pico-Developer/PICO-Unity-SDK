using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class Vector4NodeConverter : NodeConverterBase<Vector4Node>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph,
            StagingEdges stagingEdges)
        {
            Dictionary<string, string> portMap = new() {
                { "X", "in1" } ,
                { "Y", "in2" } ,
                { "Z", "in3" } ,
                { "W", "in4" }
            };

            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Combine4, shaderGraphNode, graph, stagingEdges, "Vector4", portMap); 
        }
        
    }
}