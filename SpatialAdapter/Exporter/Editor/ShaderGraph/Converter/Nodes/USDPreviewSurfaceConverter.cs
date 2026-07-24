using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Rendering.BuiltIn.ShaderGraph;
using UnityEditor.ShaderGraph;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal interface ISurfaceConverter
    {
        internal void Convert(GraphData graphData, MaterialXGraphData materialXGraphData, StagingEdges stagingEdges);
    }
    
    public class USDPreviewSurfaceConverter : ISurfaceConverter
    {
        private enum SpecialRules
        {
            OnesComplement,
            DefaultIsTangent,
            WorldToTangent,
            ObjectToTangent,
            EnablesSpecularWorkflow,
            SpecularGrayscale,
            SubtractPosition,
            VertexStage,
            FlipZ,
            AdditiveColor,
            AdditiveAlpha,
            EnableAlphaClip,
        }
        
        private static Dictionary<string, string> _blockMap = null;
        private static Dictionary<string, MaterialXDataType> _typeMap = new();
        private static Dictionary<string, SpecialRules> _rulesMap = new();

        private Dictionary<string, string> BlockMap
        {
            get
            {
                if (_blockMap == null)
                {
                    _blockMap = new();
                    InitializeBlockMap();
                    InitializeSurfaceDescription();
                }

                return _blockMap;
            }
        }
        
        private static Dictionary<string, MaterialXPortData> _litSurfaceInputs = new();
        private static Dictionary<string, MaterialXPortData> _unlitSurfaceInputs = new();
        private static void SetupLitInput(string name, MaterialXDataType type, float[] value)
            => _litSurfaceInputs.Add(name, new MaterialXPortData(name, type, value));
        
        private static void SetupUnlitInput(string name, MaterialXDataType type, float[] value)
            => _unlitSurfaceInputs.Add(name, new MaterialXPortData(name, type, value));

        private void InitializeBlockMap()
        {
            _blockMap.Add("Position", "modelPositionOffset");
            _rulesMap.Add("Position", SpecialRules.SubtractPosition);
            
            // TODO(xutong.zhou): FlipZ for normal and tangent is not necessary for Spatial Engine since we flip during mesh export
            // while for RealityKit, they need this for coordinate system handedness conversion
            _blockMap.Add("Normal", "normal");
            _rulesMap.Add("Normal", SpecialRules.FlipZ);
            _blockMap.Add("Tangent", "bitangent");
            _rulesMap.Add("Tangent", SpecialRules.FlipZ);

            _blockMap.Add("Color", "color");
            _rulesMap.Add("Color", SpecialRules.VertexStage);
            _typeMap.Add("Color", MaterialXDataType.Color4);
            _blockMap.Add("UV0", "uv0");
            _rulesMap.Add("UV0", SpecialRules.VertexStage);
            _typeMap.Add("UV0", MaterialXDataType.Vector2);
            _blockMap.Add("UV1", "uv1");
            _rulesMap.Add("UV1", SpecialRules.VertexStage);
            _typeMap.Add("UV1", MaterialXDataType.Vector2);
            _blockMap.Add("UserAttribute", "userAttribute");
            _rulesMap.Add("UserAttribute", SpecialRules.VertexStage);
            _typeMap.Add("UserAttribute", MaterialXDataType.Vector4);
            
            _blockMap.Add("BaseColor", "baseColor");
            _blockMap.Add("Specular", "specular");
            _rulesMap.Add("Specular", SpecialRules.SpecularGrayscale);
            _blockMap.Add("Occlusion", "ambientOcclusion");
            
            _rulesMap.Add("BaseColor", SpecialRules.AdditiveColor);
            _blockMap.Add("Emission", "emissiveColor");
            _blockMap.Add("Metallic", "metallic");
            _blockMap.Add("NormalTS", "normal");
            _rulesMap.Add("NormalTS", SpecialRules.DefaultIsTangent);
            _blockMap.Add("NormalWS", "normal");
            _rulesMap.Add("NormalWS", SpecialRules.WorldToTangent);
            _blockMap.Add("NormalOS", "normal");
            _rulesMap.Add("NormalOS", SpecialRules.ObjectToTangent);
            _blockMap.Add("Smoothness", "roughness");
            _rulesMap.Add("Smoothness", SpecialRules.OnesComplement);
            
            _blockMap.Add("Alpha", "opacity");
            _rulesMap.Add("Alpha", SpecialRules.AdditiveAlpha);
            _blockMap.Add("AlphaClipThreshold", "opacityThreshold");
            _rulesMap.Add("AlphaClipThreshold", SpecialRules.EnableAlphaClip);
            _blockMap.Add("CoatMask", "clearcoat");
            _blockMap.Add("CoatSmoothness", "clearcoatRoughness");
            _rulesMap.Add("CoatSmoothness", SpecialRules.OnesComplement);
        }

        private void InitializeSurfaceDescription()
        {
            SetupLitInput("baseColor",            MaterialXDataType.Color3,   new float[] { .218f, .218f, .218f });
            SetupLitInput("specular",             MaterialXDataType.Float,    new float[] { .5f });
            SetupLitInput("ambientOcclusion",     MaterialXDataType.Float,    new float[] { 1 });

            SetupLitInput("emissiveColor",        MaterialXDataType.Color3,   new float[] { 0f, 0f, 0f });
            SetupLitInput("metallic",             MaterialXDataType.Float,    new float[] { 0f });
            SetupLitInput("roughness",            MaterialXDataType.Float,    new float[] { 0.5f });
            SetupLitInput("clearcoat",            MaterialXDataType.Float,    new float[] { 0f });
            SetupLitInput("clearcoatRoughness",   MaterialXDataType.Float,    new float[] { 0.01f });
            SetupLitInput("opacity",              MaterialXDataType.Float,    new float[] { 1f });
            SetupLitInput("opacityThreshold",     MaterialXDataType.Float,    new float[] { 0f });
            
            SetupUnlitInput("color",              MaterialXDataType.Color3,   new float[] { .218f, .218f, .218f });
            SetupUnlitInput("opacity",            MaterialXDataType.Float,    new float[] { 1f });
            SetupUnlitInput("opacityThreshold",   MaterialXDataType.Float,    new float[] { 0f });
        }

        void ISurfaceConverter.Convert(GraphData graphData, MaterialXGraphData materialXGraphData, StagingEdges stagingEdges)
        {
            bool alphaClipEnabled = false;
            
            SurfaceType surfaceType = SurfaceType.Opaque;
            AlphaMode alphaMode = AlphaMode.Alpha;
            MaterialType materialType = MaterialType.Lit;
            foreach (var target in graphData.activeTargets)
            {
                string subTargetName = "Lit";
                if (target is BuiltInTarget)
                {
                    var builtInTarget = target as BuiltInTarget;
                    surfaceType = builtInTarget.surfaceType;
                    alphaMode = builtInTarget.alphaMode;
                    alphaClipEnabled = builtInTarget.alphaClip;
                    subTargetName = builtInTarget.activeSubTarget.displayName;
                }
                else
                {
                    var targetType = target.GetType();
                    
                    // Need to use reflection for URP target since the type is not accessible
                    if (targetType.FullName == "UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget")
                    {
                        surfaceType = (SurfaceType) (targetType.GetProperty("surfaceType").GetValue(target));
                        alphaMode = (AlphaMode)(targetType.GetProperty("alphaMode").GetValue(target));
                        alphaClipEnabled = (bool)targetType.GetProperty("alphaClip").GetValue(target);
                        var subTarget = (SubTarget)targetType.GetProperty("activeSubTarget").GetValue(target);
                        subTargetName = subTarget.displayName;
                    } else
                    {
                        Debug.LogWarning($"Unsupported target type {targetType} on shader graph {graphData.path}");
                    }
                }

                switch (subTargetName)
                {
                    case "Lit":
                        materialType = MaterialType.Lit;
                        break;
                    case "Unlit":
                        materialType = MaterialType.UnLit;
                        break;
                    default:
                        Debug.LogWarning($"Unsupported sub target: {subTargetName} on shader graph {graphData.path}");
                        break;
                }
                
                var fragmentNodeType = materialType == MaterialType.UnLit ? MaterialXNodeType.PicoUnlit : MaterialXNodeType.PicoPbr;

                // setup the surface shader node.
                MaterialXDataType vertexShaderDataType = MaterialXDataType.Vertex;
                MaterialXDataType fragmentShaderDataType = MaterialXDataType.Surface;
                var fragmentNodeName = "SR_" + materialXGraphData.Name;
                var vertexNodeName = fragmentNodeName + "_Vertex";
                var materialNodeName = "USD_" + materialXGraphData.Name;
                var vertexNode = materialXGraphData.AddNode(vertexNodeName, MaterialXNodeType.GeometryModification, vertexShaderDataType);
                var fragmentNode = materialXGraphData.AddNode(fragmentNodeName, fragmentNodeType, fragmentShaderDataType);

                // disable tone mapping for unlit surface
                if (fragmentNodeType == MaterialXNodeType.PicoUnlit)
                {
                    fragmentNode.AddPortWithValue("applyPostProcessToneMap", MaterialXDataType.Boolean,  false);
                }
                
                // Account for alpha modes using premultiplied alpha
                if (fragmentNodeType != MaterialXNodeType.USDPreviewSurface && (alphaMode == AlphaMode.Premultiply || alphaMode == AlphaMode.Additive))
                    fragmentNode.AddPortWithValue("hasPremultipliedAlpha", MaterialXDataType.Boolean, true);

                materialXGraphData.AddNode(materialNodeName, MaterialXNodeType.Material, MaterialXDataType.Material);
                
                materialXGraphData.AddPortAndEdge(vertexNodeName, materialNodeName, MaterialXDataType.Vertex.ToTypeString(), MaterialXDataType.Vertex);
                materialXGraphData.AddPortAndEdge(fragmentNodeName, materialNodeName, MaterialXDataType.Surface.ToTypeString(), MaterialXDataType.Surface);

                var blocks = graphData.GetNodes<BlockNode>();

                foreach (var block in blocks)
                {
                    if (!BlockMap.ContainsKey(block.descriptor.name))
                        continue;

                    var shaderNode = block.descriptor.shaderStage switch
                    {
                        ShaderStage.Fragment => fragmentNode,
                        ShaderStage.Vertex => vertexNode,
                        _ => throw new NotSupportedException($"Unsupported shader stage {block.descriptor.shaderStage}")
                    };
                    if (shaderNode == null)
                        continue; // Ignore vertex stage if unsupported.

                    var currentSlot = block.FindInputSlot<MaterialSlot>(0);
                    var srcSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(currentSlot);
                    var portType = TypeUtil.GetMaterialXDataType(currentSlot);
                    var fileValue = ShaderGraphUtil.SlotUtil.GetSlotDefaultFilename(currentSlot);
                    var floatValue = ShaderGraphUtil.SlotUtil.GetSlotDefaultValue(currentSlot);
                    var portName = BlockMap[block.descriptor.name];

                    string externalNodeName = shaderNode.Name;
                    string externalPortName = portName;
                    var externalNode = shaderNode;
                    bool ignoreIfNotConnected = false;

                    if (_rulesMap.TryGetValue(block.descriptor.name, out SpecialRules rule))
                    {
                        switch (rule)
                        {
                            // This special case adds a new port for specular workflow activation, but doesn't otherwise
                            // impact how the blockNode would be processed.
                            case SpecialRules.EnablesSpecularWorkflow:
                                if (block.owner.GetActiveBlocksForAllActiveTargets().Contains(block.descriptor))
                                    shaderNode.AddPortWithValue("useSpecularWorkflow", MaterialXDataType.Integer, 1);
                                break;

                            // Use a dot product node for RGB to grayscale conversion
                            case SpecialRules.SpecularGrayscale:
                                externalNodeName = $"{shaderNode.Name}_{portName}_Grayscale";
                                externalPortName = "in2";
                                externalNode = materialXGraphData.AddNode(
                                    externalNodeName, MaterialXNodeType.DotProduct, MaterialXDataType.Float);

                                // Convert specular color to grayscale according to the Unity reference conversion:
                                // https://docs.unity3d.com/ScriptReference/Color-grayscale.html
                                portType = MaterialXDataType.Vector3;
                                externalNode.AddPortWithValue("in1", portType, new[] { 0.299f, 0.587f, 0.114f });
                                materialXGraphData.AddEdge(externalNodeName, shaderNode.Name, portName);
                                break;

                            case SpecialRules.OnesComplement:
                                externalNodeName = $"{shaderNode.Name}_{portName}_OnesComplement";
                                externalPortName = "in2";
                                externalNode = materialXGraphData.AddNode(externalNodeName, MaterialXNodeType.Subtract,
                                    portType);

                                externalNode.AddPortWithValue("in1", portType, new float[] { 1.0f, 1.0f, 1.0f });
                                materialXGraphData.AddEdge(externalNodeName, shaderNode.Name, portName);
                                break;

                            case SpecialRules.DefaultIsTangent:
                                portType = MaterialXDataType.Vector3;
                                ignoreIfNotConnected = true;
                                break;

                            case SpecialRules.SubtractPosition:
                                if (srcSlot == null)
                                    continue;

                                portType = MaterialXDataType.Vector3;
                                ignoreIfNotConnected = true;

                                // Flip the Z coordinate to convert from Unity to RealityKit space.
                                externalNodeName = $"{vertexNodeName}_{portName}_FlipZ";
                                externalPortName = "in1";
                                externalNode = materialXGraphData.AddNode(externalNodeName, MaterialXNodeType.Multiply,
                                    portType);
                                externalNode.AddPortWithValue("in2", portType, new[] { 1.0f, 1.0f, -1.0f });

                                // Subtract the model space position to get the offset.
                                var subtractNodeName = $"{vertexNodeName}_{portName}_SubtractPosition";
                                materialXGraphData.AddNode(subtractNodeName, MaterialXNodeType.Subtract, portType);
                                materialXGraphData.AddPortAndEdge(externalNodeName, subtractNodeName, "in1", portType);
                                materialXGraphData.AddPortAndEdge(subtractNodeName, vertexNodeName, portName, portType);

                                var positionNodeName = $"{vertexNodeName}_{portName}_Position";
                                var positionNode = materialXGraphData.AddNode(positionNodeName,
                                    MaterialXNodeType.GeomPosition, portType);
                                positionNode.AddPortWithStringValue("space", MaterialXDataType.String, "object");
                                materialXGraphData.AddPortAndEdge(positionNodeName, subtractNodeName, "in2", portType);
                                break;

                            case SpecialRules.FlipZ:
                                if (srcSlot == null)
                                    continue;

                                portType = MaterialXDataType.Vector3;
                                ignoreIfNotConnected = true;

                                // Flip the Z coordinate to convert from Unity to USD space.
                                externalNodeName = $"{vertexNodeName}_{portName}_FlipZ";
                                externalPortName = "in1";
                                externalNode = materialXGraphData.AddNode(externalNodeName, "multiply", portType);
                                materialXGraphData.AddPortAndEdge(externalNodeName, vertexNodeName, portName, portType);
                                externalNode.AddPortWithValue("in2", portType, new[] { 1.0f, 1.0f, -1.0f });
                                break;

                            case SpecialRules.VertexStage:
                                if (!_typeMap.TryGetValue(block.descriptor.name, out portType))
                                    portType = MaterialXDataType.Vector3;
                                break;

                            case SpecialRules.AdditiveColor:
                                if (fragmentNodeType == MaterialXNodeType.PicoUnlit)
                                    externalPortName = portName = "color";

                                if (alphaMode == AlphaMode.Additive)
                                {
                                    // If alpha is additive, premultiply color by alpha.
                                    externalNodeName = $"{fragmentNodeName}_{portName}_MultiplyAlpha";
                                    externalPortName = "in1";
                                    externalNode = materialXGraphData.AddNode(
                                        externalNodeName, MaterialXNodeType.Multiply, MaterialXDataType.Color3);
                                    materialXGraphData.AddPortAndEdge(
                                        externalNodeName, fragmentNodeName, portName, MaterialXDataType.Color3);

                                    var alphaBlock = blocks.FirstOrDefault(b => b.descriptor.name == "Alpha");
                                    if (alphaBlock != null)
                                    {
                                        MaterialXGraphUtil.AddInputPortAndEdge(
                                            stagingEdges, externalNode, alphaBlock.FindInputSlot<MaterialSlot>(0),
                                            "in2", MaterialXDataType.Float);
                                    }
                                }

                                break;

                            case SpecialRules.AdditiveAlpha:
                                if (!(surfaceType == SurfaceType.Transparent || alphaClipEnabled))
                                {
                                    // If opaque and alpha clipping is disabled, omit opacity entirely.
                                    srcSlot = null;
                                    ignoreIfNotConnected = true;
                                }
                                else if (alphaMode == AlphaMode.Additive)
                                {
                                    // If alpha is additive, opacity should be zero (because the
                                    // destination color will be multiplied by (1 - alpha)).
                                    srcSlot = null;
                                    floatValue = new[] { 0.0f };
                                }

                                break;

                            case SpecialRules.EnableAlphaClip:
                                if (!alphaClipEnabled)
                                {
                                    // If alpha clip is not enabled, omit opacity threshold entirely.
                                    srcSlot = null;
                                    ignoreIfNotConnected = true;
                                }

                                break;
                        }
                    }


                    if (srcSlot != null && ConverterLookup.NodeCanConvert(srcSlot.owner))
                    {
                        externalNode.AddPort(externalPortName, portType);
                        stagingEdges.AddPort(currentSlot.slotReference, externalNodeName, externalPortName);
                        stagingEdges.AddShaderGraphEdge(srcSlot.slotReference, currentSlot.slotReference);
                    }
                    else if (srcSlot == null && portType == MaterialXDataType.Filename && !ignoreIfNotConnected)
                    {
                        externalNode.AddPortWithStringValue(externalPortName, portType, fileValue);
                    }
                    else if (srcSlot == null && !ignoreIfNotConnected)
                    {
                        externalNode.AddPortWithValue(externalPortName, portType, floatValue);
                    }
                }

                // resolve surface inputs without default connection with a default value
                var surfaceInputs = materialType == MaterialType.UnLit ? _unlitSurfaceInputs : _litSurfaceInputs;
                foreach (var defaultPorts in surfaceInputs.Values)
                {
                    if (!fragmentNode.HasPort(defaultPorts.Name))
                    {
                        // if alpha clip is disabled, skip opacity threshold port
                        // if alpha clip is disabled and material is not transparent, skip opacity port
                        if (defaultPorts.Name == "opacityThreshold" && !alphaClipEnabled)
                        {
                            continue;
                        }
                        
                        if (defaultPorts.Name == "opacity" && !(surfaceType == SurfaceType.Transparent || alphaClipEnabled))
                            continue;
    
                        fragmentNode.AddPortWithValue(defaultPorts.Name, defaultPorts.DataType, defaultPorts.Value);
                    }
                }
            }
        }
    }
}