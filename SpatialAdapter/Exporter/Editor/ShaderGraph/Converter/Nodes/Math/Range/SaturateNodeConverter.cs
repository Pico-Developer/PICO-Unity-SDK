using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class SaturateNodeConverter : NodeConverterBase<SaturateNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var outputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(shaderGraphNode);
            var outputType = TypeUtil.GetMaterialXDataType(outputSlot);

            Dictionary<string, string> portMap = new()
            {
                { "In", "in" },
            };

            var clampNode = MaterialXGraphUtil.AddNaryOperatorNode(
                MaterialXNodeType.Clamp,
                shaderGraphNode,
                graph,
                stagingEdges,
                "Saturate",
                portMap,
                outputType: outputType); 

            clampNode.AddPortWithValue("low", outputType, new float[outputType.ChannelCount()]);
            clampNode.AddPortWithValue("high", outputType, CreateOneValue(outputType.ChannelCount()));
        }

        private static float[] CreateOneValue(int channelCount)
        {
            var values = new float[channelCount];
            for (int i = 0; i < values.Length; ++i)
            {
                values[i] = 1.0f;
            }

            return values;
        }
    }
}
