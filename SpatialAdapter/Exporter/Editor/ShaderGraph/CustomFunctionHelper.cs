using System;
using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

internal class CompoundContext
{
    internal AbstractMaterialNode Node { get; private set; }
    internal MaterialXGraphData MaterialXGraphData { get; private set; }
    internal StagingEdges StagingEdges{get; private set;}
    internal string Context { get; private set; }
    internal Dictionary<string, NodeDef> NodeDefs { get; private set; }
    internal Dictionary<string, MaterialXNodeData> NodeData {get; private set;} = new();
    internal Dictionary<string, MaterialXNodeData> StagingDotNodes { get; private set; } = new();

    internal CompoundContext(AbstractMaterialNode node, MaterialXGraphData graph, StagingEdges stagingEdges, string context, Dictionary<string, NodeDef> nodeDefs)
    {
        Node = node;
        MaterialXGraphData = graph;
        StagingEdges = stagingEdges;
        Context = context;
        NodeDefs = nodeDefs;
    }
}
internal abstract class AbstractNodeDef
{
}

internal abstract class InputNodeDef : AbstractNodeDef
{
    internal abstract void AddPortsAndEdges(CompoundContext compoundContext, MaterialXNodeData nodeData, string nodeKey, string inputKey);
}

internal class FloatInputNodeDef : InputNodeDef
{
    internal MaterialXDataType PortType { get; private set; }
    internal float[] Values { get; private set; }

    internal FloatInputNodeDef(MaterialXDataType portType, params float[] values)
    {
        PortType = portType;
        Values = values;
    }

    internal override void AddPortsAndEdges(CompoundContext context, MaterialXNodeData nodeData, string nodeKey, string inputKey)
    {
        nodeData.AddPortWithValue(inputKey, PortType, Values);
    }
}

internal class ExternalInputNodeDef : InputNodeDef
{
    internal string Source {get; private set;}
    internal ExternalInputNodeDef(string source)
    {
        Source = source;
    }
    internal override void AddPortsAndEdges(CompoundContext context, MaterialXNodeData nodeData, string nodeKey, string inputKey)
    {
        if (!context.StagingDotNodes.TryGetValue(Source, out var dotNode))
        {
            var slot = ShaderGraphUtil.SlotUtil.GetSlotByName(context.Node, Source, true);
            var materialXDataType = TypeUtil.GetMaterialXDataType(slot);

            var dotNodeType = MaterialXNodeType.Dot;
            var dotNodeInput = "in";

            dotNode = context.MaterialXGraphData.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(context.Node, $"{context.Context}_{Source}"),
                 dotNodeType, materialXDataType);
            context.StagingDotNodes.Add(Source, dotNode);

            if (slot.isConnected)
            {
                MaterialXGraphUtil.AddInputPortAndEdge(context.StagingEdges, dotNode, slot, dotNodeInput, materialXDataType);
            }
            else
            {
                switch (slot)
                {
                    case ViewDirectionMaterialSlot:
                        {
                            var geomNode = context.MaterialXGraphData.AddNode(
                                $"{dotNode.Name}Geom", MaterialXNodeType.RealityKitViewDirection, MaterialXDataType.Vector3);
                            var flipNode = context.MaterialXGraphData.AddNode(
                                $"{dotNode.Name}Flip", MaterialXNodeType.Multiply, MaterialXDataType.Vector3);
                            context.MaterialXGraphData.AddPortAndEdge(geomNode.Name, flipNode.Name, "in1", MaterialXDataType.Vector3);
                            flipNode.AddPortWithValue("in2", MaterialXDataType.Vector3, new[] { 1.0f, 1.0f, -1.0f });
                            context.MaterialXGraphData.AddPortAndEdge(flipNode.Name, dotNode.Name, dotNodeInput, MaterialXDataType.Vector3);
                            break;
                        }
                    case NormalMaterialSlot normalSlot and { space: CoordinateSpace.Tangent }:
                        dotNode.AddPortWithValue(dotNodeInput, MaterialXDataType.Vector3, new[] { 0.0f, 0.0f, 1.0f });
                        break;
                    case SpaceMaterialSlot spaceMaterialSlot:
                        {
                            var nodeType = slot switch
                            {
                                TangentMaterialSlot => MaterialXNodeType.GeomTangent,
                                BitangentMaterialSlot => MaterialXNodeType.GeomBitangent,
                                NormalMaterialSlot => MaterialXNodeType.GeomNormal,
                                PositionMaterialSlot => MaterialXNodeType.GeomPosition,
                                _ => throw new NotSupportedException($"Unsupported slot type {slot.GetType()}"),
                            };
                            var geomNode = context.MaterialXGraphData.AddNode($"{dotNode.Name}Geom", nodeType, MaterialXDataType.Vector3);
                            var space = spaceMaterialSlot.space switch
                            {
                                CoordinateSpace.Object => "object",
                                CoordinateSpace.World => "world",
                                CoordinateSpace.Tangent => "tangent",
                                _ => throw new NotSupportedException($"Unsupported space {spaceMaterialSlot.space}"),
                            };
                            geomNode.AddPortWithStringValue("space", MaterialXDataType.String, space);

                            var flipNode = context.MaterialXGraphData.AddNode(
                                $"{dotNode.Name}Flip", MaterialXNodeType.Multiply, MaterialXDataType.Vector3);
                            context.MaterialXGraphData.AddPortAndEdge(geomNode.Name, flipNode.Name, "in1", MaterialXDataType.Vector3);
                            flipNode.AddPortWithValue("in2", MaterialXDataType.Vector3, new[] { 1.0f, 1.0f, -1.0f });

                            context.MaterialXGraphData.AddPortAndEdge(flipNode.Name, dotNode.Name, dotNodeInput, MaterialXDataType.Vector3);
                            break;
                        }

                    default:
                        MaterialXGraphUtil.AddInputPortAndEdge(context.StagingEdges, dotNode, slot, dotNodeInput, materialXDataType);
                        break;
                }
            }
        }
        context.MaterialXGraphData.AddPortAndEdge(dotNode.Name, nodeData.Name, inputKey, dotNode.DataType);
    }
}

