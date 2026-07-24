using System.Collections.Generic;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Internal;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class CustomFunctionConverter: NodeConverterBase<CustomFunctionNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            if (shaderGraphNode is CustomFunctionNode customFunctionNode)
            {
                // 1. Convert hlsl code to ast
                var abstractSyntaxTreeNode = CustomFunctionHelper.ParseHlsl(customFunctionNode, customFunctionNode.functionBody);

                List<MaterialSlot> outputSlots = new();
                shaderGraphNode.GetOutputSlots(outputSlots);
                CompoundContext context = new CompoundContext(shaderGraphNode, graph, stagingEdges, customFunctionNode.functionName, abstractSyntaxTreeNode);

                // 2. Insert ast to materialX graph
                CustomFunctionHelper.CombineNodeDefs(context, abstractSyntaxTreeNode, outputSlots);
            }
        }
        public override bool NodeCanConvert(AbstractMaterialNode shaderGraphNode)
        {
            return ((CustomFunctionNode)shaderGraphNode).sourceType == HlslSourceType.String;
        }
    }
}