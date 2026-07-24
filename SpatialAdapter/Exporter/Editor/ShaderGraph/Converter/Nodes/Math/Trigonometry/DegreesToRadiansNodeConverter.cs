using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class DegreesToRadiansNodeConverter : NodeConverterBase<DegreesToRadiansNode>
    {
        private const float DegreesToRadians = 0.01745329251994329576923690768489f;

        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var nodeData = MaterialXGraphUtil.AddUnaryOperatorNode(
                MaterialXNodeType.Multiply, shaderGraphNode, graph, stagingEdges, "in1");
            nodeData.AddPortWithValue("in2", nodeData.DataType, new float[] { DegreesToRadians, DegreesToRadians, DegreesToRadians, DegreesToRadians });
        }
    }
}
