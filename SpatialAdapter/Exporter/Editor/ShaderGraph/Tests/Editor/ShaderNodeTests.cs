using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.ShaderGraph;
using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;
using UnityEditor.ShaderGraph.Drawing;
using System.Linq;

public class ShaderNodeTests
{
    // A Test behaves as an ordinary method
    [Test]
    public void Vector1Node()
    {
        // Use the Assert class to test conditions
        var vector1Node = new Vector1Node();
        Assert.IsTrue(ConverterLookup.NodeCanConvert(vector1Node));

        MaterialXGraphData mxGraphData = new MaterialXGraphData("vector1Node", "vector1Node");
        StagingEdges stagingEdges = new StagingEdges();
        ConverterLookup.ConvertNode(vector1Node, mxGraphData, stagingEdges);
        Assert.AreEqual(mxGraphData.Nodes.Count(), 1);

        string nodeName = ShaderGraphUtil.NodeUtil.GetNodeName(vector1Node, "Vector1");
        var mtlxNode = mxGraphData.GetNode(nodeName);
        Assert.AreEqual(mtlxNode.DataType, MaterialXDataType.Float);
    }

    [Test]
    public void CustomFunctionNode()
    {
        var node = new CustomFunctionNode();
        node.AddSlot(new Vector1MaterialSlot(0, "Out", "Out", UnityEditor.Graphing.SlotType.Output, 0));
        node.AddSlot(new Vector1MaterialSlot(1, "A", "A", UnityEditor.Graphing.SlotType.Input, 0));
        node.AddSlot(new Vector1MaterialSlot(2, "B", "B", UnityEditor.Graphing.SlotType.Input, 0));

        node.sourceType = HlslSourceType.String;
        node.functionName = "AddTwoFloats";
        node.functionBody = "Out = A + B";

        Assert.IsTrue(ConverterLookup.NodeCanConvert(node));

        MaterialXGraphData mxGraphData = new MaterialXGraphData("CustomFunctionNode", "CustomFunctionNode");
        StagingEdges stagingEdges = new StagingEdges();
        ConverterLookup.ConvertNode(node, mxGraphData, stagingEdges);

        // TODO: Investigate how to test the correctness of MaterailX node
        var InputNodeNames = mxGraphData.InputNodeNames;

    }
}
