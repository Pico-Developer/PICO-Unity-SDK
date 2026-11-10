using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class SplitNodeConverter : NodeConverterBase<SplitNode>
    {
        private static bool TryGetUpstreamPort(
            MaterialSlot slot,
            MaterialXGraphData graph,
            StagingEdges stagingEdges,
            out (string xNode, string xPort) srcPort)
        {
            srcPort = default;
            if (!slot.isConnected)
                return false;

            var upstreamSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(slot);
            if (upstreamSlot == null)
                return false;

            if (!stagingEdges.SlotToPort.TryGetValue(upstreamSlot.slotReference, out srcPort))
            {
                ConverterLookup.ConvertNode(upstreamSlot.owner, graph, stagingEdges);
                if (!stagingEdges.SlotToPort.TryGetValue(upstreamSlot.slotReference, out srcPort))
                    return false;
            }

            return true;
        }

        private static string GetChannel(MaterialSlot outputSlot)
        {
            return outputSlot.RawDisplayName() switch
            {
                "R" => "r",
                "G" => "g",
                "B" => "b",
                "A" => "a",
                "X" => "x",
                "Y" => "y",
                "Z" => "z",
                "W" => "w",
                _ => outputSlot.shaderOutputName?.ToUpperInvariant() switch
                {
                    "R" => "r",
                    "G" => "g",
                    "B" => "b",
                    "A" => "a",
                    "X" => "x",
                    "Y" => "y",
                    "Z" => "z",
                    "W" => "w",
                    _ => null
                }
            };
        }

        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var inputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryInputSlot(shaderGraphNode);
            if (inputSlot == null)
                return;

            MaterialXDataType inputType = TypeUtil.GetMaterialXDataType(inputSlot);

            var outputSlots = new List<MaterialSlot>();
            shaderGraphNode.GetOutputSlots(outputSlots);

            foreach (var outputSlot in outputSlots)
            {
                if (!outputSlot.isConnected)
                    continue;

                string channel = GetChannel(outputSlot);
                if (string.IsNullOrEmpty(channel))
                    continue;

                string separateNodeName = ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, $"Separate_{outputSlot.RawDisplayName()}");
                if (!graph.GetOrAddNode(separateNodeName, MaterialXNodeType.Swizzle, MaterialXDataType.Float, out var separateNode))
                {
                    stagingEdges.AddPort(outputSlot.slotReference, separateNode.Name);
                    continue;
                }

                separateNode.AddPortWithStringValue("channels", MaterialXDataType.String, channel);
                var defaultNumericValue = ShaderGraphUtil.SlotUtil.GetSlotDefaultValue(inputSlot);
                var defaultStringValue = ShaderGraphUtil.SlotUtil.GetSlotDefaultFilename(inputSlot);

                if (TryGetUpstreamPort(inputSlot, graph, stagingEdges, out var srcPort))
                {
                    separateNode.AddPort("in", inputType);
                    graph.AddEdge(srcPort.xNode, separateNode.Name, "in");
                }
                else if (defaultNumericValue != null)
                {
                    separateNode.AddPortWithValue("in", inputType, defaultNumericValue);
                }
                else if (!string.IsNullOrEmpty(defaultStringValue))
                {
                    separateNode.AddPortWithStringValue("in", inputType, defaultStringValue);
                }
                else
                {
                    separateNode.AddPort("in", inputType);
                }

                stagingEdges.AddPort(outputSlot.slotReference, separateNode.Name);
            }
        }
    }
}
