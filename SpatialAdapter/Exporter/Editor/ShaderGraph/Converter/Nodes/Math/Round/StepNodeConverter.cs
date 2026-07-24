using UnityEditor.ShaderGraph;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph.Math.Round
{
    internal class StepNodeConverter : NodeConverterBase<StepNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            // TODO: Spatial editor only supports float step currently (12/3/2025)
            
            var outputSlots = new List<MaterialSlot>();
            shaderGraphNode.GetOutputSlots(outputSlots);
            var aType = TypeUtil.GetMaterialXDataType(outputSlots[0]);

            var portMap = new Dictionary<string, string>()
            {
                { "In",   "in"   },
                { "Edge", "edge" }
            };
            var nodeData = MaterialXGraphUtil.AddNaryOperatorNode(
                MaterialXNodeType.Step, shaderGraphNode, graph, stagingEdges, "Step", portMap, outputType: aType);
        }
    }
}