using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.ShaderGraph;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph {
    internal class SampleTexture2DNodeConverter : NodeConverterBase<SampleTexture2DNode>
    {
        private const string k_OutputNodeId = "image";
        private readonly string _wrapSName = "uaddressmode";
        private readonly string _wrapTName = "vaddressmode";
        private readonly string _filterTypeName = "filtertype";
        private readonly string _defaultColorName = "default";

        private static string GetOutputChannels(MaterialSlot outputSlot)
        {
            return outputSlot.RawDisplayName() switch
            {
                "RGBA" => string.Empty,
                "RGB" => "rgb",
                "R" => "r",
                "G" => "g",
                "B" => "b",
                "A" => "a",
                _ => string.Empty
            };
        }

        private static MaterialXDataType GetOutputDataType(MaterialSlot outputSlot)
        {
            return outputSlot.RawDisplayName() switch
            {
                "RGB" => MaterialXDataType.Vector3,
                "R" => MaterialXDataType.Float,
                "G" => MaterialXDataType.Float,
                "B" => MaterialXDataType.Float,
                "A" => MaterialXDataType.Float,
                _ => MaterialXDataType.Vector4
            };
        }

        private static void RegisterOutputPorts(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            StagingEdges stagingEdges,
            MaterialXNodeData textureSampleNode,
            List<MaterialSlot> outputSlots)
        {
            foreach (var outputSlot in outputSlots)
            {
                if (!outputSlot.isConnected)
                    continue;

                string channels = GetOutputChannels(outputSlot);
                if (string.IsNullOrEmpty(channels))
                {
                    stagingEdges.AddPort(outputSlot.slotReference, textureSampleNode.Name);
                    continue;
                }

                string swizzleNodeName = ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, $"{k_OutputNodeId}_{outputSlot.RawDisplayName()}");
                if (!graph.GetOrAddNode(swizzleNodeName, MaterialXNodeType.Swizzle, GetOutputDataType(outputSlot), out var swizzleNode))
                {
                    stagingEdges.AddPort(outputSlot.slotReference, swizzleNode.Name);
                    continue;
                }

                swizzleNode.AddPort("in", MaterialXDataType.Vector4);
                swizzleNode.AddPortWithStringValue("channels", MaterialXDataType.String, channels);
                graph.AddEdge(textureSampleNode.Name, swizzleNode.Name, "in");
                stagingEdges.AddPort(outputSlot.slotReference, swizzleNode.Name);
            }
        }

        private bool TryAddDirectTextureFilePort(MaterialSlot slot, MaterialXGraphData graph, MaterialXNodeData textureSampleNode)
        {
            if (slot.RawDisplayName() != "Texture" || !slot.isConnected)
                return false;

            var textureOutputSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(slot);
            if (textureOutputSlot?.owner is not Texture2DAssetNode textureAssetNode || textureAssetNode.texture == null)
                return false;

            string texturePath = ConverterUtil.GetExportedTextureReferencePath(graph.FilePath, textureAssetNode.texture);
            if (string.IsNullOrEmpty(texturePath))
                return false;

            graph.RegisterTexture(textureAssetNode.texture);
            textureSampleNode.AddPortWithStringValue("file", MaterialXDataType.Filename, texturePath);
            return true;
        }

        private string GetFilterType(FilterMode filterMode)
        {
            switch (filterMode)
            {
                case FilterMode.Bilinear:
                case FilterMode.Trilinear:
                    return "linear";
                case FilterMode.Point:
                default:
                    return "closest";
            }
        }
        private string GetWrapType(TextureWrapMode wrapMode)
        {
            switch (wrapMode)
            {
                case TextureWrapMode.Repeat:
                    return "periodic";
                case TextureWrapMode.Clamp:
                    return "clamp";
                case TextureWrapMode.Mirror:
                case TextureWrapMode.MirrorOnce:
                    return "mirror";
                default:
                    return "constant";
            }
        }

        private string GetFilterTypeFromSampler(object filterMode)
        {
            switch (filterMode?.ToString())
            {
                case "Linear":
                case "Trilinear":
                    return "linear";
                case "Point":
                default:
                    return "closest";
            }
        }

        private string GetWrapTypeFromSampler(object wrapMode)
        {
            switch (wrapMode?.ToString())
            {
                case "Repeat":
                    return "periodic";
                case "Clamp":
                    return "clamp";
                case "Mirror":
                case "MirrorOnce":
                    return "mirror";
                default:
                    return "constant";
            }
        }
        
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            // TODO revisit use of the exact texture sampling node we export to. Image vs USDUVTexture etc. etc.
            string nodeName = ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, k_OutputNodeId);
            var textureSampleNode = graph.AddNode(nodeName, MaterialXNodeType.MaterialXImage, MaterialXDataType.Vector4);
            
            textureSampleNode.AddPortWithValue(_defaultColorName, MaterialXDataType.Vector4, new[] { 0.0f, 0.0f, 0.0f, 0.0f });

            var inputSlots = new List<MaterialSlot>();
            var outputSlots = new List<MaterialSlot>();
            shaderGraphNode.GetInputSlots(inputSlots);
            shaderGraphNode.GetOutputSlots(outputSlots);

            string wrapModeU = "periodic";
            string wrapModeV = "periodic";
            string filterTypeValue = "closest";

            foreach (var slot in inputSlots)
            {
                if (slot.RawDisplayName() == "Texture" && slot.isConnected)
                {
                    var textureOutputSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(slot);
                    if (textureOutputSlot?.owner is Texture2DAssetNode textureAssetNode)
                    {
                        wrapModeU = GetWrapType(textureAssetNode.texture.wrapModeU);
                        wrapModeV = GetWrapType(textureAssetNode.texture.wrapModeV);
                        filterTypeValue = GetFilterType(textureAssetNode.texture.filterMode);
                    }
                }
                else if (slot.RawDisplayName() == "Sampler" && slot.isConnected)
                {
                    var samplerOutputSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(slot);
                    if (samplerOutputSlot?.owner is SamplerStateNode samplerStateNode)
                    {
                        wrapModeU = GetWrapTypeFromSampler(samplerStateNode.GetType().GetProperty("wrap")?.GetValue(samplerStateNode));
                        wrapModeV = wrapModeU;
                        filterTypeValue = GetFilterTypeFromSampler(samplerStateNode.GetType().GetProperty("filter")?.GetValue(samplerStateNode));
                    }
                }
            }

            textureSampleNode.AddPortWithStringValue(_wrapSName, MaterialXDataType.String, wrapModeU);
            textureSampleNode.AddPortWithStringValue(_wrapTName, MaterialXDataType.String, wrapModeV);
            textureSampleNode.AddPortWithStringValue(_filterTypeName, MaterialXDataType.String, filterTypeValue);

            RegisterOutputPorts(shaderGraphNode, graph, stagingEdges, textureSampleNode, outputSlots);
            
            Dictionary<string, string> slotToPortName = new()
            {
                { "UV", "texcoord" },
                { "Texture", "file"}
            };
            
            foreach (var slot in inputSlots)
            {
                if (slot.RawDisplayName() == "Sampler")
                {
                    continue;
                }

                if (!slotToPortName.TryGetValue(slot.RawDisplayName(), out var portName))
                    continue;
                var portType = TypeUtil.GetMaterialXDataType(slot);

                if (TryAddDirectTextureFilePort(slot, graph, textureSampleNode))
                    continue;

                MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, textureSampleNode, slot, portName, portType);
            }
        }
    }
}
