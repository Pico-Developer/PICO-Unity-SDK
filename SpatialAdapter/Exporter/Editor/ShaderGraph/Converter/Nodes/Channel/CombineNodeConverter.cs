using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class CombineNodeConverter : NodeConverterBase<CombineNode>
    {
        private readonly struct OutputDefinition
        {
            internal readonly string Context;
            internal readonly string MaterialXNodeType;
            internal readonly MaterialXDataType OutputType;
            internal readonly string[] InputNames;

            internal OutputDefinition(
                string context,
                string materialXNodeType,
                MaterialXDataType outputType,
                params string[] inputNames)
            {
                Context = context;
                MaterialXNodeType = materialXNodeType;
                OutputType = outputType;
                InputNames = inputNames;
            }
        }

        private static readonly Dictionary<string, OutputDefinition> OutputDefinitions = new()
        {
            { "RGBA", new OutputDefinition("Combine_RGBA", MaterialXNodeType.Combine4, MaterialXDataType.Vector4, "R", "G", "B", "A") },
            { "RGB", new OutputDefinition("Combine_RGB", MaterialXNodeType.Combine3, MaterialXDataType.Vector3, "R", "G", "B") },
            { "RG", new OutputDefinition("Combine_RG", MaterialXNodeType.Combine2, MaterialXDataType.Vector2, "R", "G") },
        };

        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var outputSlots = new List<MaterialSlot>();
            shaderGraphNode.GetOutputSlots(outputSlots);

            foreach (var outputSlot in outputSlots)
            {
                if (!outputSlot.isConnected)
                    continue;

                string outputName = outputSlot.shaderOutputName?.ToUpperInvariant() ?? outputSlot.RawDisplayName().ToUpperInvariant();
                if (!OutputDefinitions.TryGetValue(outputName, out var outputDefinition))
                    continue;

                string nodeName = ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, outputDefinition.Context);
                if (!graph.GetOrAddNode(
                        nodeName,
                        outputDefinition.MaterialXNodeType,
                        outputDefinition.OutputType,
                        out var combineNode))
                {
                    stagingEdges.AddPort(outputSlot.slotReference, combineNode.Name);
                    continue;
                }

                for (int index = 0; index < outputDefinition.InputNames.Length; ++index)
                {
                    string inputName = outputDefinition.InputNames[index];
                    MaterialSlot inputSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, inputName);
                    if (inputSlot == null)
                        continue;

                    MaterialXGraphUtil.AddInputPortAndEdge(
                        stagingEdges,
                        combineNode,
                        inputSlot,
                        $"in{index + 1}",
                        TypeUtil.GetMaterialXDataType(inputSlot));
                }

                stagingEdges.AddPort(outputSlot.slotReference, combineNode.Name);
            }
        }
    }
}
