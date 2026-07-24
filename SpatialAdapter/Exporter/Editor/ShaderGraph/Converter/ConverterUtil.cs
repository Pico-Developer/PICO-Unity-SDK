using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEngine;
using NodeUtils = UnityEditor.Graphing.NodeUtils;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    public static class ConverterUtil
    {
        // Size of src type must be larger or equal to the size of dst type
        internal static string GenerateSwizzleChannelString(MaterialXDataType sourceType, MaterialXDataType destinationType)
        {
            if (sourceType == destinationType) return string.Empty;
            string channelString;
            if (sourceType.IsColor())
            {
                channelString = "rgba";
            }
            else if (sourceType.IsScalar())
            {
                channelString = "xxxx";
            }
            else
            {
                channelString = "xyzw";
            }

            return channelString.Substring(0, destinationType.ChannelCount());
        }
        
        public static string SanitizeName(string rawName)
        {
            string sanitizedName = Regex.Replace(rawName, "\\W+", "");
            return sanitizedName.Length != 0 && !char.IsDigit(sanitizedName[0]) ? sanitizedName : "_" + sanitizedName;
        }
        
        internal static string RemoveWhitespace(string rawValue)
        {
            return Regex.Replace(rawValue, @"\s+", "");
        }

        internal static string GetExportedTextureReferencePath(string graphAssetPath, Texture texture)
        {
            if (texture == null)
                return null;

            return GetExportedTextureReferencePath(graphAssetPath, AssetDatabase.GetAssetPath(texture));
        }

        internal static string GetExportedTextureReferencePath(string graphAssetPath, string textureAssetPath)
        {
            if (string.IsNullOrEmpty(graphAssetPath) || string.IsNullOrEmpty(textureAssetPath))
                return null;

            string graphDirectory = Path.GetDirectoryName(graphAssetPath);
            string textureDirectory = Path.GetDirectoryName(textureAssetPath);
            string textureFileName = Path.GetFileName(textureAssetPath);

            if (string.IsNullOrEmpty(textureFileName))
                return null;

            if (string.IsNullOrEmpty(graphDirectory) || string.IsNullOrEmpty(textureDirectory))
                return textureFileName;

            string relativeTextureDirectory = Path.GetRelativePath(graphDirectory, textureDirectory);
            return string.IsNullOrEmpty(relativeTextureDirectory) || relativeTextureDirectory == "."
                ? textureFileName
                : Path.Combine(relativeTextureDirectory, textureFileName);
        }

        internal static void EnsureImplicitProperty(string nodeName, MaterialXDataType dataType, MaterialXGraphData graph)
        {
            if (!graph.HasNode(nodeName))
            {
                var nodeData = graph.AddNode(nodeName, MaterialXNodeType.Constant, dataType, false, true);
                if (MaterialXDataTypeExtensions.IsString(dataType))
                    nodeData.AddPortWithStringValue("value", dataType, "ERR");
                else if (MaterialXDataTypeExtensions.IsMatrix(dataType))
                    nodeData.AddPortWithValue("value", dataType, new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 });
                else
                    nodeData.AddPortWithValue("value", dataType, new float[MaterialXDataTypeExtensions.GetLength(dataType)]);
            }
        }
    }
    
    internal static class MaterialXGraphUtil {
        internal static void AddInputPortAndEdge(
            StagingEdges stagingEdges, MaterialXNodeData nodeData, MaterialSlot slot, string portName, MaterialXDataType portType)
        {
            var defaultNumericValue = ShaderGraphUtil.SlotUtil.GetSlotDefaultValue(slot);
            var defaultStringValue = ShaderGraphUtil.SlotUtil.GetSlotDefaultFilename(slot);

            if (defaultNumericValue != null)
                nodeData.AddPortWithValue(portName, portType, defaultNumericValue);
            else if (!string.IsNullOrEmpty(defaultStringValue))
                nodeData.AddPortWithStringValue(portName, portType, defaultStringValue);
            else
                nodeData.AddPort(portName, portType);

            stagingEdges.AddShaderGraphEdgeAndPort(slot, nodeData.Name, portName);
        }

        internal static MaterialXNodeData AddUnaryOperatorNode(string nodeType, AbstractMaterialNode node,
            MaterialXGraphData graphData, StagingEdges stagingEdges, string portName = "in",
            MaterialXDataType? outputTypeOverride = null, string slotNameOverride = "")
        {
            string nodeName = ShaderGraphUtil.NodeUtil.GetNodeName(node);
            var outputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(node);
            MaterialXDataType outputDataType = outputTypeOverride ??
                                               TypeUtil.GetMaterialXDataType(outputSlot);
            
            var nodeData = graphData.AddNode(nodeName, nodeType, outputDataType);

            var inputSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(node, slotNameOverride) ?? ShaderGraphUtil.SlotUtil.GetPrimaryInputSlot(node);
            var inputValue = ShaderGraphUtil.SlotUtil.GetSlotDefaultValue(inputSlot);
            var inputDataType = outputTypeOverride ?? TypeUtil.GetMaterialXDataType(outputSlot);
            nodeData.AddPortWithValue(portName, inputDataType, inputValue);


            stagingEdges?.AddPort(outputSlot.slotReference, nodeName);
            stagingEdges?.AddShaderGraphEdgeAndPort(inputSlot, nodeData.Name, portName);

            return nodeData;
        }

        internal static MaterialXNodeData AddBinaryOperatorNode(string nodeType,
            AbstractMaterialNode node,
            MaterialXGraphData graphData,
            StagingEdges stagingEdges,
            string leftParam = "A",
            string materialXBaseName = "in")
        {
            string nodeName = ShaderGraphUtil.NodeUtil.GetNodeName(node);
            MaterialXDataType outputDataType = TypeUtil.GetMaterialXDataType(ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(node));

            var nodeData = graphData.AddNode(nodeName, nodeType, outputDataType);

            var inputSlots = new List<MaterialSlot>();
            var outputSlots = new List<MaterialSlot>();
            node.GetInputSlots(inputSlots);
            node.GetOutputSlots(outputSlots);

            stagingEdges.AddPort(outputSlots[0].slotReference, nodeName);

            foreach (var slot in inputSlots)
            {
                var portName = slot.shaderOutputName == leftParam ? $"{materialXBaseName}1" : $"{materialXBaseName}2";
                var value = ShaderGraphUtil.SlotUtil.GetSlotDefaultValue(slot);
                var slotType = TypeUtil.GetMaterialXDataType(slot);
                nodeData.AddPortWithValue(portName, slotType, value);

                var upstreamSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(slot);
                if (upstreamSlot != null)
                {
                    stagingEdges.AddPort(slot.slotReference, nodeName, portName);
                    stagingEdges.AddShaderGraphEdge(upstreamSlot.slotReference, slot.slotReference);
                }
            }

            return nodeData;
        }
        
        internal static MaterialXNodeData AddNaryOperatorNode(
            string nodeType,
            AbstractMaterialNode node,
            MaterialXGraphData graph,
            StagingEdges stagingEdges,
            string context,
            Dictionary<string, string> shaderGraphToMaterialXPortMap = null,
            Dictionary<string, MaterialXDataType> shaderGraphToMaterialXPortType = null,
            MaterialXDataType? outputType = null
        )
        {
            string nodeName = ShaderGraphUtil.NodeUtil.GetNodeName(node, context);
            MaterialXDataType outputDataTypeName = outputType ?? TypeUtil.GetMaterialXDataType(ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(node));

            var nodeData = graph.AddNode(nodeName, nodeType, outputDataTypeName);

            var outputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(node);
            stagingEdges.AddPort(outputSlot.slotReference, nodeData.Name);


            var inputSlots = new List<MaterialSlot>();
            node.GetInputSlots<MaterialSlot>(inputSlots);

            if (shaderGraphToMaterialXPortMap != null)
            {
                foreach(var slot in inputSlots)
                {
                    var slotName = slot.RawDisplayName();

                    if (!shaderGraphToMaterialXPortMap.TryGetValue(slotName, out var portName))
                        continue;

                    var portType = TypeUtil.GetMaterialXDataType(slot);
                    if (shaderGraphToMaterialXPortType != null && shaderGraphToMaterialXPortType.TryGetValue(slotName, out var overridePortType))
                        portType = overridePortType;

                    AddInputPortAndEdge(stagingEdges, nodeData, slot, portName, portType);
                }
            }
            return nodeData;
        }
        
        internal static MaterialXNodeData AddUVNode(MaterialXGraphData graph, string name, int uvIndex)
        {
            MaterialXNodeData uvNode;
            if (uvIndex == 0)
            {
                uvNode = graph.AddNode(name, MaterialXNodeType.GeomTexCoord, MaterialXDataType.Vector2);
                uvNode.AddPortWithValue("index", MaterialXDataType.Integer, uvIndex);
            }
            else
            {
                uvNode = graph.AddNode(name, MaterialXNodeType.USDPrimvarReader, MaterialXDataType.Vector2);
                uvNode.AddPortWithStringValue("varname", MaterialXDataType.String, $"vertexUV{uvIndex}");
            }
            return uvNode;
        }
        
        internal static void AddImplicitPropertyFromNode(
            string nodeName, MaterialXDataType dataType, AbstractMaterialNode node, MaterialXGraphData graph,
            StagingEdges stagingEdges, string slotName, MaterialXDataType? swizzleType = null, string channels = null)
        {
            var slot = ShaderGraphUtil.SlotUtil.GetSlotByName(node, slotName);
            if (slot.isConnected)
            {
                InitializeImplicitProperty(nodeName, dataType, graph, node);
                
                string outputNodeName = nodeName;
                 
                if (swizzleType != null && channels != null)
                {
                    outputNodeName = ShaderGraphUtil.NodeUtil.GetNodeName(node, $"{nodeName}{channels}");
                    var swizzleNode = graph.AddNode(outputNodeName, MaterialXNodeType.Swizzle, swizzleType.GetValueOrDefault());
                    swizzleNode.AddPortWithStringValue("channels", MaterialXDataType.String, channels);
                    graph.AddPortAndEdge(nodeName, outputNodeName, "in", dataType);
                }
                stagingEdges?.AddPort(slot.slotReference, outputNodeName);
            }
        }
        
        internal static void InitializeImplicitProperty(string nodeName, MaterialXDataType dataType, MaterialXGraphData graph, AbstractMaterialNode matNode = null)
        {
            if (!graph.HasNode(nodeName))
            {
                MaterialXNodeData newNodeData = graph.AddNode(nodeName, MaterialXNodeType.Constant, dataType, false, true);

                if (dataType.IsString() || dataType.IsFileName())
                {
                    string valueData = "ERR";
                    
                    var textureNode = matNode as Texture2DAssetNode;
                    if (textureNode?.texture != null)
                    {
                        graph.RegisterTexture(textureNode.texture);
                        valueData = ConverterUtil.GetExportedTextureReferencePath(graph.FilePath, textureNode.texture) ?? valueData;
                    }

                    newNodeData.AddPortWithStringValue("value", dataType, valueData);   
                }
                else if (dataType.IsMatrix())
                    newNodeData.AddPortWithValue("value", dataType, new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 });
                else
                    newNodeData.AddPortWithValue("value", dataType, new float[dataType.ChannelCount()]);
            }
        }

        internal static void WriteTextureOutput(Texture texture)
        {
            
        }

        internal static string GetFullTexturePath(Texture texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            return path;
        }
        
        internal static void HandleUVSlot(UVMaterialSlot slot, string name, string dstNodeName, string dstPortName, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            if (!slot.isConnected)
            {
                var index = (int)slot.channel;
                var uvReadNode = AddUVNode(graph, name, index);

                // Flip the V coordinate so downstream UV processing stays in Unity's convention.
                var multiplyNode = graph.AddNode($"{name}Multiply", MaterialXNodeType.Multiply, MaterialXDataType.Vector2);
                graph.AddPortAndEdge(uvReadNode.Name, multiplyNode.Name, "in1", MaterialXDataType.Vector2);
                multiplyNode.AddPortWithValue("in2",  MaterialXDataType.Vector2, new[] { 1.0f, -1.0f });

                var addNode = graph.AddNode($"{name}Add", MaterialXNodeType.Add, MaterialXDataType.Vector2);
                graph.AddPortAndEdge(multiplyNode.Name, addNode.Name, "in1", MaterialXDataType.Vector2);
                addNode.AddPortWithValue("in2",  MaterialXDataType.Vector2, new[] { 0.0f, 1.0f });

                graph.AddPortAndEdge(addNode.Name, dstNodeName, dstPortName, MaterialXDataType.Vector2);
            }
            else
            {
                var dstNode = graph.GetNode(dstNodeName);
                if (!dstNode.HasPort(dstPortName))
                    dstNode.AddPort(dstPortName, MaterialXDataType.Vector2);
                stagingEdges.AddShaderGraphEdgeAndPort(slot, dstNodeName, dstPortName);
            }
        }
    }

    internal static class ShaderGraphUtil
    {
        internal static class SlotUtil
        {
            internal static string GetSlotDefaultFilename(MaterialSlot slot)
            {
                try
                {
                    var texture2DSlot = (Texture2DInputMaterialSlot)slot;
                    return ConverterUtil.GetExportedTextureReferencePath(texture2DSlot.owner.owner.path, texture2DSlot.texture);
                }
                catch
                {
                    return null;
                }
            }
            
            internal static MaterialSlot GetOutputSlot(MaterialSlot slot)
            {
                if (!slot.isConnected)
                    return null;
                return slot.owner.owner.GetEdges(slot.slotReference).First().outputSlot.slot;
            }

            internal static float[] GetSlotDefaultValue(MaterialSlot slot)
            {
                switch (slot)
                {
                    case ColorRGBMaterialSlot colorRgbSlot:
                    {
                        var linearValue = ((Color)(Vector4)colorRgbSlot.value).linear;
                        return new[] { linearValue.r, linearValue.g, linearValue.b };
                    }
                    case ColorRGBAMaterialSlot colorRgbaSlot:
                    {
                        var linearValue = ((Color)colorRgbaSlot.value).linear;
                        return new[] { linearValue.r, linearValue.g, linearValue.b, linearValue.a };
                    }
                    case BooleanMaterialSlot booleanSlot:
                        return new[] { booleanSlot.value ? 1f : 0f };

                    case Vector1MaterialSlot vector1Slot:
                        return new[] { vector1Slot.value };

                    case Vector2MaterialSlot vector2Slot:
                        return new[] { vector2Slot.value.x, vector2Slot.value.y };

                    case Vector3MaterialSlot vector3Slot:
                        return new[] { vector3Slot.value.x, vector3Slot.value.y, vector3Slot.value.z };

                    case DynamicVectorMaterialSlot dynamicVectorSlot:
                        return new[] { dynamicVectorSlot.value.x, dynamicVectorSlot.value.y, dynamicVectorSlot.value.z, dynamicVectorSlot.value.w };

                    case Vector4MaterialSlot vector4Slot:
                        return new[] { vector4Slot.value.x, vector4Slot.value.y, vector4Slot.value.z, vector4Slot.value.w };

                    case Matrix2MaterialSlot matrix2Slot:
                        return TypeUtil.FlattenMatrix2(matrix2Slot.value);

                    case Matrix3MaterialSlot matrix3Slot:
                        return TypeUtil.FlattenMatrix3(matrix3Slot.value);

                    case Matrix4MaterialSlot matrix4Slot:
                        return TypeUtil.FlattenMatrix4(matrix4Slot.value);

                    case DynamicMatrixMaterialSlot dynamicMatrixSlot:
                        return TypeUtil.GetConcreteValue(dynamicMatrixSlot.concreteValueType, dynamicMatrixSlot.value);

                    case DynamicValueMaterialSlot dynamicValueSlot:
                        return TypeUtil.GetConcreteValue(dynamicValueSlot.concreteValueType, dynamicValueSlot.value);

                    default:
                        return null;
                }
            }
            
            internal static MaterialSlot GetPrimaryInputSlot(AbstractMaterialNode node)
            {
                var inputs = new List<MaterialSlot>();
                node.GetInputSlots(inputs);
                return inputs.FirstOrDefault();
            }

            internal static MaterialSlot GetPrimaryOutputSlot(AbstractMaterialNode node)
            {
                var outputs = new List<MaterialSlot>();
                node.GetOutputSlots(outputs);
                return outputs.FirstOrDefault();
            }
            
            internal static MaterialSlot GetSlotByName(
                AbstractMaterialNode node, string rawDisplayName, bool ignoreWhitespace = false)
            {
                var slots = new List<MaterialSlot>();
                node.GetSlots(slots);
                return GetSlotByName(slots, rawDisplayName, ignoreWhitespace);
            }

            static MaterialSlot GetSlotByName(
                List<MaterialSlot> slots, string rawDisplayName, bool ignoreWhitespace = false)
            {
                var displayName = rawDisplayName;
                if (ignoreWhitespace)
                    displayName = ConverterUtil.RemoveWhitespace(displayName);

                foreach (var slot in slots)
                {
                    var slotName = slot.RawDisplayName();
                    if (ignoreWhitespace)
                        slotName = ConverterUtil.RemoveWhitespace(slotName);

                    if (slotName == displayName)
                        return slot;
                }
                return null;
            }

            static Stack<MaterialSlot> s_SlotStack = new Stack<MaterialSlot>();
            internal static ShaderStage GetEffectiveShaderStage(MaterialSlot initialSlot)
            {
                var graph = initialSlot.owner.owner;
                s_SlotStack.Clear();
                s_SlotStack.Push(initialSlot);
                while (s_SlotStack.Any())
                {
                    var slot = s_SlotStack.Pop();
                    ShaderStage stage;
                    if (slot.stageCapability.TryGetShaderStage(out stage))
                        return stage;

                    if (slot.isOutputSlot)
                    {
                        foreach (var edge in graph.GetEdges(slot.slotReference))
                        {
                            var node = edge.inputSlot.node;
                            s_SlotStack.Push(node.FindInputSlot<MaterialSlot>(edge.inputSlot.slotId));
                        }
                    }
                    else
                    {
                        var ownerSlots = Enumerable.Empty<MaterialSlot>();
                        if (slot.isInputSlot)
                            ownerSlots = slot.owner.GetOutputSlots<MaterialSlot>(slot);
                        foreach (var ownerSlot in ownerSlots)
                            s_SlotStack.Push(ownerSlot);
                    }
                }
                return ShaderStage.Fragment;
            }
        }

        internal static class NodeUtil
        {
            private static Dictionary<string, string> specialBaseName = new Dictionary<string, string>()
            {
                {"Reflection", "_realitykit_"},
                {"Step", "_realitykit_"},
                {"View Direction", "_realitykit_"},
            };
            
            internal static string GetNodeName(AbstractMaterialNode node, string context = "")
            {
                if (string.IsNullOrEmpty(context))
                    context = node.name;
                
                string baseName = "Node_";
                if (specialBaseName.ContainsKey(node.name))
                    baseName = specialBaseName[node.name] + baseName;
                
                return $"{ConverterUtil.SanitizeName(context)}{baseName}{node.objectId}";
            }
        }
    }
    
    internal static class TypeUtil
    {
        internal static MaterialXDataType GetMaterialXDataType(MaterialSlot slot)
        {
            return slot switch
            {
                ColorRGBMaterialSlot => MaterialXDataType.Color3,
                ColorRGBAMaterialSlot => MaterialXDataType.Color4,
                _ => slot.concreteValueType switch
                {
                    ConcreteSlotValueType.Boolean => MaterialXDataType.Boolean,
                    ConcreteSlotValueType.Vector1 => MaterialXDataType.Float,
                    ConcreteSlotValueType.Vector2 => MaterialXDataType.Vector2,
                    ConcreteSlotValueType.Vector3 => MaterialXDataType.Vector3,
                    ConcreteSlotValueType.Vector4 => MaterialXDataType.Vector4,
                    ConcreteSlotValueType.Matrix2 => MaterialXDataType.Matrix22,
                    ConcreteSlotValueType.Matrix3 => MaterialXDataType.Matrix33,
                    ConcreteSlotValueType.Matrix4 => MaterialXDataType.Matrix44,
                    ConcreteSlotValueType.Texture2D => MaterialXDataType.Filename,
                    ConcreteSlotValueType.Texture3D => MaterialXDataType.Filename,
                    ConcreteSlotValueType.Cubemap => MaterialXDataType.Filename,
                    ConcreteSlotValueType.Gradient => MaterialXDataType.Color4Array,
                    _ => MaterialXDataType.Unsupported,
                }
            };
        }
        
        internal static float[] GetConcreteValue(ConcreteSlotValueType concreteType, Matrix4x4 matrix)
        {
            return concreteType switch
            {
                ConcreteSlotValueType.Matrix2 => FlattenMatrix2(matrix),
                ConcreteSlotValueType.Matrix3 => FlattenMatrix3(matrix),
                _ => FlattenMatrix4(matrix),
            };
        }
            
        internal static float[] FlattenMatrix2(Matrix4x4 matrix)
        {
            return new[]
            {
                matrix.m00, matrix.m01,
                matrix.m10, matrix.m11,
            };
        }

        internal static float[] FlattenMatrix3(Matrix4x4 matrix)
        {
            return new[]
            {
                matrix.m00, matrix.m01, matrix.m02,
                matrix.m10, matrix.m11, matrix.m12,
                matrix.m20, matrix.m21, matrix.m22,
            };
        }

        internal static float[] FlattenMatrix4(Matrix4x4 matrix)
        {
            return new[]
            {
                matrix.m00, matrix.m01, matrix.m02, matrix.m03,
                matrix.m10, matrix.m11, matrix.m12, matrix.m13,
                matrix.m20, matrix.m21, matrix.m22, matrix.m23,
                matrix.m30, matrix.m31, matrix.m32, matrix.m33,
            };
        }
    }
}
