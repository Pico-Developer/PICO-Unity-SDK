using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class RotateNodeConverter : NodeConverterBase<RotateNode>
    {
        private const float DegreesToRadians = 3.1415926f / 180.0f;

        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var uvSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "UV") as UVMaterialSlot;
            var centerSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Center");
            var rotationSlot = ShaderGraphUtil.SlotUtil.GetSlotByName(shaderGraphNode, "Rotation");
            var outputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(shaderGraphNode);
            if (uvSlot == null || centerSlot == null || rotationSlot == null || outputSlot == null)
            {
                return;
            }

            bool useDegrees = shaderGraphNode.GetType().GetProperty("unit")?.GetValue(shaderGraphNode)?.ToString() == "Degrees";

            var centerNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RotateCenter"),
                MaterialXNodeType.Multiply,
                MaterialXDataType.Vector2);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, centerNode, centerSlot, "in1", MaterialXDataType.Vector2);
            centerNode.AddPortWithValue("in2", MaterialXDataType.Vector2, new float[] { 1f, 1f });

            var rotationNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RotateRotation"),
                MaterialXNodeType.Multiply,
                MaterialXDataType.Float);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, rotationNode, rotationSlot, "in1", MaterialXDataType.Float);
            rotationNode.AddPortWithValue("in2", MaterialXDataType.Float, useDegrees ? DegreesToRadians : 1f);

            var uvDeltaNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RotateDelta"),
                MaterialXNodeType.Subtract,
                MaterialXDataType.Vector2);
            MaterialXGraphUtil.HandleUVSlot(
                uvSlot,
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RotateUV"),
                uvDeltaNode.Name,
                "in1",
                graph,
                stagingEdges);
            graph.AddPortAndEdge(centerNode.Name, uvDeltaNode.Name, "in2", MaterialXDataType.Vector2);

            var sineNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RotateSine"),
                MaterialXNodeType.Sine,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(rotationNode.Name, sineNode.Name, "in", MaterialXDataType.Float);

            var cosineNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RotateCosine"),
                MaterialXNodeType.Cosine,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(rotationNode.Name, cosineNode.Name, "in", MaterialXDataType.Float);

            var negativeSineNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RotateNegSine"),
                MaterialXNodeType.Multiply,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(sineNode.Name, negativeSineNode.Name, "in1", MaterialXDataType.Float);
            negativeSineNode.AddPortWithValue("in2", MaterialXDataType.Float, -1f);

            var rotXNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RotateXBasis"),
                MaterialXNodeType.Combine2,
                MaterialXDataType.Vector2);
            graph.AddPortAndEdge(cosineNode.Name, rotXNode.Name, "in1", MaterialXDataType.Float);
            graph.AddPortAndEdge(sineNode.Name, rotXNode.Name, "in2", MaterialXDataType.Float);

            var rotYNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RotateYBasis"),
                MaterialXNodeType.Combine2,
                MaterialXDataType.Vector2);
            graph.AddPortAndEdge(negativeSineNode.Name, rotYNode.Name, "in1", MaterialXDataType.Float);
            graph.AddPortAndEdge(cosineNode.Name, rotYNode.Name, "in2", MaterialXDataType.Float);

            var dotXNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RotateDotX"),
                MaterialXNodeType.DotProduct,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(uvDeltaNode.Name, dotXNode.Name, "in1", MaterialXDataType.Vector2);
            graph.AddPortAndEdge(rotXNode.Name, dotXNode.Name, "in2", MaterialXDataType.Vector2);

            var dotYNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RotateDotY"),
                MaterialXNodeType.DotProduct,
                MaterialXDataType.Float);
            graph.AddPortAndEdge(uvDeltaNode.Name, dotYNode.Name, "in1", MaterialXDataType.Vector2);
            graph.AddPortAndEdge(rotYNode.Name, dotYNode.Name, "in2", MaterialXDataType.Vector2);

            var rotatedNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "RotateVec"),
                MaterialXNodeType.Combine2,
                MaterialXDataType.Vector2);
            graph.AddPortAndEdge(dotXNode.Name, rotatedNode.Name, "in1", MaterialXDataType.Float);
            graph.AddPortAndEdge(dotYNode.Name, rotatedNode.Name, "in2", MaterialXDataType.Float);

            var outNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "Rotate"),
                MaterialXNodeType.Add,
                MaterialXDataType.Vector2);
            graph.AddPortAndEdge(rotatedNode.Name, outNode.Name, "in1", MaterialXDataType.Vector2);
            graph.AddPortAndEdge(centerNode.Name, outNode.Name, "in2", MaterialXDataType.Vector2);

            stagingEdges.AddPort(outputSlot.slotReference, outNode.Name);
        }
    }
}
