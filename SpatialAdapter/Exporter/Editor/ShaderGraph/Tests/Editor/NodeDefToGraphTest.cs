using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Graphs;
using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.TestTools;
using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;
using System.Linq;

public class NodeDefToGraph
{
    private GraphData mUnityShaderGraphData;
    private MaterialXGraphData mMtlXGraphData;
    private StagingEdges mStagingEdges;
    [OneTimeSetUp]
    public void Setup()
    {
        mUnityShaderGraphData = new GraphData();
        mMtlXGraphData = new MaterialXGraphData("CustomFunction", "CustomFunction");
        mStagingEdges = new StagingEdges();

    }
    [Test]
    public void fAPlusfBTimesfCfloat()
    {
        Dictionary<string, NodeDef> abstractSyntaxTreeNode = new()
        {
            ["Out"] = new(MaterialXNodeType.Add, MaterialXDataType.Float, "Out", new()
            {
                ["in1"] = new ExternalInputNodeDef("fA"),
                ["in2"] = new InlineInputNodeDef(new NodeDef(MaterialXNodeType.Multiply, MaterialXDataType.Float, "Out", new()
                {
                    ["in1"] = new ExternalInputNodeDef("fB"),
                    ["in2"] = new ExternalInputNodeDef("fC"),
                })),
            }),
        };

        // Construct a node that matches the inputs and outputs of the NodeDefs
        var node = new CustomFunctionNode();
        node.AddSlot(new Vector1MaterialSlot(0, "fA", "fA", UnityEditor.Graphing.SlotType.Input, 0));
        node.AddSlot(new Vector1MaterialSlot(1, "fB", "fB", UnityEditor.Graphing.SlotType.Input, 0));
        node.AddSlot(new Vector1MaterialSlot(2, "fC", "fC", UnityEditor.Graphing.SlotType.Input, 0));
        node.AddSlot(new Vector1MaterialSlot(3, "Out", "Out", UnityEditor.Graphing.SlotType.Output, 0));

        CompoundContext context = new CompoundContext(node, mMtlXGraphData, mStagingEdges, node.functionName, abstractSyntaxTreeNode);

        List<MaterialSlot> outputSlots = new();
        node.GetOutputSlots(outputSlots);

        CustomFunctionHelper.CombineNodeDefs(context, abstractSyntaxTreeNode, outputSlots);

        // Converted node count should be 5
        Assert.AreEqual(mMtlXGraphData.Nodes.Count(), 5);
    }
}
