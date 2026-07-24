using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ProjectionNodeConverter : NodeConverterBase<ProjectionNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var abstractSyntaxTreeNode = CustomFunctionHelper.ParseHlsl(shaderGraphNode, "Out = B * dot(A, B) / dot(B, B);");

            List<MaterialSlot> outputSlots = new();
            shaderGraphNode.GetOutputSlots(outputSlots);
            CompoundContext context = new CompoundContext(shaderGraphNode, graph, stagingEdges, "Projection", abstractSyntaxTreeNode);

            CustomFunctionHelper.CombineNodeDefs(context, abstractSyntaxTreeNode, outputSlots);
        }
    }
}
