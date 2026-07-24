using UnityEditor.ShaderGraph;
using System.Collections.Generic;
using NUnit.Framework;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class SliderNodeConverter : NodeConverterBase<SliderNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var outputSlots = new List<MaterialSlot>();
            shaderGraphNode.GetOutputSlots(outputSlots);
            
            var nodeType = TypeUtil.GetMaterialXDataType(outputSlots[0]);
            var graphNode = MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Clamp, shaderGraphNode, graph, stagingEdges, "Slider");

            SliderNode sliderNode = (SliderNode)shaderGraphNode;
            graphNode.AddPortWithValue("in",   nodeType, sliderNode.value.x);
            graphNode.AddPortWithValue("low",  nodeType, sliderNode.value.y);
            graphNode.AddPortWithValue("high", nodeType, sliderNode.value.z);
        }
    }
}