using System.Collections.Generic;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Internal;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class FresnelEffectNodeConverter : NodeConverterBase<FresnelNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {

                Dictionary<string, MaterialXDataType> inputNamesTypes = new Dictionary<string, MaterialXDataType>(){
                    ["Normal"] = MaterialXDataType.Vector3,
                    ["ViewDir"] = MaterialXDataType.Vector3,
                    ["Power"] = MaterialXDataType.Float,
                };
                var abstractSyntaxTreeNode = CustomFunctionHelper.ParseHlsl(inputNamesTypes, "Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);");

                List<MaterialSlot> outputSlots = new();
                shaderGraphNode.GetOutputSlots(outputSlots);
                CompoundContext context = new CompoundContext(shaderGraphNode, graph, stagingEdges, "FresnelEffect", abstractSyntaxTreeNode);

                // 2. Insert ast to materialX graph
                CustomFunctionHelper.CombineNodeDefs(context, abstractSyntaxTreeNode, outputSlots);
        }
    }
}