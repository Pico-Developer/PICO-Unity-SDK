using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ReciprocalSquareRootNodeConverter : NodeConverterBase<ReciprocalSquareRootNode>
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

            var sqrtNodeName = ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "ReciprocalSquareRootSqrt");
            var sqrtNode = graph.AddNode(sqrtNodeName, MaterialXNodeType.SquareRoot, outputType);
            MaterialXGraphUtil.AddInputPortAndEdge(stagingEdges, sqrtNode, inputSlot, "in", outputType);

            var divideNodeName = ShaderGraphUtil.NodeUtil.GetNodeName(shaderGraphNode, "ReciprocalSquareRoot");
            var divideNode = graph.AddNode(divideNodeName, MaterialXNodeType.Divide, outputType);
            divideNode.AddPortWithValue("in1", outputType, new float[] { 1f, 1f, 1f, 1f });
            graph.AddPortAndEdge(sqrtNode.Name, divideNode.Name, "in2", outputType);

            stagingEdges.AddPort(outputSlot.slotReference, divideNode.Name);
        }
    }
}
