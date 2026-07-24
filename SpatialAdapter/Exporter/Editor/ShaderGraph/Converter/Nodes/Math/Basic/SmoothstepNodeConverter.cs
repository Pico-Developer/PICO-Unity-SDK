using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;
using UnityEditor.ShaderGraph;
using System.Collections.Generic;

namespace Exporter.Editor.ShaderGraph.Converter.Nodes.Math.Basic
{
    internal class SmoothstepNodeConverter : NodeConverterBase<SmoothstepNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var outputSlots = new List<MaterialSlot>();
            shaderGraphNode.GetOutputSlots(outputSlots);
            var aType = TypeUtil.GetMaterialXDataType(outputSlots[0]);

            var portMap = new Dictionary<string, string>();
            portMap.Add("Edge1", "low");
            portMap.Add("Edge2", "high");
            portMap.Add("In", "in");
            var nodeData = MaterialXGraphUtil.AddNaryOperatorNode(
                MaterialXNodeType.SmoothStep, shaderGraphNode, graph, stagingEdges, "Smoothstep", portMap, outputType: aType);
        }
    }
}