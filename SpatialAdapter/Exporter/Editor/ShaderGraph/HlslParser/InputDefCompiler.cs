using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    class NodeDefCompiler
    {
        static MaterialXDataType ConvertToMatchedType(
                SyntaxNode node, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output,
                Dictionary<string, InputNodeDef> inputDefs, params string[] inputNames)
        {
            var maxLength = 1;
            var maxElementLength = 1;
            foreach (var inputName in inputNames)
            {
                var inputDef = inputDefs[inputName];
                var inputType = GetOutputType(inputDef, inputs, output);
                maxLength = System.Math.Max(maxLength, MaterialXDataTypeExtensions.GetLength(inputType));
                maxElementLength = System.Math.Max(maxElementLength, MaterialXDataTypeExtensions.GetElementLength(inputType));
            }

            var matchedType = maxElementLength switch
            {
                2 => MaterialXDataType.Matrix22,
                3 => MaterialXDataType.Matrix33,
                4 => MaterialXDataType.Matrix44,
                _ => MaterialXDataTypeExtensions.GetTypeOfLength(maxLength),
            };

            foreach (var inputName in inputNames)
            {
                ConvertToType(node, inputs, output, inputDefs, inputName, matchedType);
            }

            return matchedType;
        }

        static void ConvertToType(
            SyntaxNode node, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output,
            Dictionary<string, InputNodeDef> inputDefs, string inputName, MaterialXDataType expectedType)
        {
            var inputDef = inputDefs[inputName];
            if (!TryConvert(ref inputDef, inputs, output, expectedType))
            {
                var inputType = GetOutputType(inputDef, inputs, output);
                throw new Exception($"Mismatched argument type ({inputType} vs. {expectedType})");
            }
            inputDefs[inputName] = inputDef;
        }

        static InputNodeDef GetSharedInput(InputNodeDef inputDef, Dictionary<string, NodeDef> output)
        {
            if (inputDef is not InlineInputNodeDef inlineInputDef)
                return inputDef;
            
            var temporaryName = $"__Tmp{output.Count}";
            output.Add(temporaryName, inlineInputDef.Source);
            return new InternalInputNodeDef(temporaryName);
        }
        static bool TryConvert(
            ref InputNodeDef inputDef, Dictionary<string, MaterialXDataType> inputs,
            Dictionary<string, NodeDef> output, MaterialXDataType expectedType)
        {
            var outputType = GetOutputType(inputDef, inputs, output);
            if (outputType == expectedType)
            {
                return true;
            }

            var outputLength = MaterialXDataTypeExtensions.GetLength(outputType);
            var expectedLength = MaterialXDataTypeExtensions.GetLength(expectedType);
            if (inputDef is FloatInputNodeDef floatInputDef)
            {
                var newValues = new float[expectedLength];
                if (outputLength == 1)
                    Array.Fill(newValues, floatInputDef.Values[0]);
                else
                    Array.Copy(floatInputDef.Values, newValues, System.Math.Min(outputLength, expectedLength));
                inputDef = new FloatInputNodeDef(expectedType, newValues);
                return true;
            }
            
            if (outputLength == 1 || outputLength == expectedLength)
            {
                inputDef = new InlineInputNodeDef(MaterialXNodeType.Convert, expectedType, new()
                {
                    ["in"] = inputDef,
                });
                return true;
            }
            
            return false;
        }
        static MaterialXDataType GetOutputType(InputNodeDef inputDef, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output)
        {
            return inputDef switch
            {
                FloatInputNodeDef floatInputDef => floatInputDef.PortType,
                InternalInputNodeDef internalInputDef => output[internalInputDef.Source].OutputType,
                ExternalInputNodeDef externalInputDef => inputs[externalInputDef.Source],
                InlineInputNodeDef inlineInputDef => inlineInputDef.Source.OutputType,
                _ => MaterialXDataType.String,
            };
        }
        delegate InputNodeDef Compiler(
             SyntaxNode node, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output);

        static Dictionary<(string, Operator.VariantType), Compiler> OperatorCompilers = new()
        {
            [(";", Operator.VariantType.Default)] = SemicolonCompiler,
            [("=", Operator.VariantType.Default)] = AssignmentCompiler,
            [("+", Operator.VariantType.Default)] = BuildBinaryOperatorCompiler(MaterialXNodeType.Add, true),
            [("-", Operator.VariantType.Default)] = BuildBinaryOperatorCompiler(MaterialXNodeType.Subtract, true),
            [("*", Operator.VariantType.Default)] = BuildBinaryOperatorCompiler(MaterialXNodeType.Multiply, true),
            [(".", Operator.VariantType.Default)] = SwizzleCompiler,
            [("/", Operator.VariantType.Default)] = BuildBinaryOperatorCompiler(MaterialXNodeType.Divide, true),

            // POW, saturate, dot, normailize
            [("pow", Operator.VariantType.FunctionCall)] = BuildBinaryOperatorCompiler(MaterialXNodeType.Power, true),
            [("saturate", Operator.VariantType.FunctionCall)] = BuildUnaryOperatorCompiler(MaterialXNodeType.Clamp),
            [("dot", Operator.VariantType.FunctionCall)] = BuildBinaryOperatorCompiler(MaterialXNodeType.DotProduct, true, MaterialXDataType.Float),
            [("normalize", Operator.VariantType.FunctionCall)] = BuildUnaryOperatorCompiler(MaterialXNodeType.Normalize),
            [("floor", Operator.VariantType.FunctionCall)] = BuildUnaryOperatorCompiler(MaterialXNodeType.Floor),
            [("max", Operator.VariantType.FunctionCall)] = BuildBinaryOperatorCompiler(MaterialXNodeType.Max, true),

            [("float3", Operator.VariantType.FunctionCall)] = BuildConstructorCompiler(MaterialXDataType.Vector3),
            [("smoothstep", Operator.VariantType.FunctionCall)] = BuildNaryOperatorCompiler(MaterialXNodeType.SmoothStep, "low", "high", "in"),
            [("sin", Operator.VariantType.FunctionCall)] = BuildUnaryOperatorCompiler(MaterialXNodeType.Sine),
            [("reflect", Operator.VariantType.FunctionCall)] = BuildNaryOperatorCompiler( MaterialXNodeType.RealityKitReflect, "in", "normal"),
            
            [("distance", Operator.VariantType.FunctionCall)] = BuildBinaryOperatorCompiler(MaterialXNodeType.Distance, true, MaterialXDataType.Float),
        };

        static InputNodeDef SemicolonCompiler(
            SyntaxNode node, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output)
        {
            node.Children.ForEach(child => child.Compile(inputs, output));
            return null;
        }

        static InputNodeDef AssignmentCompiler(
            SyntaxNode node, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output)
        {
            InputNodeDef inputDef = node.Children[1].Compile(inputs, output);

            string symbol;
            var leftChild = node.Children[0];
            if (leftChild.Token is Symbol)
            {
                symbol = leftChild.Token.Content;
            }
            else if (leftChild.Token is Operator op &&
                op.Variant == Operator.VariantType.VariableDefinition &&
                leftChild.Children.Count == 1 &&
                leftChild.Children[0].Token is Symbol leftGrandchildSymbol)
            {
                symbol = leftGrandchildSymbol.Content;
                
                var expectedType = MaterialXDataTypeExtensions.GetTypeForHlsl(op.Content);
                if (expectedType == MaterialXDataType.Unsupported)
                    throw new System.Exception($"Unknown type {op.Content}");
                
                // Ensure the variable being assigned has the same type as the expected value 
                if (!TryConvert(ref inputDef, inputs, output, expectedType))
                    throw new Exception($"Expected {op.Content} rvalue"); 
            }
            else
            {
                throw new Exception($"Invalid lvalue for assignment {node.Token.Content}");
            }

            switch (inputDef)
            {
                case InlineInputNodeDef inlineInputDef:
                    output[symbol] = inlineInputDef.Source;
                    break;

                default:
                    output[symbol] =
                    new( MaterialXNodeType.Dot, GetOutputType(inputDef, inputs, output), "Out",
                        new()
                        {
                            ["in"] = inputDef,
                        });
                    break;
            }

            return new InternalInputNodeDef(symbol);
        }
        static InputNodeDef SwizzleCompiler(SyntaxNode node, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output)
        {
            var leftInputDef = node.Children[0].Compile(inputs, output);
            var leftInputType = GetOutputType(leftInputDef, inputs, output);

            var leftLength = MaterialXDataTypeExtensions.GetLength(leftInputType);
            if (leftLength > 4)
                throw new Exception("Left side of . cannot be swizzled");
            if (node.Children[1].Token is not Symbol swizzle)
                throw new Exception("Right side of . is not a swizzle");

            var containsXYZW = false;
            var containsRGBA = false;
            var containsOther = false;
            foreach (var ch in swizzle.Content)
            {
                if ("xyzw".Contains(ch))
                    containsXYZW = true;
                else if ("rgba".Contains(ch))
                    containsRGBA = true;
                else
                    containsOther = true;
            }
            if (!(containsXYZW ^ containsRGBA) || containsOther || swizzle.Content.Length > 4)
                throw new Exception("Invalid swizzle");

            var outputType = MaterialXDataTypeExtensions.GetTypeOfLength(swizzle.Content.Length);

            var channels = swizzle.Content;
            if (containsRGBA)
            {
                System.Text.StringBuilder builder = new();
                foreach (var ch in channels)
                {
                    builder.Append(ch switch
                    {
                        'r' => 'x',
                        'g' => 'y',
                        'b' => 'z',
                        _ => 'w',
                    });
                }
                channels = builder.ToString();
            }

            return new InlineInputNodeDef(MaterialXNodeType.Swizzle, outputType, new()
            {
                ["in"] = leftInputDef,
                ["channels"] = new StringInputNodeDef(channels),
            });
        }


        static InputNodeDef BuildVectorConstructor(
                    Dictionary<string, NodeDef> output, MaterialXDataType dataType, List<InputNodeDef> inputDefs, List<MaterialXDataType> outputTypes)
        {
            Assert.IsTrue(MaterialXDataTypeExtensions.IsVector(dataType));
            var dataTypeLength = MaterialXDataTypeExtensions.GetLength(dataType);

            Dictionary<string, InputNodeDef> inputDefsMap = new();
            var inIndex = 1;

            void AddScalar(InputNodeDef inputDef)
            {
                Assert.IsTrue(inIndex <= dataTypeLength);
                inputDefsMap[$"in{inIndex++}"] = inputDef;
            }

            for (var i = 0; i < inputDefs.Count; ++i)
            {
                var inputLength = MaterialXDataTypeExtensions.GetLength(outputTypes[i]);
                Assert.AreNotEqual(inputLength, 0);
                if (inputLength == 1)
                {
                    AddScalar(inputDefs[i]);
                    continue;
                }
                var inputDef = inputDefs[i];
                if (inputDef is FloatInputNodeDef floatInputDef)
                {
                    foreach (var value in floatInputDef.Values)
                    {
                        AddScalar(new FloatInputNodeDef(MaterialXDataType.Float, value));
                    }
                }
                else
                {
                    var sharedInput = GetSharedInput(inputDef, output);
                    for (var j = 0; j < inputLength; ++j)
                    {
                        AddScalar(new InlineInputNodeDef(MaterialXNodeType.Swizzle, MaterialXDataType.Float, new()
                        {
                            ["in"] = sharedInput,
                            ["channels"] = new StringInputNodeDef("xyzw".Substring(j, 1)),
                        }));
                    }
                }
            }

            return new InlineInputNodeDef($"combine{dataTypeLength}", dataType, inputDefsMap);
        }
        static Compiler BuildConstructorCompiler(MaterialXDataType fixedDataType = MaterialXDataType.Unsupported)
        {
            return (node, inputs, output) =>
            {
                List<InputNodeDef> inputDefs = new();
                List<MaterialXDataType> outputTypes = new();
                var allFloatInputDefs = true;
                foreach (var child in node.Children)
                {
                    var inputDef = child.Compile(inputs, output);
                    if (inputDef is not FloatInputNodeDef)
                        allFloatInputDefs = false;

                    inputDefs.Add(inputDef);
                    outputTypes.Add(GetOutputType(inputDef, inputs, output));
                }
                var totalLength = outputTypes.Select(MaterialXDataTypeExtensions.GetLength).Sum();
                MaterialXDataType dataType;
                if (fixedDataType == null)
                {
                    dataType = MaterialXDataTypeExtensions.GetTypeOfLength(totalLength);
                    if (dataType == null)
                        throw new Exception($"No type known of length {totalLength}");
                }
                else
                {
                    var expectedLength = MaterialXDataTypeExtensions.GetLength(fixedDataType);
                    if (totalLength != expectedLength)
                    {
                        throw new Exception($"Expected {expectedLength} components, found {totalLength}");
                    }
                    dataType = fixedDataType;
                }

                if (inputDefs.Count == 1)
                {
                    return inputDefs[0];
                }
                else if (allFloatInputDefs)
                {
                    var values = inputDefs.SelectMany(inputDef => ((FloatInputNodeDef)inputDef).Values).ToArray();
                    return new FloatInputNodeDef(dataType, values);
                }

                if (MaterialXDataTypeExtensions.IsVector(dataType))
                    return BuildVectorConstructor(output, dataType, inputDefs, outputTypes);
                else
                    throw new Exception($"Cannot construct type {dataType}");
            };
        }
        static Compiler BuildUnaryOperatorCompiler(string nodeType, string inputPort = "in", MaterialXDataType outputType = MaterialXDataType.Unsupported)
        {
            return (node, inputs, output) =>
            {
                Dictionary<string, InputNodeDef> inputDefs = new()
                {
                    [inputPort] = node.Children[0].Compile(inputs, output),
                };
                var matchedType = ConvertToMatchedType(node, inputs, output, inputDefs, inputPort);

                return new InlineInputNodeDef(nodeType, outputType == MaterialXDataType.Unsupported ? matchedType : outputType , inputDefs);
            };
        }

        static Compiler BuildBinaryOperatorCompiler(string nodeType, bool allowFloatRight = false, MaterialXDataType outputType = MaterialXDataType.Unsupported)
        {
            return (node, inputs, output) =>
            {
                Dictionary<string, InputNodeDef> inputDefs = new()
                {
                    ["in1"] = node.Children[0].Compile(inputs, output),
                    ["in2"] = node.Children[1].Compile(inputs, output),
                };
        
                // We allow the right hand side to be a float for the FA node variants, like vector * scalar.
                MaterialXDataType matchedType;
                if (allowFloatRight && GetOutputType(inputDefs["in2"], inputs, output) == MaterialXDataType.Float)
                    matchedType = ConvertToMatchedType(node, inputs, output, inputDefs, "in1");
                else
                    matchedType = ConvertToMatchedType(node, inputs, output, inputDefs, "in1", "in2");
                    
                return new InlineInputNodeDef(nodeType, outputType == MaterialXDataType.Unsupported ? matchedType : outputType, inputDefs);
            };
        }
        static Compiler BuildNaryOperatorCompiler(string nodeType, params string[] inputPorts)
        {
            return (node, inputs, output) =>
            {
                Dictionary<string, InputNodeDef> inputDefs = new();
                for (var i = 0; i < inputPorts.Length; ++i)
                {
                    inputDefs.Add(inputPorts[i], node.Children[i].Compile(inputs, output));
                }
                var outputType = ConvertToMatchedType(node, inputs, output, inputDefs, inputPorts);

                return new InlineInputNodeDef(nodeType, outputType, inputDefs);
            };
        }
        internal static InputNodeDef CompileSymbol(
             SyntaxNode node, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output)
        {
            var symbol = node.Token as Symbol;
            if (inputs.ContainsKey(symbol.Content))
                return new ExternalInputNodeDef(symbol.Content);

            if (output.ContainsKey(symbol.Content))
                return new InternalInputNodeDef(symbol.Content);

            // TODO: Handle other HLSL built-in symbol types
            throw new System.Exception($"Unknown symbol {symbol.Content}");
        }

        internal static InputNodeDef CompileOperator(
             SyntaxNode node, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output)
        {
            var op = node.Token as Operator;
            if (OperatorCompilers.TryGetValue((op.Content, op.Variant), out var compiler))
                return compiler(node, inputs, output);

            throw new System.Exception($"Unknown operator {op.Content}");
        }
    }
}
