using UnityEditor.ShaderGraph;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class RemapNodeConverter : NodeConverterBase<RemapNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var abstractSyntaxTreeNode = CustomFunctionHelper.ParseHlsl(shaderGraphNode,
                "Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);");

            List<MaterialSlot> outputSlots = new();
            shaderGraphNode.GetOutputSlots(outputSlots);
            CompoundContext context = new CompoundContext(shaderGraphNode, graph, stagingEdges, "Remap", abstractSyntaxTreeNode);

            CustomFunctionHelper.CombineNodeDefs(context, abstractSyntaxTreeNode, outputSlots);
        }
    }
}
