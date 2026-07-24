using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class FractionNodeConverter : NodeConverterBase<FractionNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var abstractSyntaxTreeNode = CustomFunctionHelper.ParseHlsl(shaderGraphNode, "Out = In - floor(In);");

            List<MaterialSlot> outputSlots = new();
            shaderGraphNode.GetOutputSlots(outputSlots);
            CompoundContext context = new CompoundContext(shaderGraphNode, graph, stagingEdges, "Fraction", abstractSyntaxTreeNode);

            CustomFunctionHelper.CombineNodeDefs(context, abstractSyntaxTreeNode, outputSlots);
        }
    }
}
