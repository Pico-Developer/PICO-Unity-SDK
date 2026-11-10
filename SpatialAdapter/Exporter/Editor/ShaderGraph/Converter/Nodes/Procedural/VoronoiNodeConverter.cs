using System.Collections.Generic;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class VoronoiNodeConverter : NodeConverterBase<VoronoiNode>
    {
        private static void AddInputPortFromSlot(
            MaterialSlot slot,
            MaterialXNodeData nodeData,
            string portName,
            MaterialXDataType portType,
            MaterialXGraphData graph,
            StagingEdges stagingEdges)
        {
            var defaultNumericValue = ShaderGraphUtil.SlotUtil.GetSlotDefaultValue(slot);
            var defaultStringValue = ShaderGraphUtil.SlotUtil.GetSlotDefaultFilename(slot);

            if (defaultNumericValue != null)
                nodeData.AddPortWithValue(portName, portType, defaultNumericValue);
            else if (!string.IsNullOrEmpty(defaultStringValue))
                nodeData.AddPortWithStringValue(portName, portType, defaultStringValue);
            else
                nodeData.AddPort(portName, portType);

            if (!slot.isConnected)
                return;

            var upstreamSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(slot);
            if (upstreamSlot == null)
                return;

            if (!stagingEdges.SlotToPort.TryGetValue(upstreamSlot.slotReference, out var srcPort))
            {
                ConverterLookup.ConvertNode(upstreamSlot.owner, graph, stagingEdges);
                if (!stagingEdges.SlotToPort.TryGetValue(upstreamSlot.slotReference, out srcPort))
                    return;
            }

            graph.AddEdge(srcPort.xNode, nodeData.Name, portName);
        }

        private static bool TryHandleTilingAndOffsetInput(
            AbstractMaterialNode shaderGraphNode,
            UVMaterialSlot uvSlot,
            MaterialSlot cellDensitySlot,
            MaterialXNodeData noiseNode,
            MaterialXGraphData graph,
            StagingEdges stagingEdges)
        {
            if (!uvSlot.isConnected)
                return false;

            var uvSourceSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(uvSlot);
            if (uvSourceSlot?.owner is not TilingAndOffsetNode tilingAndOffsetNode)
                return false;

            var tilingSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(tilingAndOffsetNode, "Tiling");
            var offsetSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(tilingAndOffsetNode, "Offset");
            if (tilingSlot == null || offsetSlot == null)
                return false;

            var effectiveTilingNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "VoronoiEffectiveTiling"),
                MaterialXNodeType.Multiply,
                MaterialXDataType.Vector2);
            AddInputPortFromSlot(tilingSlot, effectiveTilingNode, "in1", MaterialXDataType.Vector2, graph, stagingEdges);
            AddInputPortFromSlot(cellDensitySlot, effectiveTilingNode, "in2", MaterialXDataType.Float, graph, stagingEdges);

            var tilingNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "VoronoiTilingMul"),
                MaterialXNodeType.Multiply,
                MaterialXDataType.Vector2);
            var uv0Node = MaterialXGraphUtil.AddUVNode(
                graph,
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "VoronoiUV"),
                0);
            graph.AddPortAndEdge(uv0Node.Name, tilingNode.Name, "in1", MaterialXDataType.Vector2);
            graph.AddPortAndEdge(effectiveTilingNode.Name, tilingNode.Name, "in2", MaterialXDataType.Vector2);

            var offsetNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "VoronoiOffsetAdd"),
                MaterialXNodeType.Add,
                MaterialXDataType.Vector2);
            graph.AddPortAndEdge(tilingNode.Name, offsetNode.Name, "in1", MaterialXDataType.Vector2);

            var offsetFlipNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "VoronoiOffsetFlip"),
                MaterialXNodeType.Multiply,
                MaterialXDataType.Vector2);
            AddInputPortFromSlot(offsetSlot, offsetFlipNode, "in1", MaterialXDataType.Vector2, graph, stagingEdges);
            offsetFlipNode.AddPortWithValue("in2", MaterialXDataType.Vector2, new float[] { 1.0f, -1.0f });
            graph.AddPortAndEdge(offsetFlipNode.Name, offsetNode.Name, "in2", MaterialXDataType.Vector2);

            graph.AddPortAndEdge(offsetNode.Name, noiseNode.Name, "texcoord", MaterialXDataType.Vector2);
            return true;
        }

        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var outputSlots = new List<MaterialSlot>();
            shaderGraphNode.GetOutputSlots(outputSlots);

            bool hasConnectedOutput = false;
            foreach (var outputSlot in outputSlots)
            {
                if (outputSlot.isConnected)
                {
                    hasConnectedOutput = true;
                    break;
                }
            }

            if (!hasConnectedOutput)
                return;

            var uvSlot = (UVMaterialSlot)ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "UV");
            var angleOffsetSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "AngleOffset");
            var cellDensitySlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "CellDensity");

            string nodeName = ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "Voronoi");
            if (!graph.GetOrAddNode(nodeName, MaterialXNodeType.WorleyNoise2D, MaterialXDataType.Float, out var noiseNode))
            {
                foreach (var outputSlot in outputSlots)
                {
                    if (outputSlot.isConnected)
                        stagingEdges.AddPort(outputSlot.slotReference, noiseNode.Name);
                }
                return;
            }

            if (!TryHandleTilingAndOffsetInput(shaderGraphNode, uvSlot, cellDensitySlot, noiseNode, graph, stagingEdges))
            {
                var scaledUvNode = graph.AddNode(
                    ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "VoronoiScale"),
                    MaterialXNodeType.Multiply,
                    MaterialXDataType.Vector2);
                MaterialXGraphUtil.HandleUVSlot(
                    uvSlot,
                    ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "VoronoiUV"),
                    scaledUvNode.Name,
                    "in1",
                    graph,
                    stagingEdges);
                MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, scaledUvNode, cellDensitySlot, "in2", MaterialXDataType.Float);
                graph.AddPortAndEdge(scaledUvNode.Name, noiseNode.Name, "texcoord", MaterialXDataType.Vector2);
            }
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, noiseNode, angleOffsetSlot, "jitter", MaterialXDataType.Float);

            foreach (var outputSlot in outputSlots)
            {
                if (outputSlot.isConnected)
                    stagingEdges.AddPort(outputSlot.slotReference, noiseNode.Name);
            }
        }
    }
}
