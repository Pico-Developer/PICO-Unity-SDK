using UnityEditor.ShaderGraph;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph.Channel
{
    internal class ColorMaskNodeConverter : NodeConverterBase<ColorMaskNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            Dictionary<string, MaterialXDataType> inputNamesTypes = new Dictionary<string, MaterialXDataType>(){
                ["In"]        = MaterialXDataType.Vector3,
                ["MaskColor"] = MaterialXDataType.Color3,
                ["Range"]     = MaterialXDataType.Float,
                ["Fuzziness"] = MaterialXDataType.Float,
            };
            var abstractSyntaxTreeNode = CustomFunctionHelper.ParseHlsl(inputNamesTypes, "Out = saturate(1 - (distance(MaskColor, In) - Range) / max(Fuzziness, 1e-5));");
            
            List<MaterialSlot> outputSlots = new();
            shaderGraphNode.GetOutputSlots(outputSlots);
            CompoundContext context = new CompoundContext(shaderGraphNode, graph, stagingEdges, "ColorMask", abstractSyntaxTreeNode);

            CustomFunctionHelper.CombineNodeDefs(context, abstractSyntaxTreeNode, outputSlots);
        }
    }
}