internal class InternalInputNodeDef : InputNodeDef
{
    internal string Source { get; private set; }
    internal InternalInputNodeDef(string source)
    {
        Source = source;
    }
    internal override void AddPortsAndEdges(CompoundContext context, MaterialXNodeData nodeData, string nodeKey, string inputKey)
    {
        var sourceNode = context.NodeDefs[Source].AddNodesAndEdges(context, Source);
        context.MaterialXGraphData.AddPortAndEdge(sourceNode.Name, nodeData.Name, inputKey, sourceNode.DataType);
    }
}

internal class InlineInputNodeDef : InputNodeDef
{
    internal NodeDef Source { get; private set;}
    internal InlineInputNodeDef(string nodeType, MaterialXDataType outputType, Dictionary<string, InputNodeDef> inputs, string outputName = "out" )
    {
        Source = new NodeDef(nodeType, outputType, outputName, inputs);
    }
    internal InlineInputNodeDef(NodeDef source)
    {
        Source = source;
    }

    internal override void AddPortsAndEdges(CompoundContext context, MaterialXNodeData nodeData, string nodeKey, string inputKey)
    {
        var sourceNode = Source.AddNodesAndEdges(context, $"{nodeKey}_{inputKey}");
        context.MaterialXGraphData.AddPortAndEdge(sourceNode.Name, nodeData.Name, inputKey, sourceNode.DataType);
    }
}

internal class StringInputNodeDef : InputNodeDef
{
    internal string Value { get; private set; }

    internal StringInputNodeDef(string value)
    {
        Value = value;
    }

    internal override void AddPortsAndEdges(CompoundContext compoundContext, MaterialXNodeData nodeData, string nodeKey, string inputKey)
    {
        nodeData.AddPortWithStringValue(inputKey, MaterialXDataType.String, Value);
    }
}

internal class FragmentInputNodeDef : InputNodeDef
{
    internal override void AddPortsAndEdges(CompoundContext context, MaterialXNodeData nodeData, string nodeKey, string inputKey)
    {

    }
}

internal class PerStageInputNodeDef : InputNodeDef
{
    internal InputNodeDef Vertex { get; private set; }
    internal InputNodeDef Fragment { get; private set; }
    internal PerStageInputNodeDef(InputNodeDef vertex, InputNodeDef fragment)
    {
        Vertex = vertex;
        Fragment = fragment;
    }

    internal override void AddPortsAndEdges(CompoundContext context, MaterialXNodeData nodeData, string nodeKey, string inputKey)
    {
        var outputs = new List<MaterialSlot>();
        context.Node.GetOutputSlots(outputs);
        foreach (var output in outputs)
        {
            if (ShaderGraphUtil.SlotUtil.GetEffectiveShaderStage(output) == ShaderStage.Vertex)
            {
                Vertex.AddPortsAndEdges(context, nodeData, nodeKey, inputKey);
                return;
            }
        }
        Fragment.AddPortsAndEdges(context, nodeData, nodeKey, inputKey);
    }
}


internal class NodeDef : AbstractNodeDef
{
    internal string NodeType;
    internal MaterialXDataType OutputType;
    internal string OutputName;
    Dictionary<string, InputNodeDef> Inputs;

    internal NodeDef(string nodeType, MaterialXDataType outputType, string outputName, Dictionary<string, InputNodeDef> inputs)
    {
        NodeType = nodeType;
        OutputType = outputType;
        OutputName = outputName;
        Inputs = inputs;
    }

    internal MaterialXNodeData AddNodesAndEdges(CompoundContext context, string key)
    {
        if (!context.NodeData.TryGetValue(key, out var nodeDatum))
        {
            nodeDatum = context.MaterialXGraphData.AddNode(ShaderGraphUtil.NodeUtil.GetNodeName(context.Node, $"{context.Context}_{key}"), NodeType, OutputType);
            nodeDatum.OutputName = OutputName;
            context.NodeData.Add(key, nodeDatum);


            var outputSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(context.Node, key);
            if (outputSlot != null)
            {
                outputSlot = ShaderGraphUtil.SlotUtil.GetOutputSlot(outputSlot);
                if (outputSlot != null)
                    context.StagingEdges.AddPort(outputSlot.slotReference, nodeDatum.Name);
            }

            foreach (var (inputName, inputDef) in Inputs)
            {
                inputDef.AddPortsAndEdges(context, nodeDatum, key, inputName);
            }
        }
        return nodeDatum;
    }
}

