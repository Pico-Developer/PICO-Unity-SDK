using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class TruncateNodeConverter : NodeConverterBase<TruncateNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var inputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryInputSlot(shaderGraphNode);
            var outputSlot = ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(shaderGraphNode);
            if (inputSlot == null || outputSlot == null)
            {
                return;
            }

            var outputType = TypeUtil.GetMaterialXDataType(outputSlot);

            var inputNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "TruncateInput"),
                MaterialXNodeType.Multiply,
                outputType);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, inputNode, inputSlot, "in1", outputType);
            inputNode.AddPortWithValue("in2", outputType, new float[] { 1f, 1f, 1f, 1f });

            var absNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "TruncateAbs"),
                MaterialXNodeType.AbsVal,
                outputType);
            graph.AddPortAndEdge(inputNode.Name, absNode.Name, "in", outputType);

            var floorNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "TruncateFloor"),
                MaterialXNodeType.Floor,
                outputType);
            graph.AddPortAndEdge(absNode.Name, floorNode.Name, "in", outputType);

            var signNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "TruncateSign"),
                MaterialXNodeType.Sign,
                outputType);
            graph.AddPortAndEdge(inputNode.Name, signNode.Name, "in", outputType);

            var multiplyNode = graph.AddNode(
                ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "Truncate"),
                MaterialXNodeType.Multiply,
                outputType);
            graph.AddPortAndEdge(signNode.Name, multiplyNode.Name, "in1", outputType);
            graph.AddPortAndEdge(floorNode.Name, multiplyNode.Name, "in2", outputType);

            stagingEdges.AddPort(outputSlot.slotReference, multiplyNode.Name);
        }
    }
}
