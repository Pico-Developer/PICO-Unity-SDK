using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class NormalStrengthNodeConverter : NodeConverterBase<NormalStrengthNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var inputSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "In");
            var strengthSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Strength");
            var outputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(shaderGraphNode);

            var inputProxyNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalStrengthInputProxy"),
                MaterialXNodeType.Add,
                MaterialXDataType.Vector3);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, inputProxyNode, inputSlot, "in1", MaterialXDataType.Vector3);
            inputProxyNode.AddPortWithValue("in2", MaterialXDataType.Vector3, new[] { 0.0f, 0.0f, 0.0f });

            var strengthProxyNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalStrengthValueProxy"),
                MaterialXNodeType.Add,
                MaterialXDataType.Float);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, strengthProxyNode, strengthSlot, "in1", MaterialXDataType.Float);
            strengthProxyNode.AddPortWithValue("in2", MaterialXDataType.Float, 0.0f);

            var xyNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalStrengthXY"),
                MaterialXNodeType.Swizzle,
                MaterialXDataType.Vector2);
            xyNode.AddPortWithStringValue("channels", MaterialXDataType.String, "xy");
            graph.AddPortAndEdge(inputProxyNode.Name, xyNode.Name, "in", MaterialXDataType.Vector3);

            var scaledXyNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalStrengthScaledXY"),
                MaterialXNodeType.Multiply,
                MaterialXDataType.Vector2);
            graph.AddPortAndEdge(xyNode.Name, scaledXyNode.Name, "in1", MaterialXDataType.Vector2);
            graph.AddPortAndEdge(strengthProxyNode.Name, scaledXyNode.Name, "in2", MaterialXDataType.Float);

            var xNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalStrengthX"),
                MaterialXNodeType.Swizzle,
                MaterialXDataType.Float);
            xNode.AddPortWithStringValue("channels", MaterialXDataType.String, "x");
            graph.AddPortAndEdge(scaledXyNode.Name, xNode.Name, "in", MaterialXDataType.Vector2);

            var yNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalStrengthY"),
                MaterialXNodeType.Swizzle,
                MaterialXDataType.Float);
            yNode.AddPortWithStringValue("channels", MaterialXDataType.String, "y");
            graph.AddPortAndEdge(scaledXyNode.Name, yNode.Name, "in", MaterialXDataType.Vector2);

            var zNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalStrengthZ"),
                MaterialXNodeType.Swizzle,
                MaterialXDataType.Float);
            zNode.AddPortWithStringValue("channels", MaterialXDataType.String, "z");
            graph.AddPortAndEdge(inputProxyNode.Name, zNode.Name, "in", MaterialXDataType.Vector3);

            var saturateNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalStrengthSaturate"),
                MaterialXNodeType.Clamp,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(strengthProxyNode.Name, saturateNode.Name, "in", MaterialXDataType.Float);
            saturateNode.AddPortWithValue("low", MaterialXDataType.Float, 0.0f);
            saturateNode.AddPortWithValue("high", MaterialXDataType.Float, 1.0f);

            var zMixNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalStrengthMixedZ"),
                MaterialXNodeType.Mix,
                MaterialXDataType.Float);
            zMixNode.AddPortWithValue("bg", MaterialXDataType.Float, 1.0f);
            graph.AddPortAndEdge(zNode.Name, zMixNode.Name, "fg", MaterialXDataType.Float);
            graph.AddPortAndEdge(saturateNode.Name, zMixNode.Name, "mix", MaterialXDataType.Float);

            var combineNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "NormalStrengthOut"),
                MaterialXNodeType.Combine3,
                MaterialXDataType.Vector3);
            graph.AddPortAndEdge(xNode.Name, combineNode.Name, "in1", MaterialXDataType.Float);
            graph.AddPortAndEdge(yNode.Name, combineNode.Name, "in2", MaterialXDataType.Float);
            graph.AddPortAndEdge(zMixNode.Name, combineNode.Name, "in3", MaterialXDataType.Float);

            stagingEdges.AddPort(outputSlot.slotReference, combineNode.Name);
        }
    }
}