internal static class CustomFunctionHelper
{
    internal static void CombineNodeDefs(CompoundContext context, Dictionary<String, NodeDef> nodeDefs, List<MaterialSlot> outputSlots)
    {
        foreach (var outputSlot in outputSlots)
        {
            if (nodeDefs.TryGetValue(outputSlot.RawDisplayName(), out var outputNodeDefinition))
            {
                outputNodeDefinition.AddNodesAndEdges(context, outputSlot.RawDisplayName());
            }
        }
    }

    internal static Dictionary<String, NodeDef> ParseHlsl(AbstractMaterialNode node, String hlsl)
    {
        var inputSlots = new List<MaterialSlot>();
        node.GetInputSlots(inputSlots);
        Dictionary<string, MaterialXDataType> inputNamesTypes = new();

        foreach (var inputSlot in inputSlots)
        {
            var inputName = inputSlot.RawDisplayName().Replace(" ", "");
            inputNamesTypes.Add(inputName, TypeUtil.GetMaterialXDataType(inputSlot));
        }
        
        return ParseHlsl(inputNamesTypes, hlsl);
    }

    private static void PopOperator(Stack<Operator> operators, List<SyntaxNode> operands)
    {
      var op = operators.Pop();
      var arity = op.GetArity(operands.Count);
      if (operands.Count < arity)
          throw new Exception("Not enough operands");

      var node = new SyntaxNode(op, operands.GetRange(operands.Count - arity, arity));
      operands.RemoveRange(operands.Count - arity, arity);
      operands.Add(node);
    }

    internal static Dictionary<String, NodeDef> ParseHlsl(Dictionary<string, MaterialXDataType> inputNamesTypes, String hlsl)
    {
        Stack<Operator> operatorStack = new();
        List<SyntaxNode> operands = new();
        var tokenizer = new Tokenizer(hlsl);
        Token lastToken = null;
        Token nextToken = null;
        Token token = null;
        while(true)
        {
            if (nextToken == null)
            {
                lastToken = token;
                token = tokenizer.GetNextToken(lastToken);
            }
            else
            {
                lastToken = token;
                token = nextToken;
                nextToken = null;
            }
            if (token == null)
            {
                break;
            }

            switch (token)
            {
                case Symbol symbol:
                    // Promote to function call if next lexeme is (, variable definition if next is symbol.
                    nextToken = tokenizer.GetNextToken(token);
                    if (nextToken is Operator && nextToken.Content == "(")
                        operatorStack.Push(new(symbol.Content, Operator.VariantType.FunctionCall));
                    else if (nextToken is Symbol)
                        operatorStack.Push(new(symbol.Content, Operator.VariantType.VariableDefinition));
                    else
                        operands.Add(new(token));
                    break;

                case Literal:
                    operands.Add(new(token));
                    break;

                case Operator op:
                    switch (op.Content)
                    {
                        case "(":
                        case "{":
                        case "[":
                            var closer = op.Content switch 
                            {
                                "(" => ")",
                                    "{" => "}",
                                    _ => "]",
                            };
                            // Special handling for empty brackets.
                            nextToken = tokenizer.GetNextToken(token);
                            if (nextToken is Operator && nextToken.Content == closer)
                                operatorStack.Push(new(op.Content, Operator.VariantType.Nullary));
                            else
                                operatorStack.Push(op);
                            break;

                        case ")":
                        case "}":
                        case "]":
                            {
                                var opener = op.Content switch 
                                {
                                    ")" => "(",
                                        "}" => "{",
                                        _ => "[",
                                };
                                while (operatorStack.Count > 0 && operatorStack.Peek().Content != opener)
                                {
                                    PopOperator(operatorStack, operands);
                                }
                                if (operatorStack.Count == 0)
                                    throw new Exception($"Mismatched {op.Content}");
                                PopOperator(operatorStack, operands);
                                if (operatorStack.Count > 0 &&
                                        operatorStack.Peek().Variant == Operator.VariantType.FunctionCall)
                                {
                                    PopOperator(operatorStack, operands);
                                }
                                break;
                            }
                        default:
                            while (operatorStack.Count > 0 && operatorStack.Peek().TakesPrecedenceOver(op))
                            {
                                PopOperator(operatorStack, operands);
                            }
                            operatorStack.Push(op);
                            break;
                    }
                    break;
            }
        }

        while(operatorStack.Count > 0)
        {
            PopOperator(operatorStack, operands);
        }

        Dictionary<string, NodeDef> outputs = new Dictionary<string, NodeDef>();
        foreach (var operand in operands)
        {
            operand.Compile(inputNamesTypes, outputs);
        }

        return outputs;
    }
}
