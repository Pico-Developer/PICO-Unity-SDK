using UnityEditor.ShaderGraph;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ReflectNodeConverter : NodeConverterBase<ReflectionNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            // TODO: Spatial editor only supports vector3 reflect currently (12/3/2025)
            
            Dictionary<string, string> portMap = new()
            {
                { "In", "in" },
                { "Normal", "normal" }
            };
            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Reflect, shaderGraphNode, graph, stagingEdges, "Reflection", portMap);
        }
    }
}