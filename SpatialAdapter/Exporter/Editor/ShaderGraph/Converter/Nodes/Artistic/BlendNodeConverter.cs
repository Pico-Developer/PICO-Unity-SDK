using System.Reflection;
using UnityEditor.ShaderGraph;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class BlendNodeConverter : NodeConverterBase<BlendNode>
    {
        private const float k_Epsilon = 1e-12f;

        private static float[] CreateConstant(MaterialXDataType dataType, float value)
        {
            var values = new float[dataType.ChannelCount()];
            for (int i = 0; i < values.Length; ++i)
            {
                values[i] = value;
            }

            return values;
        }

        private static string GetBlendModeName(BlendNode blendNode)
        {
            var modeProperty = blendNode.GetType().GetProperty(
                "blendMode",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return modeProperty?.GetValue(blendNode)?.ToString() ?? "Overwrite";
        }

        private static void SetNodeInput(
            MaterialXGraphData graph,
            MaterialXNodeData nodeData,
            string portName,
            MaterialXDataType portType,
            string upstreamNodeName = null,
            float[] constantValue = null)
        {
            if (!string.IsNullOrEmpty(upstreamNodeName))
            {
                graph.AddPortAndEdge(upstreamNodeName, nodeData.Name, portName, portType);
                return;
            }

            nodeData.AddPortWithValue(portName, portType, constantValue ?? new float[portType.ChannelCount()]);
        }

        private static MaterialXNodeData AddUnaryNode(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            string context,
            string nodeType,
            MaterialXDataType outputType,
            string inputNodeName)
        {
            var nodeData = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, context),
                nodeType,
                outputType);

            SetNodeInput(graph, nodeData, "in", outputType, inputNodeName);
            return nodeData;
        }

        private static MaterialXNodeData AddBinaryNode(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            string context,
            string nodeType,
            MaterialXDataType outputType,
            string in1NodeName = null,
            float[] in1Value = null,
            string in2NodeName = null,
            float[] in2Value = null)
        {
            var nodeData = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, context),
                nodeType,
                outputType);

            SetNodeInput(graph, nodeData, "in1", outputType, in1NodeName, in1Value);
            SetNodeInput(graph, nodeData, "in2", outputType, in2NodeName, in2Value);
            return nodeData;
        }

        private static MaterialXNodeData AddStepNode(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            string context,
            MaterialXDataType outputType,
            string edgeNodeName = null,
            float[] edgeValue = null,
            string inputNodeName = null,
            float[] inputValue = null)
        {
            var nodeData = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, context),
                MaterialXNodeType.Step,
                outputType);

            SetNodeInput(graph, nodeData, "edge", outputType, edgeNodeName, edgeValue);
            SetNodeInput(graph, nodeData, "in", outputType, inputNodeName, inputValue);
            return nodeData;
        }

        private static MaterialXNodeData AddLerpNode(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            string context,
            MaterialXDataType outputType,
            string aNodeName,
            string bNodeName,
            string tNodeName)
        {
            var deltaNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, $"{context}Delta"),
                MaterialXNodeType.Subtract,
                outputType);
            SetNodeInput(graph, deltaNode, "in1", outputType, bNodeName);
            SetNodeInput(graph, deltaNode, "in2", outputType, aNodeName);

            var scaleNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, $"{context}Scale"),
                MaterialXNodeType.Multiply,
                outputType);
            SetNodeInput(graph, scaleNode, "in1", outputType, deltaNode.Name);
            SetNodeInput(graph, scaleNode, "in2", MaterialXDataType.Float, tNodeName);

            var resultNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, context),
                MaterialXNodeType.Add,
                outputType);
            SetNodeInput(graph, resultNode, "in1", outputType, aNodeName);
            SetNodeInput(graph, resultNode, "in2", outputType, scaleNode.Name);
            return resultNode;
        }

        private static MaterialXNodeData AddClampNode(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            string context,
            MaterialXDataType outputType,
            string inputNodeName,
            float lowValue,
            float highValue)
        {
            var nodeData = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, context),
                MaterialXNodeType.Clamp,
                outputType);

            SetNodeInput(graph, nodeData, "in", outputType, inputNodeName);
            nodeData.AddPortWithValue("low", outputType, CreateConstant(outputType, lowValue));
            nodeData.AddPortWithValue("high", outputType, CreateConstant(outputType, highValue));
            return nodeData;
        }

        private static MaterialXNodeData AddInputProxyNode(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            StagingEdges stagingEdges,
            string context,
            MaterialSlot slot,
            MaterialXDataType outputType)
        {
            var nodeData = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, context),
                MaterialXNodeType.Add,
                outputType);

            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, nodeData, slot, "in1", outputType);
            nodeData.AddPortWithValue("in2", outputType, CreateConstant(outputType, 0.0f));
            return nodeData;
        }

        private static MaterialXNodeData BuildBurn(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var oneMinusBlend = AddBinaryNode(
                shaderGraphNode, graph, "BlendBurnOneMinusBlend",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: blendNodeName);
            var divide = AddBinaryNode(
                shaderGraphNode, graph, "BlendBurnDivide",
                MaterialXNodeType.Divide, outputType,
                in1NodeName: oneMinusBlend.Name,
                in2NodeName: baseNodeName);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendBurnResult",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: divide.Name);
        }

        private static MaterialXNodeData BuildDarken(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendDarkenResult",
                MaterialXNodeType.Min, outputType,
                in1NodeName: blendNodeName,
                in2NodeName: baseNodeName);
        }

        private static MaterialXNodeData BuildDifference(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var subtract = AddBinaryNode(
                shaderGraphNode, graph, "BlendDifferenceSubtract",
                MaterialXNodeType.Subtract, outputType,
                in1NodeName: blendNodeName,
                in2NodeName: baseNodeName);
            return AddUnaryNode(
                shaderGraphNode, graph, "BlendDifferenceResult",
                MaterialXNodeType.AbsVal, outputType,
                subtract.Name);
        }

        private static MaterialXNodeData BuildDodge(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var oneMinusBlend = AddBinaryNode(
                shaderGraphNode, graph, "BlendDodgeOneMinusBlend",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: blendNodeName);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendDodgeResult",
                MaterialXNodeType.Divide, outputType,
                in1NodeName: baseNodeName,
                in2NodeName: oneMinusBlend.Name);
        }

        private static MaterialXNodeData BuildDivide(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var blendWithEpsilon = AddBinaryNode(
                shaderGraphNode, graph, "BlendDivideBlendWithEpsilon",
                MaterialXNodeType.Add, outputType,
                in1NodeName: blendNodeName,
                in2Value: CreateConstant(outputType, k_Epsilon));
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendDivideResult",
                MaterialXNodeType.Divide, outputType,
                in1NodeName: baseNodeName,
                in2NodeName: blendWithEpsilon.Name);
        }

        private static MaterialXNodeData BuildExclusion(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var multiply = AddBinaryNode(
                shaderGraphNode, graph, "BlendExclusionMultiply",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: blendNodeName,
                in2NodeName: baseNodeName);
            var scaledMultiply = AddBinaryNode(
                shaderGraphNode, graph, "BlendExclusionScaledMultiply",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: multiply.Name,
                in2Value: CreateConstant(outputType, 2.0f));
            var sum = AddBinaryNode(
                shaderGraphNode, graph, "BlendExclusionSum",
                MaterialXNodeType.Add, outputType,
                in1NodeName: blendNodeName,
                in2NodeName: baseNodeName);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendExclusionResult",
                MaterialXNodeType.Subtract, outputType,
                in1NodeName: sum.Name,
                in2NodeName: scaledMultiply.Name);
        }

        private static MaterialXNodeData BuildHardLight(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var oneMinusBase = AddBinaryNode(
                shaderGraphNode, graph, "BlendHardLightOneMinusBase",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: baseNodeName);
            var oneMinusBlend = AddBinaryNode(
                shaderGraphNode, graph, "BlendHardLightOneMinusBlend",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: blendNodeName);
            var darkMultiply = AddBinaryNode(
                shaderGraphNode, graph, "BlendHardLightDarkMultiply",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: oneMinusBase.Name,
                in2NodeName: oneMinusBlend.Name);
            var darkScaled = AddBinaryNode(
                shaderGraphNode, graph, "BlendHardLightDarkScaled",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: darkMultiply.Name,
                in2Value: CreateConstant(outputType, 2.0f));
            var result1 = AddBinaryNode(
                shaderGraphNode, graph, "BlendHardLightResult1",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: darkScaled.Name);

            var lightMultiply = AddBinaryNode(
                shaderGraphNode, graph, "BlendHardLightLightMultiply",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: baseNodeName,
                in2NodeName: blendNodeName);
            var result2 = AddBinaryNode(
                shaderGraphNode, graph, "BlendHardLightResult2",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: lightMultiply.Name,
                in2Value: CreateConstant(outputType, 2.0f));

            var zeroOrOne = AddStepNode(
                shaderGraphNode, graph, "BlendHardLightMask",
                outputType,
                edgeNodeName: blendNodeName,
                inputValue: CreateConstant(outputType, 0.5f));
            var inverseMask = AddBinaryNode(
                shaderGraphNode, graph, "BlendHardLightInverseMask",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: zeroOrOne.Name);

            var low = AddBinaryNode(
                shaderGraphNode, graph, "BlendHardLightLow",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: result2.Name,
                in2NodeName: zeroOrOne.Name);
            var high = AddBinaryNode(
                shaderGraphNode, graph, "BlendHardLightHigh",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: inverseMask.Name,
                in2NodeName: result1.Name);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendHardLightResult",
                MaterialXNodeType.Add, outputType,
                in1NodeName: low.Name,
                in2NodeName: high.Name);
        }

        private static MaterialXNodeData BuildHardMix(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var oneMinusBase = AddBinaryNode(
                shaderGraphNode, graph, "BlendHardMixOneMinusBase",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: baseNodeName);
            return AddStepNode(
                shaderGraphNode, graph, "BlendHardMixResult",
                outputType,
                edgeNodeName: oneMinusBase.Name,
                inputNodeName: blendNodeName);
        }

        private static MaterialXNodeData BuildLighten(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendLightenResult",
                MaterialXNodeType.Max, outputType,
                in1NodeName: blendNodeName,
                in2NodeName: baseNodeName);
        }

        private static MaterialXNodeData BuildLinearBurn(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var sum = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearBurnSum",
                MaterialXNodeType.Add, outputType,
                in1NodeName: baseNodeName,
                in2NodeName: blendNodeName);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearBurnResult",
                MaterialXNodeType.Subtract, outputType,
                in1NodeName: sum.Name,
                in2Value: CreateConstant(outputType, 1.0f));
        }

        private static MaterialXNodeData BuildLinearDodge(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearDodgeResult",
                MaterialXNodeType.Add, outputType,
                in1NodeName: baseNodeName,
                in2NodeName: blendNodeName);
        }

        private static MaterialXNodeData BuildLinearLight(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var doubledBlend = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightDoubledBlend",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: blendNodeName,
                in2Value: CreateConstant(outputType, 2.0f));

            var lowSum = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightLowSum",
                MaterialXNodeType.Add, outputType,
                in1NodeName: baseNodeName,
                in2NodeName: doubledBlend.Name);
            var lowRaw = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightLowRaw",
                MaterialXNodeType.Subtract, outputType,
                in1NodeName: lowSum.Name,
                in2Value: CreateConstant(outputType, 1.0f));
            var low = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightLow",
                MaterialXNodeType.Max, outputType,
                in1NodeName: lowRaw.Name,
                in2Value: CreateConstant(outputType, 0.0f));

            var blendMinusHalf = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightBlendMinusHalf",
                MaterialXNodeType.Subtract, outputType,
                in1NodeName: blendNodeName,
                in2Value: CreateConstant(outputType, 0.5f));
            var doubledHighBlend = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightDoubledHighBlend",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: blendMinusHalf.Name,
                in2Value: CreateConstant(outputType, 2.0f));
            var highRaw = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightHighRaw",
                MaterialXNodeType.Add, outputType,
                in1NodeName: baseNodeName,
                in2NodeName: doubledHighBlend.Name);
            var high = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightHigh",
                MaterialXNodeType.Min, outputType,
                in1NodeName: highRaw.Name,
                in2Value: CreateConstant(outputType, 1.0f));

            var mask = AddStepNode(
                shaderGraphNode, graph, "BlendLinearLightMask",
                outputType,
                edgeNodeName: blendNodeName,
                inputValue: CreateConstant(outputType, 0.5f));
            var inverseMask = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightInverseMask",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: mask.Name);

            var lowMasked = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightLowMasked",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: low.Name,
                in2NodeName: mask.Name);
            var highMasked = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightHighMasked",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: inverseMask.Name,
                in2NodeName: high.Name);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightResult",
                MaterialXNodeType.Add, outputType,
                in1NodeName: lowMasked.Name,
                in2NodeName: highMasked.Name);
        }

        private static MaterialXNodeData BuildLinearLightAddSub(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var doubledBase = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightAddSubDoubledBase",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: baseNodeName,
                in2Value: CreateConstant(outputType, 2.0f));
            var sum = AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightAddSubSum",
                MaterialXNodeType.Add, outputType,
                in1NodeName: blendNodeName,
                in2NodeName: doubledBase.Name);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendLinearLightAddSubResult",
                MaterialXNodeType.Subtract, outputType,
                in1NodeName: sum.Name,
                in2Value: CreateConstant(outputType, 1.0f));
        }

        private static MaterialXNodeData BuildMultiply(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendMultiplyResult",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: baseNodeName,
                in2NodeName: blendNodeName);
        }

        private static MaterialXNodeData BuildNegation(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var sum = AddBinaryNode(
                shaderGraphNode, graph, "BlendNegationSum",
                MaterialXNodeType.Add, outputType,
                in1NodeName: blendNodeName,
                in2NodeName: baseNodeName);
            var oneMinusSum = AddBinaryNode(
                shaderGraphNode, graph, "BlendNegationOneMinusSum",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: sum.Name);
            var abs = AddUnaryNode(
                shaderGraphNode, graph, "BlendNegationAbs",
                MaterialXNodeType.AbsVal, outputType,
                oneMinusSum.Name);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendNegationResult",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: abs.Name);
        }

        private static MaterialXNodeData BuildOverlay(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var oneMinusBase = AddBinaryNode(
                shaderGraphNode, graph, "BlendOverlayOneMinusBase",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: baseNodeName);
            var oneMinusBlend = AddBinaryNode(
                shaderGraphNode, graph, "BlendOverlayOneMinusBlend",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: blendNodeName);
            var darkMultiply = AddBinaryNode(
                shaderGraphNode, graph, "BlendOverlayDarkMultiply",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: oneMinusBase.Name,
                in2NodeName: oneMinusBlend.Name);
            var darkScaled = AddBinaryNode(
                shaderGraphNode, graph, "BlendOverlayDarkScaled",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: darkMultiply.Name,
                in2Value: CreateConstant(outputType, 2.0f));
            var result1 = AddBinaryNode(
                shaderGraphNode, graph, "BlendOverlayResult1",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: darkScaled.Name);

            var lightMultiply = AddBinaryNode(
                shaderGraphNode, graph, "BlendOverlayLightMultiply",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: baseNodeName,
                in2NodeName: blendNodeName);
            var result2 = AddBinaryNode(
                shaderGraphNode, graph, "BlendOverlayResult2",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: lightMultiply.Name,
                in2Value: CreateConstant(outputType, 2.0f));

            var zeroOrOne = AddStepNode(
                shaderGraphNode, graph, "BlendOverlayMask",
                outputType,
                edgeNodeName: baseNodeName,
                inputValue: CreateConstant(outputType, 0.5f));
            var inverseMask = AddBinaryNode(
                shaderGraphNode, graph, "BlendOverlayInverseMask",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: zeroOrOne.Name);

            var low = AddBinaryNode(
                shaderGraphNode, graph, "BlendOverlayLow",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: result2.Name,
                in2NodeName: zeroOrOne.Name);
            var high = AddBinaryNode(
                shaderGraphNode, graph, "BlendOverlayHigh",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: inverseMask.Name,
                in2NodeName: result1.Name);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendOverlayResult",
                MaterialXNodeType.Add, outputType,
                in1NodeName: low.Name,
                in2NodeName: high.Name);
        }

        private static MaterialXNodeData BuildPinLight(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var check = AddStepNode(
                shaderGraphNode, graph, "BlendPinLightCheck",
                outputType,
                edgeValue: CreateConstant(outputType, 0.5f),
                inputNodeName: blendNodeName);
            var baseMinusHalf = AddBinaryNode(
                shaderGraphNode, graph, "BlendPinLightBaseMinusHalf",
                MaterialXNodeType.Subtract, outputType,
                in1NodeName: baseNodeName,
                in2Value: CreateConstant(outputType, 0.5f));
            var doubledBaseMinusHalf = AddBinaryNode(
                shaderGraphNode, graph, "BlendPinLightDoubledBaseMinusHalf",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: baseMinusHalf.Name,
                in2Value: CreateConstant(outputType, 2.0f));
            var max = AddBinaryNode(
                shaderGraphNode, graph, "BlendPinLightMax",
                MaterialXNodeType.Max, outputType,
                in1NodeName: doubledBaseMinusHalf.Name,
                in2NodeName: blendNodeName);
            var result1 = AddBinaryNode(
                shaderGraphNode, graph, "BlendPinLightResult1",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: check.Name,
                in2NodeName: max.Name);

            var doubledBase = AddBinaryNode(
                shaderGraphNode, graph, "BlendPinLightDoubledBase",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: baseNodeName,
                in2Value: CreateConstant(outputType, 2.0f));
            var min = AddBinaryNode(
                shaderGraphNode, graph, "BlendPinLightMin",
                MaterialXNodeType.Min, outputType,
                in1NodeName: doubledBase.Name,
                in2NodeName: blendNodeName);
            var inverseCheck = AddBinaryNode(
                shaderGraphNode, graph, "BlendPinLightInverseCheck",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: check.Name);
            var result2 = AddBinaryNode(
                shaderGraphNode, graph, "BlendPinLightResult2",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: inverseCheck.Name,
                in2NodeName: min.Name);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendPinLightResult",
                MaterialXNodeType.Add, outputType,
                in1NodeName: result1.Name,
                in2NodeName: result2.Name);
        }

        private static MaterialXNodeData BuildScreen(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var oneMinusBlend = AddBinaryNode(
                shaderGraphNode, graph, "BlendScreenOneMinusBlend",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: blendNodeName);
            var oneMinusBase = AddBinaryNode(
                shaderGraphNode, graph, "BlendScreenOneMinusBase",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: baseNodeName);
            var multiply = AddBinaryNode(
                shaderGraphNode, graph, "BlendScreenMultiply",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: oneMinusBlend.Name,
                in2NodeName: oneMinusBase.Name);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendScreenResult",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: multiply.Name);
        }

        private static MaterialXNodeData BuildSoftLight(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var doubledBaseBlend = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightDoubledBaseBlendMul",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: baseNodeName,
                in2NodeName: blendNodeName);
            var firstTerm = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightFirstTerm",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: doubledBaseBlend.Name,
                in2Value: CreateConstant(outputType, 2.0f));

            var baseSquared = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightBaseSquared",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: baseNodeName,
                in2NodeName: baseNodeName);
            var doubledBlend = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightDoubledBlend",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: blendNodeName,
                in2Value: CreateConstant(outputType, 2.0f));
            var oneMinusDoubledBlend = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightOneMinusDoubledBlend",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: doubledBlend.Name);
            var secondTerm = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightSecondTerm",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: baseSquared.Name,
                in2NodeName: oneMinusDoubledBlend.Name);
            var result1 = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightResult1",
                MaterialXNodeType.Add, outputType,
                in1NodeName: firstTerm.Name,
                in2NodeName: secondTerm.Name);

            var sqrtBase = AddUnaryNode(
                shaderGraphNode, graph, "BlendSoftLightSqrtBase",
                MaterialXNodeType.SquareRoot, outputType,
                baseNodeName);
            var doubledBlendMinusOne = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightDoubledBlendMinusOne",
                MaterialXNodeType.Subtract, outputType,
                in1NodeName: doubledBlend.Name,
                in2Value: CreateConstant(outputType, 1.0f));
            var highFirstTerm = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightHighFirstTerm",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: sqrtBase.Name,
                in2NodeName: doubledBlendMinusOne.Name);
            var oneMinusBlend = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightOneMinusBlend",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: blendNodeName);
            var doubledBase = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightDoubledBase",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: baseNodeName,
                in2Value: CreateConstant(outputType, 2.0f));
            var highSecondTerm = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightHighSecondTerm",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: doubledBase.Name,
                in2NodeName: oneMinusBlend.Name);
            var result2 = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightResult2",
                MaterialXNodeType.Add, outputType,
                in1NodeName: highFirstTerm.Name,
                in2NodeName: highSecondTerm.Name);

            var mask = AddStepNode(
                shaderGraphNode, graph, "BlendSoftLightMask",
                outputType,
                edgeValue: CreateConstant(outputType, 0.5f),
                inputNodeName: blendNodeName);
            var inverseMask = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightInverseMask",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: mask.Name);
            var high = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightHigh",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: result2.Name,
                in2NodeName: mask.Name);
            var low = AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightLow",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: inverseMask.Name,
                in2NodeName: result1.Name);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendSoftLightResult",
                MaterialXNodeType.Add, outputType,
                in1NodeName: high.Name,
                in2NodeName: low.Name);
        }

        private static MaterialXNodeData BuildSubtract(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendSubtractResult",
                MaterialXNodeType.Subtract, outputType,
                in1NodeName: baseNodeName,
                in2NodeName: blendNodeName);
        }

        private static MaterialXNodeData BuildVividLight(
            AbstractMaterialNode shaderGraphNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            var oneMinusBlend = AddBinaryNode(
                shaderGraphNode, graph, "BlendVividLightOneMinusBlend",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: blendNodeName);
            var doubledBase = AddBinaryNode(
                shaderGraphNode, graph, "BlendVividLightDoubledBase",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: baseNodeName,
                in2Value: CreateConstant(outputType, 2.0f));
            var firstDivide = AddBinaryNode(
                shaderGraphNode, graph, "BlendVividLightFirstDivide",
                MaterialXNodeType.Divide, outputType,
                in1NodeName: oneMinusBlend.Name,
                in2NodeName: doubledBase.Name);
            var result1 = AddBinaryNode(
                shaderGraphNode, graph, "BlendVividLightResult1",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: firstDivide.Name);

            var oneMinusBase = AddBinaryNode(
                shaderGraphNode, graph, "BlendVividLightOneMinusBase",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: baseNodeName);
            var doubledOneMinusBase = AddBinaryNode(
                shaderGraphNode, graph, "BlendVividLightDoubledOneMinusBase",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: oneMinusBase.Name,
                in2Value: CreateConstant(outputType, 2.0f));
            var result2 = AddBinaryNode(
                shaderGraphNode, graph, "BlendVividLightResult2",
                MaterialXNodeType.Divide, outputType,
                in1NodeName: blendNodeName,
                in2NodeName: doubledOneMinusBase.Name);

            var mask = AddStepNode(
                shaderGraphNode, graph, "BlendVividLightMask",
                outputType,
                edgeValue: CreateConstant(outputType, 0.5f),
                inputNodeName: baseNodeName);
            var inverseMask = AddBinaryNode(
                shaderGraphNode, graph, "BlendVividLightInverseMask",
                MaterialXNodeType.Subtract, outputType,
                in1Value: CreateConstant(outputType, 1.0f),
                in2NodeName: mask.Name);
            var high = AddBinaryNode(
                shaderGraphNode, graph, "BlendVividLightHigh",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: result2.Name,
                in2NodeName: mask.Name);
            var low = AddBinaryNode(
                shaderGraphNode, graph, "BlendVividLightLow",
                MaterialXNodeType.Multiply, outputType,
                in1NodeName: inverseMask.Name,
                in2NodeName: result1.Name);
            return AddBinaryNode(
                shaderGraphNode, graph, "BlendVividLightResult",
                MaterialXNodeType.Add, outputType,
                in1NodeName: high.Name,
                in2NodeName: low.Name);
        }

        private static MaterialXNodeData BuildBlendMode(
            BlendNode blendNode,
            MaterialXGraphData graph,
            MaterialXDataType outputType,
            string baseNodeName,
            string blendNodeName)
        {
            return GetBlendModeName(blendNode) switch
            {
                "Overlay" => BuildOverlay(blendNode, graph, outputType, baseNodeName, blendNodeName),
                "Overwrite" => null,
                _ => BuildLinearDodge(blendNode, graph, outputType, baseNodeName, blendNodeName),
            };
        }

        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            if (shaderGraphNode is not BlendNode blendNode)
                return;

            var outputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(shaderGraphNode);
            var outputType = TypeUtil.GetMaterialXDataType(outputSlot);

            var baseSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Base");
            var blendSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Blend");
            var opacitySlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Opacity");

            var baseProxy = AddInputProxyNode(
                shaderGraphNode, graph, stagingEdges, "BlendBaseProxy", baseSlot, outputType);
            var blendProxy = AddInputProxyNode(
                shaderGraphNode, graph, stagingEdges, "BlendBlendProxy", blendSlot, outputType);
            var opacityProxy = AddInputProxyNode(
                shaderGraphNode, graph, stagingEdges, "BlendOpacityProxy", opacitySlot, MaterialXDataType.Float);

            var blendResult = BuildBlendMode(blendNode, graph, outputType, baseProxy.Name, blendProxy.Name);
            var finalResult = AddLerpNode(
                shaderGraphNode,
                graph,
                "BlendFinalResult",
                outputType,
                baseProxy.Name,
                blendResult?.Name ?? blendProxy.Name,
                opacityProxy.Name);

            stagingEdges.AddPort(outputSlot.slotReference, finalResult.Name);
        }
    }
}
