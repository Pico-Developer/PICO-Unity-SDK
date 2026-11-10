using System.Collections.Generic;
using UnityEditor.ShaderGraph;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class NormalFromTextureNodeConverter : NodeConverterBase<NormalFromTextureNode>
    {
        private const string k_TexturePortName = "file";
        private const string k_TexcoordPortName = "texcoord";
        private const string k_DefaultColorName = "default";
        private const string k_WrapSName = "uaddressmode";
        private const string k_WrapTName = "vaddressmode";
        private const string k_FilterTypeName = "filtertype";

        private static bool TryGetDirectTexturePath(MaterialSlot slot, MaterialXGraphData graph, out string texturePath)
        {
            texturePath = null;
            if (slot?.RawDisplayName() != "Texture" || !slot.isConnected)
                return false;

            var textureOutputSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(slot);
            if (textureOutputSlot?.owner is not Texture2DAssetNode textureAssetNode || textureAssetNode.texture == null)
                return false;

            texturePath = ConverterUtil.GetExportedTextureReferencePath(graph.FilePath, textureAssetNode.texture);
            if (string.IsNullOrEmpty(texturePath))
            {
                texturePath = null;
                return false;
            }

            graph.RegisterTexture(textureAssetNode.texture);
            return true;
        }

        private static string GetDefaultTexturePath(MaterialSlot textureSlot, MaterialXGraphData graph)
        {
            if (textureSlot is Texture2DInputMaterialSlot textureInputSlot && textureInputSlot.texture != null)
            {
                graph.RegisterTexture(textureInputSlot.texture);
            }

            return ShaderGraphUtil.SlotUtil.GetSlotDefaultFilename(textureSlot);
        }

        private static string GetFilterType(FilterMode filterMode)
        {
            return filterMode switch
            {
                FilterMode.Bilinear => "linear",
                FilterMode.Trilinear => "linear",
                _ => "closest"
            };
        }

        private static string GetWrapType(TextureWrapMode wrapMode)
        {
            return wrapMode switch
            {
                TextureWrapMode.Repeat => "periodic",
                TextureWrapMode.Clamp => "clamp",
                TextureWrapMode.Mirror => "mirror",
                TextureWrapMode.MirrorOnce => "mirror",
                _ => "constant"
            };
        }

        private static string GetFilterTypeFromSampler(object filterMode)
        {
            return filterMode?.ToString() switch
            {
                "Linear" => "linear",
                "Trilinear" => "linear",
                _ => "closest"
            };
        }

        private static string GetWrapTypeFromSampler(object wrapMode)
        {
            return wrapMode?.ToString() switch
            {
                "Repeat" => "periodic",
                "Clamp" => "clamp",
                "Mirror" => "mirror",
                "MirrorOnce" => "mirror",
                _ => "constant"
            };
        }

        private static void ResolveTextureSamplingState(
            List<MaterialSlot> inputSlots,
            out string wrapModeU,
            out string wrapModeV,
            out string filterType)
        {
            wrapModeU = "periodic";
            wrapModeV = "periodic";
            filterType = "closest";

            foreach (var slot in inputSlots)
            {
                if (slot.RawDisplayName() == "Texture" && slot.isConnected)
                {
                    var textureOutputSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(slot);
                    if (textureOutputSlot?.owner is Texture2DAssetNode textureAssetNode && textureAssetNode.texture != null)
                    {
                        wrapModeU = GetWrapType(textureAssetNode.texture.wrapModeU);
                        wrapModeV = GetWrapType(textureAssetNode.texture.wrapModeV);
                        filterType = GetFilterType(textureAssetNode.texture.filterMode);
                    }
                }
                else if (slot.RawDisplayName() == "Sampler" && slot.isConnected)
                {
                    var samplerOutputSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(slot);
                    if (samplerOutputSlot?.owner is SamplerStateNode samplerStateNode)
                    {
                        wrapModeU = GetWrapTypeFromSampler(samplerStateNode.GetType().GetProperty("wrap")?.GetValue(samplerStateNode));
                        wrapModeV = wrapModeU;
                        filterType = GetFilterTypeFromSampler(samplerStateNode.GetType().GetProperty("filter")?.GetValue(samplerStateNode));
                    }
                }
            }
        }

        private static MaterialXNodeData CreateTextureSampleNode(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            string context,
            string wrapModeU,
            string wrapModeV,
            string filterType,
            string texcoordNodeName,
            MaterialXNodeData textureFileProxyNode,
            string directTexturePath)
        {
            var sampleNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, context),
                MaterialXNodeType.MaterialXImage,
                MaterialXDataType.Vector4);

            sampleNode.AddPortWithValue(k_DefaultColorName, MaterialXDataType.Vector4, new[] { 0.0f, 0.0f, 0.0f, 0.0f });
            sampleNode.AddPortWithStringValue(k_WrapSName, MaterialXDataType.String, wrapModeU);
            sampleNode.AddPortWithStringValue(k_WrapTName, MaterialXDataType.String, wrapModeV);
            sampleNode.AddPortWithStringValue(k_FilterTypeName, MaterialXDataType.String, filterType);

            if (!string.IsNullOrEmpty(directTexturePath))
            {
                sampleNode.AddPortWithStringValue(k_TexturePortName, MaterialXDataType.Filename, directTexturePath);
            }
            else if (textureFileProxyNode != null)
            {
                graph.AddPortAndEdge(textureFileProxyNode.Name, sampleNode.Name, k_TexturePortName, MaterialXDataType.Filename);
            }

            graph.AddPortAndEdge(texcoordNodeName, sampleNode.Name, k_TexcoordPortName, MaterialXDataType.Vector2);
            return sampleNode;
        }

        private static MaterialXNodeData CreateRedChannelSample(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            string context,
            MaterialXNodeData sampleNode)
        {
            var swizzleNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, context),
                MaterialXNodeType.Swizzle,
                MaterialXDataType.Float);
            swizzleNode.AddPort("in", MaterialXDataType.Vector4);
            swizzleNode.AddPortWithStringValue("channels", MaterialXDataType.String, "r");
            graph.AddEdge(sampleNode.Name, swizzleNode.Name, "in");
            return swizzleNode;
        }

        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var textureSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Texture");
            var uvSlot = (UVMaterialSlot)ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "UV");
            var offsetSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Offset");
            var strengthSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Strength");
            var outputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(shaderGraphNode);

            var inputSlots = new List<MaterialSlot>();
            shaderGraphNode.GetInputSlots(inputSlots);

            ResolveTextureSamplingState(inputSlots, out var wrapModeU, out var wrapModeV, out var filterType);

            TryGetDirectTexturePath(textureSlot, graph, out var directTexturePath);

            MaterialXNodeData textureFileProxyNode = null;
            if (string.IsNullOrEmpty(directTexturePath))
            {
                textureFileProxyNode = graph.AddNode(
                    ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureFile"),
                    MaterialXNodeType.Constant,
                    MaterialXDataType.Filename);
                textureFileProxyNode.AddPortWithStringValue(
                    "value",
                    MaterialXDataType.Filename,
                    GetDefaultTexturePath(textureSlot, graph) ?? "placeholder.png");
                stagingEdges.AddShaderGraphEdgeAndPort(textureSlot, textureFileProxyNode.Name, "value");
            }

            var uvProxyNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureUV"),
                MaterialXNodeType.Add,
                MaterialXDataType.Vector2);
            MaterialXGraphUtil.HandleUVSlot(
                uvSlot,
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureUVInput"),
                uvProxyNode.Name,
                "in1",
                graph,
                stagingEdges);
            uvProxyNode.AddPortWithValue("in2", MaterialXDataType.Vector2, new[] { 0.0f, 0.0f });

            var offsetPowNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureOffsetPow"),
                MaterialXNodeType.Power,
                MaterialXDataType.Float);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, offsetPowNode, offsetSlot, "in1", MaterialXDataType.Float);
            offsetPowNode.AddPortWithValue("in2", MaterialXDataType.Float, 3.0f);

            var offsetScaleNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureOffsetScale"),
                MaterialXNodeType.Multiply,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(offsetPowNode.Name, offsetScaleNode.Name, "in1", MaterialXDataType.Float);
            offsetScaleNode.AddPortWithValue("in2", MaterialXDataType.Float, 0.1f);

            var offsetUNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureOffsetU"),
                MaterialXNodeType.Combine2,
                MaterialXDataType.Vector2);
            graph.AddPortAndEdge(offsetScaleNode.Name, offsetUNode.Name, "in1", MaterialXDataType.Float);
            offsetUNode.AddPortWithValue("in2", MaterialXDataType.Float, 0.0f);

            var offsetVNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureOffsetV"),
                MaterialXNodeType.Combine2,
                MaterialXDataType.Vector2);
            offsetVNode.AddPortWithValue("in1", MaterialXDataType.Float, 0.0f);
            graph.AddPortAndEdge(offsetScaleNode.Name, offsetVNode.Name, "in2", MaterialXDataType.Float);

            var offsetUvUNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureOffsetUVU"),
                MaterialXNodeType.Add,
                MaterialXDataType.Vector2);
            graph.AddPortAndEdge(uvProxyNode.Name, offsetUvUNode.Name, "in1", MaterialXDataType.Vector2);
            graph.AddPortAndEdge(offsetUNode.Name, offsetUvUNode.Name, "in2", MaterialXDataType.Vector2);

            var offsetUvVNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureOffsetUVV"),
                MaterialXNodeType.Add,
                MaterialXDataType.Vector2);
            graph.AddPortAndEdge(uvProxyNode.Name, offsetUvVNode.Name, "in1", MaterialXDataType.Vector2);
            graph.AddPortAndEdge(offsetVNode.Name, offsetUvVNode.Name, "in2", MaterialXDataType.Vector2);

            var normalSampleNode = CreateTextureSampleNode(
                shaderGraphNode,
                graph,
                "NormalFromTextureSample",
                wrapModeU,
                wrapModeV,
                filterType,
                uvProxyNode.Name,
                textureFileProxyNode,
                directTexturePath);
            var normalUSampleNode = CreateTextureSampleNode(
                shaderGraphNode,
                graph,
                "NormalFromTextureSampleU",
                wrapModeU,
                wrapModeV,
                filterType,
                offsetUvUNode.Name,
                textureFileProxyNode,
                directTexturePath);
            var normalVSampleNode = CreateTextureSampleNode(
                shaderGraphNode,
                graph,
                "NormalFromTextureSampleV",
                wrapModeU,
                wrapModeV,
                filterType,
                offsetUvVNode.Name,
                textureFileProxyNode,
                directTexturePath);

            var normalSampleRedNode = CreateRedChannelSample(shaderGraphNode, graph, "NormalFromTextureSampleRed", normalSampleNode);
            var normalUSampleRedNode = CreateRedChannelSample(shaderGraphNode, graph, "NormalFromTextureSampleURed", normalUSampleNode);
            var normalVSampleRedNode = CreateRedChannelSample(shaderGraphNode, graph, "NormalFromTextureSampleVRed", normalVSampleNode);

            var strengthProxyNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureStrength"),
                MaterialXNodeType.Add,
                MaterialXDataType.Float);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, strengthProxyNode, strengthSlot, "in1", MaterialXDataType.Float);
            strengthProxyNode.AddPortWithValue("in2", MaterialXDataType.Float, 0.0f);

            var uDifferenceNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureUDifference"),
                MaterialXNodeType.Subtract,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(normalUSampleRedNode.Name, uDifferenceNode.Name, "in1", MaterialXDataType.Float);
            graph.AddPortAndEdge(normalSampleRedNode.Name, uDifferenceNode.Name, "in2", MaterialXDataType.Float);

            var vDifferenceNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureVDifference"),
                MaterialXNodeType.Subtract,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(normalVSampleRedNode.Name, vDifferenceNode.Name, "in1", MaterialXDataType.Float);
            graph.AddPortAndEdge(normalSampleRedNode.Name, vDifferenceNode.Name, "in2", MaterialXDataType.Float);

            var uStrengthNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureUStrength"),
                MaterialXNodeType.Multiply,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(uDifferenceNode.Name, uStrengthNode.Name, "in1", MaterialXDataType.Float);
            graph.AddPortAndEdge(strengthProxyNode.Name, uStrengthNode.Name, "in2", MaterialXDataType.Float);

            var vStrengthNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureVStrength"),
                MaterialXNodeType.Multiply,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(vDifferenceNode.Name, vStrengthNode.Name, "in1", MaterialXDataType.Float);
            graph.AddPortAndEdge(strengthProxyNode.Name, vStrengthNode.Name, "in2", MaterialXDataType.Float);

            var tangentUNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureTangentU"),
                MaterialXNodeType.Combine3,
                MaterialXDataType.Vector3);
            tangentUNode.AddPortWithValue("in1", MaterialXDataType.Float, 1.0f);
            tangentUNode.AddPortWithValue("in2", MaterialXDataType.Float, 0.0f);
            graph.AddPortAndEdge(uStrengthNode.Name, tangentUNode.Name, "in3", MaterialXDataType.Float);

            var tangentVNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureTangentV"),
                MaterialXNodeType.Combine3,
                MaterialXDataType.Vector3);
            tangentVNode.AddPortWithValue("in1", MaterialXDataType.Float, 0.0f);
            tangentVNode.AddPortWithValue("in2", MaterialXDataType.Float, 1.0f);
            graph.AddPortAndEdge(vStrengthNode.Name, tangentVNode.Name, "in3", MaterialXDataType.Float);

            var crossNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureCross"),
                MaterialXNodeType.CrossProduct,
                MaterialXDataType.Vector3);
            graph.AddPortAndEdge(tangentUNode.Name, crossNode.Name, "in1", MaterialXDataType.Vector3);
            graph.AddPortAndEdge(tangentVNode.Name, crossNode.Name, "in2", MaterialXDataType.Vector3);

            var normalizeNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalFromTextureOut"),
                MaterialXNodeType.Normalize,
                MaterialXDataType.Vector3);
            graph.AddPortAndEdge(crossNode.Name, normalizeNode.Name, "in", MaterialXDataType.Vector3);

            stagingEdges.AddPort(outputSlot.slotReference, normalizeNode.Name);
        }
    }
}
