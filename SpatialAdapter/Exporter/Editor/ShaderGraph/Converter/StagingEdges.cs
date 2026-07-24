using System;
using System.Collections.Generic;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class StagingEdges
    {
        internal Dictionary<SlotReference, (string xNode, string xPort)> SlotToPort = new();
        internal Dictionary<(string xNode, string xPort), SlotReference> PortToSlot = new();
        internal List<Edge> ShaderGraphEdges = new();

        internal void AddPort(SlotReference slot, string materialXNode, string materialXPort = "")
        {
            if (SlotToPort.TryGetValue(slot, out var existing) && existing.xNode == materialXNode &&
                existing.xPort == materialXPort)
            {
                return;
            }
            
            SlotToPort.Add(slot, (xNode: materialXNode, xPort: materialXPort));
            PortToSlot.TryAdd((xNode: materialXNode, xPort: materialXPort), slot);
        }

        internal void AddShaderGraphEdge(SlotReference src, SlotReference dst)
        {
            ShaderGraphEdges.Add(new Edge(src, dst));
        }

        internal void AddShaderGraphEdgeAndPort(MaterialSlot slot, string materialXNode, string materialXPort)
        {
            if (slot.isConnected)
            {
                MaterialSlot outputSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(slot);
                AddShaderGraphEdge(outputSlot.slotReference, slot.slotReference);
                AddPort(slot.slotReference, materialXNode, materialXPort);
            }
        }

        internal void ResolvePortConnections(MaterialXGraphData graphData)
        {
            foreach (var edge in ShaderGraphEdges)
            {
                try
                {
                    var edgeSrc = edge.outputSlot;
                    var edgeDst = edge.inputSlot;
                
                    var src = SlotToPort[edgeSrc];
                    var dst = SlotToPort[edgeDst];
                    graphData.AddEdge(src.xNode, dst.xNode, dst.xPort);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to resolve port connections for " + graphData.Name + ": " + e.Message);
                }
            }
            
            HandleTypeConversion(graphData);
        }

        private static void HandleTypeConversion(MaterialXGraphData graphData)
        {
            Dictionary<(string dstNode, string dstPort), string> updatedEdges = new();

            foreach (var edge in graphData.Edges)
            {
                string srcNodeName = edge.Value;
                string dstNodeName = edge.Key.dstNode;
                string dstPortName = edge.Key.dstPort;

                MaterialXNodeData srcNode = graphData.GetNode(srcNodeName);
                MaterialXNodeData dstNode = graphData.GetNode(dstNodeName);

                MaterialXPortData dstPort = dstNode.GetPort(dstPortName);

                MaterialXDataType srcType = srcNode.DataType;
                MaterialXDataType dstType = dstPort.DataType;

                
                // Type matches, no need for conversion
                if (srcType == dstType)
                {
                    updatedEdges.Add((dstNodeName, dstPortName), srcNodeName);
                    continue;
                }
                
                // Swizzle doesn't support more than 4 components
                if (srcType.GetSizeInByte() > 16 || dstType.GetSizeInByte() > 16)
                {
                    throw new System.Exception($"MaterialX cannot convert from {srcType} to {dstType}, from '{srcNodeName}' to '{dstNodeName}.{dstPortName}'.");
                }

                string convertTitle = $"From{srcNodeName}To{dstNodeName}{dstPortName}";
                
                // convert int/bool to float as that is the standard input type for node
                if (srcType.GetSizeInByte() == 4 && srcType != MaterialXDataType.Float)
                {
                    string convertNodeName = $"ConvertSrcTypeToFloat_{convertTitle}";
                    var convertNode = graphData.AddNode(convertNodeName, MaterialXNodeType.Convert, MaterialXDataType.Float);
                    string convertNodeInputPortName = "in";
                    convertNode.AddPort(convertNodeInputPortName, srcType);

                    updatedEdges.Add((convertNodeName, convertNodeInputPortName), srcNodeName);

                    srcType = MaterialXDataType.Float;
                    srcNodeName = convertNodeName;

                    if (srcType == dstType)
                    {
                        updatedEdges.Add((dstNodeName, dstPortName), srcNodeName);
                        continue;
                    }
                }
                
                if (srcType.GetSizeInByte() >= dstType.GetSizeInByte()
                    || srcType.GetSizeInByte() == 4 && dstType.GetSizeInByte() != 4)
                {
                    var swizzleNodeName = $"ConvertSwizzle_{convertTitle}";
                    var convertNode = graphData.AddNode(swizzleNodeName, MaterialXNodeType.Swizzle, dstType);
                    convertNode.AddPort("in", srcType);
                    convertNode.AddPortWithStringValue("channels", MaterialXDataType.String,
                        ConverterUtil.GenerateSwizzleChannelString(srcType, dstType));

                    updatedEdges.Add((swizzleNodeName, "in"), srcNodeName);
                    srcNodeName = swizzleNodeName;
                }
                else if (srcType.GetSizeInByte() < dstType.GetSizeInByte())
                {
                    var channelCombineNode = graphData.AddNode($"ConvertCombine_{convertTitle}", 
                        $"combine{dstType.ChannelCount()}", dstType);
                    
                    for (int i = 0; i < dstType.ChannelCount(); ++i)
                    {
                        channelCombineNode.AddPortWithValue($"in{i+1}", MaterialXDataType.Float, 0.0f);
                        if (i < srcType.ChannelCount())
                        {
                            var swizzleNode = graphData.AddNode($"ConvertSwizzle{i+1}_{convertTitle}", MaterialXNodeType.Swizzle, MaterialXDataType.Float);
                            swizzleNode.AddPort("in", srcType);
                            swizzleNode.AddPortWithStringValue("channels", MaterialXDataType.String, (srcType.IsColor() ? "rgba" : "xyzw").Substring(i, 1));
                            updatedEdges.Add((swizzleNode.Name, "in"), srcNodeName);
                            updatedEdges.Add((channelCombineNode.Name, $"in{i+1}"), swizzleNode.Name);
                        }
                    }

                    srcNodeName = channelCombineNode.Name;
                }
                
                updatedEdges.Add((dstNodeName, dstPortName), srcNodeName);
            }
            
            graphData.Edges.Clear();
            graphData.Edges = updatedEdges;
        }
    }
}