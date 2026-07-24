using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal static class MaterialXGraphSerializer
    {
        internal static string Serialize(MaterialXGraphData graphData)
        {
            var usdBuilder = new USDBuilder();
            usdBuilder.ProcessGraph(graphData);

            // For consistency, convert to unix-style line endings and paths
            return usdBuilder.GetUSDAString()
                .Replace("\r\n", "\n")
                .Replace("\\", "/");
        }
    }

    internal class USDBuilder
    {
        static readonly int spacesPerIndent = 4;

        // Initial text used for all exported USD files
        static readonly List<string> usdPreface = new()
        {
            @"#usda 1.0",
            @"(",
            @"    metersPerUnit = 1",
            @"    upAxis = ""Y""",
            @")",
            @"",
        };

        static readonly string surfaceShaderOutput = @"token outputs:out";

        static readonly string vertexShaderOutput = @"token outputs:out";
        
        static readonly string geometryModifierNodeLabel = @"ND_realitykit_geometrymodifier_2_0_vertexshader";

        // RAII solution for creating and closing nested USD scopes
        struct USDScope : IDisposable
        {
            USDBuilder _usdBuilder;
            int _indentLevel;

            public int ChildIndentLevel => _indentLevel + 1;

            public USDScope(USDBuilder usdBuilder, string definition, int indentLevel = 0)
            {
                this._usdBuilder = usdBuilder;
                _indentLevel = indentLevel;

                this._usdBuilder.AppendIndentedLine(definition, _indentLevel);
                this._usdBuilder.AppendIndentedLine("{", _indentLevel);
            }

            public USDScope(USDBuilder usdBuilder, List<string> definition, int indentLevel = 0)
            {
                this._usdBuilder = usdBuilder;
                _indentLevel = indentLevel;

                foreach (var definitionLine in definition)
                {
                    this._usdBuilder.AppendIndentedLine(definitionLine, _indentLevel);
                }

                this._usdBuilder.AppendIndentedLine("{", _indentLevel);
            }

            public USDScope AddChildScope(string definition)
            {
                return new USDScope(_usdBuilder, definition, ChildIndentLevel);
            }

            public USDScope AddChildScope(List<string> definition)
            {
                return new USDScope(_usdBuilder, definition, ChildIndentLevel);
            }

            public void Dispose()
            {
                _usdBuilder.AppendIndentedLine("}", _indentLevel);
            }
        }

        StringBuilder _stringBuilder = new();
        MaterialXGraphData _graph;

        // Get the fully converted material as a USD-ascii string
        internal string GetUSDAString()
        {
            return _stringBuilder.ToString();
        }

        // Each shader graph currently needs to be encoded as a separate .usda file
        internal void ProcessGraph(MaterialXGraphData graph)
        {
            _graph = graph;
            _stringBuilder.Clear();

            AppendIndentedLines(usdPreface, 0);

            using (var materialXScope = new USDScope(this, @"def ""MaterialX"""))
            {
                using (var materialsScope = materialXScope.AddChildScope(@"def ""Materials"""))
                {
                    foreach (var node in _graph.Nodes)
                    {
                        if (node.NodeType.Equals(MaterialXNodeType.Material, StringComparison.OrdinalIgnoreCase))
                        {
                            string materialDefinition = $@"def Material ""{node.Name}""";
                            using (var materialScope = materialsScope.AddChildScope(materialDefinition))
                            {
                                ProcessMaterial(materialScope, node);
                            }
                        }
                    }
                }
            }
        }

        // Process the material node, including its properties and all subgraph nodes
        private void ProcessMaterial(USDScope materialScope, MaterialXNodeData materialNode)
        {
            Assert.IsTrue(materialNode.DataType.ToTypeString().Equals("material", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(materialNode.NodeType.Equals(MaterialXNodeType.Material, StringComparison.OrdinalIgnoreCase));

            MaterialXNodeData surfaceShaderNode = _graph.GetConnectedNodeData(materialNode, "surfaceshader");
            _graph.TryGetConnectedNode(materialNode, "vertexshader", out var vertexShaderNode);

            ProcessShaderProperties(materialScope);

            // Connect shader output to material output
            AppendIndentedLine(
                @$"token outputs:mtlx:surface.connect = </MaterialX/Materials/{materialNode.Name}/{surfaceShaderNode.NodeType}.outputs:out>",
                materialScope.ChildIndentLevel);

            if (vertexShaderNode != null)
            {
                AppendIndentedLine(
                    @$"token outputs:realitykit:vertex.connect = </MaterialX/Materials/{materialNode.Name}/GeometryModifier.outputs:out>",
                    materialScope.ChildIndentLevel);
            }

            _stringBuilder.AppendLine("");

            HashSet<MaterialXNodeData> shaderSubgraphRoots = new();
            ProcessShader(
                materialScope, materialNode, surfaceShaderNode, surfaceShaderNode.NodeType,
                @$"uniform token info:id = ""ND_{surfaceShaderNode.NodeType}_surfaceshader""",
                surfaceShaderOutput, shaderSubgraphRoots);
            if (vertexShaderNode != null)
            {
                _stringBuilder.AppendLine("");

                ProcessShader(
                    materialScope, materialNode, vertexShaderNode, "GeometryModifier",
                    $@"uniform token info:id = ""{geometryModifierNodeLabel}""",
                    vertexShaderOutput, shaderSubgraphRoots);
            }

            ProcessShaderSubgraphs(materialScope, materialNode, shaderSubgraphRoots);
        }

        private void ProcessShaderProperties(USDScope materialScope)
        {
            // Add all the custom inputs
            foreach (var input in _graph.InputNodeNames)
            {
                ProcessShaderInput(input, materialScope);
            }

            // TODO(SND-131): Add all the "system" inputs.
            foreach (var input in _graph.SystemInputNodeNames)
            {
                ProcessShaderInput(input, materialScope);
            }

            _stringBuilder.AppendLine("");
        }

        private void ProcessShaderInput(string input, USDScope materialScope)
        {
            var inputNode = _graph.GetNode(input);
            if (inputNode == null)
            {
                Debug.LogWarning($"Missing node for shader input {input}");
                return;
            }

            // Currently, filenames are always textures. The texture may have been specified
            // as "None" at processing time, in which case we will have no ports.
            if (inputNode.DataType.ToTypeString().Equals("filename"))
            {
                AppendIndentedLine(
                    @$"{GetUSDDataTypeString(inputNode.DataType)} inputs:{inputNode.Name} = @{inputNode.GetPort("value").StringValue}@ (colorSpace = ""srgb_texture"")",
                    materialScope.ChildIndentLevel);
                return;
            }

            Assert.AreEqual(inputNode.Ports.Count(), 1);
            foreach (var port in inputNode.Ports)
            {
                AppendPortInput(port, inputNode.Name, materialScope.ChildIndentLevel);
            }
        }

        private void ProcessShader(
            USDScope materialScope, MaterialXNodeData materialNode, MaterialXNodeData shaderNode, string shaderName,
            string shaderNodeInfo, string shaderOutput, HashSet<MaterialXNodeData> rootNodes)
        {
            using (var shaderScope = materialScope.AddChildScope(@$"def Shader ""{shaderName}"""))
            {
                // Add node type info
                AppendIndentedLine(shaderNodeInfo, shaderScope.ChildIndentLevel);

                // Connect inputs, and gather up subgraph roots for further processing
                foreach (var port in shaderNode.Ports)
                {
                    // Both inputs and connected nodes have node representations (inputs as default values).
                    if (_graph.TryGetConnectedNode(shaderNode, port.Name, out var connectedNode))
                    {
                        if (_graph.InputNodeNames.Contains(connectedNode.Name) ||
                            _graph.SystemInputNodeNames.Contains(connectedNode.Name))
                        {
                            // Attach input and/or systemInputs.
                            string nodeConnection =
                                $"{GetUSDDataTypeString(port.DataType)} inputs:{port.Name}.connect = " +
                                $"</MaterialX/Materials/{materialNode.Name}.inputs:{connectedNode.Name}>";
                            AppendIndentedLine(nodeConnection, shaderScope.ChildIndentLevel);
                        }
                        else
                        {
                            // Attach connected node.
                            string nodeConnection =
                                $"{GetUSDDataTypeString(port.DataType)} inputs:{port.Name}.connect = " +
                                $"</MaterialX/Materials/{materialNode.Name}/{connectedNode.Name}" +
                                $".outputs:{connectedNode.OutputName}>";
                            AppendIndentedLine(nodeConnection, shaderScope.ChildIndentLevel);
                            rootNodes.Add(connectedNode);
                        }
                    }
                    else
                    {
                        AppendPortInput(port, port.Name, shaderScope.ChildIndentLevel);
                    }
                }

                // Add output
                AppendIndentedLine(shaderOutput, shaderScope.ChildIndentLevel);
            }
        }

        private void ProcessShaderSubgraphs(
            USDScope materialScope, MaterialXNodeData materialNode, HashSet<MaterialXNodeData> pendingNodes)
        {
            HashSet<MaterialXNodeData> processedNodes = new();
            while (pendingNodes.Count > 0)
            {
                _stringBuilder.AppendLine("");

                var node = pendingNodes.First();
                pendingNodes.Remove(node);
                processedNodes.Add(node);

                using (var nodeScope = materialScope.AddChildScope(@$"def Shader ""{node.Name}"""))
                {
                    string nodeInfoId = GetNodeInfoId(node);
                    AppendIndentedLine($@"uniform token info:id = ""{nodeInfoId}""", nodeScope.ChildIndentLevel);
                    foreach (var port in node.Ports)
                    {
                        // If this port is connected to a node, add that node to the queue for recursive processing
                        if (_graph.TryGetConnectedNode(node, port.Name, out var connectedNode))
                        {
                            if (_graph.InputNodeNames.Contains(connectedNode.Name)
                                || _graph.SystemInputNodeNames.Contains(connectedNode.Name))
                            {
                                string inputParameterConnection =
                                    $"{GetUSDDataTypeString(port.DataType)} inputs:{port.Name}.connect = </MaterialX/Materials/{materialNode.Name}.inputs:{connectedNode.Name}>";
                                AppendIndentedLine(inputParameterConnection, nodeScope.ChildIndentLevel);
                            }
                            else
                            {
                                string nodeConnection =
                                    $"{GetUSDDataTypeString(port.DataType)} inputs:{port.Name}.connect = " +
                                    $"</MaterialX/Materials/{materialNode.Name}/{connectedNode.Name}.outputs:{connectedNode.OutputName}>";
                                AppendIndentedLine(nodeConnection, nodeScope.ChildIndentLevel);

                                if (!processedNodes.Contains(connectedNode))
                                    pendingNodes.Add(connectedNode);
                            }
                        }
                        else if (port.DataType == MaterialXDataType.Filename)
                        {
                            AppendIndentedLine(
                                @$"{GetUSDDataTypeString(port.DataType)} inputs:{port.Name} = @{port.StringValue}@",
                                nodeScope.ChildIndentLevel);
                        }
                        // Strings are supplied via stringData
                        else if (port.DataType == MaterialXDataType.String)
                        {
                            AppendIndentedLine(
                                @$"{GetUSDDataTypeString(port.DataType)} inputs:{port.Name} = ""{port.StringValue}""",
                                nodeScope.ChildIndentLevel);
                        }
                        else
                        {
                            AppendPortInput(port, port.Name, nodeScope.ChildIndentLevel);
                        }
                    }

                    AppendIndentedLine(
                        $@"{GetUSDDataTypeString(node.DataType)} outputs:{node.OutputName}", nodeScope.ChildIndentLevel);
                }
            }
        }

        private void AppendPortInput(MaterialXPortData port, string inputName, int indentLevel)
        {
            int dataLength = port.DataType.ChannelCount();

            // If there's no value, there's *probably* a connection that will supply this
            if (port.ByteValue == null || port.ByteValue.Length == 0)
            {
                AppendIndentedLine(
                    $"{GetUSDDataTypeString(port.DataType)} inputs:{inputName} = {GetUSDDefaultValue(port.DataType)}",
                    indentLevel);
            }
            // Handle scalar numerical or boolean values
            else if (port.Value.Length == 1 || dataLength == 1)
            {
                AppendIndentedLine(
                    $"{GetUSDDataTypeString(port.DataType)} inputs:{inputName} = {port.Value[0]}", indentLevel);
            }
            // Handle vector numerical values
            else
            {
                string indent = new(' ', indentLevel * spacesPerIndent);
                _stringBuilder.Append($"{indent}{GetUSDDataTypeString(port.DataType)} inputs:{inputName} = ");

                var isArray = port.DataType.IsArray();
                _stringBuilder.Append(isArray ? '[' : '(');

                var valueLength = isArray ? port.Value.Length : System.Math.Min(dataLength, port.Value.Length);
                if (valueLength > 0)
                {
                    int elementLength = port.DataType.GetElementLength();
                    AppendValueElement(port, 0, elementLength);

                    int elementCount = valueLength / elementLength;
                    for (int i = 1; i < elementCount; ++i)
                    {
                        _stringBuilder.Append(", ");
                        AppendValueElement(port, i * elementLength, elementLength);
                    }
                }

                _stringBuilder.AppendLine(isArray ? "]" : ")");
            }
        }

        private void AppendValueElement(MaterialXPortData port, int startIndex, int length)
        {
            if (length == 1)
            {
                _stringBuilder.Append($"{port.Value[startIndex]}");
            }
            else
            {
                _stringBuilder.Append($"({port.Value[startIndex]}");
                for (int i = 1; i < length; ++i)
                {
                    _stringBuilder.Append($", {port.Value[startIndex + i]}");
                }

                _stringBuilder.Append(")");
            }
        }

        // Convert from MaterialX type to corresponding USD type
        private string GetUSDDataTypeString(MaterialXDataType datatype)
        {
            switch (datatype)
            {
                case MaterialXDataType.Boolean:
                    return "bool";
                case MaterialXDataType.Integer:
                    return "int";
                case MaterialXDataType.Float:
                    return "float";
                case MaterialXDataType.Color3:
                    return "color3f";
                case MaterialXDataType.Color4:
                    return "color4f";
                case MaterialXDataType.Vector2:
                    return "float2";
                case MaterialXDataType.Vector3:
                    return "float3";
                case MaterialXDataType.Vector4:
                    return "float4";
                case MaterialXDataType.Matrix44:
                    return "matrix4d";
                case MaterialXDataType.Matrix33:
                    return "matrix3d";
                case MaterialXDataType.Matrix22:
                    return "matrix2d";
                case MaterialXDataType.FloatArray:
                    return "float[]";
                case MaterialXDataType.Color4Array:
                    return "color4f[]";
                case MaterialXDataType.Filename:
                    return "asset";
                case MaterialXDataType.String:
                    return "string";
                default:
                    return string.Empty;
            }
        }

        private string GetUSDDefaultValue(MaterialXDataType dataType)
        {
            switch (dataType)
            {
                case MaterialXDataType.Boolean:
                case MaterialXDataType.Integer:
                case MaterialXDataType.Float:
                    return "0";
                case MaterialXDataType.Color3:
                    return "(1, 1, 1)";
                case MaterialXDataType.Color4:
                    return "(1, 1, 1, 1)";
                case MaterialXDataType.Vector2:
                    return "(0, 0)";
                case MaterialXDataType.Vector3:
                    return "(0, 0, 0)";
                case MaterialXDataType.Vector4:
                    return "(0, 0, 0, 0)";
                case MaterialXDataType.Matrix22:
                    return "((1, 0), (0, 1))";
                case MaterialXDataType.Matrix33:
                    return "((1, 0, 0), (0, 1, 0), (0, 0, 1))";
                case MaterialXDataType.Matrix44:
                    return "((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1))";
                default:
                    Debug.LogError($"Can't determine default for data type {dataType}");
                    return "()";
            }
        }

        // USD handles "overloaded" materialX nodes by fully expanding the matrix of options into a set of unique node IDs.
        private string GetNodeInfoId(MaterialXNodeData node)
        {
            // TODO(LWXR-1273): Support other nodes with more complicated input/output mappings. For examples, see:
            // transformmatrix, arrayappend, remap, smoothstep
            switch (node.NodeType)
            {
                case MaterialXNodeType.Swizzle:
                case MaterialXNodeType.Convert:
                    return $"ND_{node.NodeType}_{RequireFirstInputPortType(node)?.ToTypeString()}_{node.DataType.ToTypeString()}";
                
                case MaterialXNodeType.Distance:
                case MaterialXNodeType.DotProduct:
                case MaterialXNodeType.Extract:
                case MaterialXNodeType.Magnitude:
                case MaterialXNodeType.Determinant:
                    return $"ND_{node.NodeType}_{RequireFirstInputPortType(node)?.ToTypeString()}";

                case MaterialXNodeType.Noise2D:
                case MaterialXNodeType.Noise3D:
                case MaterialXNodeType.Fractal3D:
                    return GetFloatVariantNodeInfoId(node, "amplitude");

                case MaterialXNodeType.Mix:
                    return GetFloatVariantNodeInfoId(node, "mix");

                case MaterialXNodeType.Add:
                case MaterialXNodeType.Subtract:
                case MaterialXNodeType.Multiply:
                case MaterialXNodeType.Divide:
                case MaterialXNodeType.Modulo:
                case MaterialXNodeType.Power:
                case MaterialXNodeType.SafePower:
                case MaterialXNodeType.Min:
                case MaterialXNodeType.Max:
                    return GetFloatVariantNodeInfoId(node, "in2");

                case MaterialXNodeType.Arctangent2:
                    return GetFloatVariantNodeInfoId(node, "iny", "inx");

                case MaterialXNodeType.Clamp:
                case MaterialXNodeType.SmoothStep:
                    return GetFloatVariantNodeInfoId(node, "low", "high");

                case MaterialXNodeType.Remap:
                    return GetFloatVariantNodeInfoId(node, "inlow", "inhigh", "outlow", "outhigh");

                case MaterialXNodeType.Contrast:
                    return GetFloatVariantNodeInfoId(node, "amount", "pivot");

                case MaterialXNodeType.Range:
                    return GetFloatVariantNodeInfoId(node, "inlow", "inhigh", "gamma", "outlow", "outhigh");

                case MaterialXNodeType.IfEqual:
                    return GetFirstInputPortType(node, "value") switch
                    {
                        MaterialXDataType.Integer => $"ND_ifequal_{node.DataType.ToTypeString()}I",
                        MaterialXDataType.Boolean => $"ND_ifequal_{node.DataType.ToTypeString()}B",
                        _ => GetDefaultNodeInfoId(node),
                    };
                case MaterialXNodeType.Switch:
                    return (GetFirstInputPortType(node, "which") == MaterialXDataType.Integer)
                        ? $"ND_switch_{node.DataType.ToTypeString()}I"
                        : GetDefaultNodeInfoId(node);

                case MaterialXNodeType.Combine2:
                    if (node.DataType == MaterialXDataType.Color4)
                    {
                        return "ND_combine2_color4CF";
                    }
                    else if (node.DataType == MaterialXDataType.Vector4)
                    {
                        return (RequireFirstInputPortType(node) == MaterialXDataType.Vector3)
                            ? "ND_combine2_vector4VF"
                            : "ND_combine2_vector4VV";
                    }
                    else
                    {
                        return GetDefaultNodeInfoId(node);
                    }
                case MaterialXNodeType.TransformMatrix:
                    return (node.DataType, GetFirstInputPortType(node, "mat")) switch
                    {
                        (MaterialXDataType.Vector2, MaterialXDataType.Matrix33) => "ND_transformmatrix_vector2M3",
                        (MaterialXDataType.Vector3, MaterialXDataType.Matrix44) => "ND_transformmatrix_vector3M4",
                        _ => GetDefaultNodeInfoId(node),
                    };
                default: 
                    // Rules for reality kit built in types
                    switch (node.NodeType)
                    {
                        case MaterialXNodeType.GeomColor:
                            return $"ND_{node.NodeType}_{GetGeomColorNodeInfoSuffix(node.DataType)}";
                        case MaterialXNodeType.GeomViewDirection:
                            return $"ND_realitykit_{node.NodeType}_vector3"; 
                        case MaterialXNodeType.Reflect:
                        case MaterialXNodeType.Step:
                            return $"ND_realitykit_{node.NodeType}_{RequireFirstInputPortType(node)?.ToTypeString()}"; 
                        case MaterialXNodeType.RealityKitSurfaceModelToView:
                        case MaterialXNodeType.RealityKitSurfaceViewToProjection:
                        case MaterialXNodeType.RealityKitSurfaceScreenPosition:
                        case MaterialXNodeType.PicoGeometryModifierModelToView:
                        case MaterialXNodeType.RealityKitGeometryModifierViewToProjection:
                            return $"ND_{node.NodeType}";
                        default:
                            return GetDefaultNodeInfoId(node);
                    }
            }
        }

        private string GetFloatVariantNodeInfoId(MaterialXNodeData node, params string[] inputs)
        {
            if (node.DataType == MaterialXDataType.Float)
                return GetDefaultNodeInfoId(node);

            MaterialXDataType? commonInputType = null;
            foreach (var input in inputs)
            {
                var inputType = GetFirstInputPortType(node, input);
                if (inputType != null)
                {
                    if (commonInputType == null)
                        commonInputType = inputType;
                    else if (commonInputType != inputType)
                        Debug.LogError($"Mismatched input types on node {node.Name} ({commonInputType}/{inputType})");
                }
            }

            return (commonInputType == MaterialXDataType.Float)
                ? $"ND_{node.NodeType}_{node.DataType.ToTypeString()}FA"
                : GetDefaultNodeInfoId(node);
        }

        private string GetDefaultNodeInfoId(MaterialXNodeData node)
        {
            return $"ND_{node.NodeType}_{node.DataType.ToTypeString()}";
        }

        private string GetGeomColorNodeInfoSuffix(MaterialXDataType dataType)
        {
            return dataType switch
            {
                MaterialXDataType.Vector3 => "color3",
                MaterialXDataType.Vector4 => "color4",
                _ => dataType.ToTypeString(),
            };
        }

        private MaterialXDataType? RequireFirstInputPortType(MaterialXNodeData node, string prefix = "in")
        {
            var inputType = GetFirstInputPortType(node, prefix);
            if (inputType != null)
                return inputType;

            Debug.LogError($"Failed to find input node for node {node.Name}");
            return null;
        }

        private MaterialXDataType? GetFirstInputPortType(MaterialXNodeData node, string prefix)
        {
            foreach (var port in node.Ports)
            {
                if (port.Name.StartsWith(prefix))
                    return port.DataType;
            }

            return null;
        }

        private void AppendIndentedLine(string line, int indentLevel = 0)
        {
            string indent = new(' ', indentLevel * spacesPerIndent);
            _stringBuilder.AppendLine($"{indent}{line}");
        }

        private void AppendIndentedLines(List<string> lines, int indentLevel = 0)
        {
            string indent = new(' ', indentLevel * spacesPerIndent);
            foreach (var line in lines)
            {
                _stringBuilder.AppendLine($"{indent}{line}");
            }
        }
    }
}
