using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class RadiansToDegreesNodeConverter : NodeConverterBase<RadiansToDegreesNode>
    {
        private const float RadiansToDegrees = 57.295779513082320876798154814105f;

        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var nodeData = MaterialXGraphUtil.AddUnaryOperatorNode(
                MaterialXNodeType.Multiply, shaderGraphNode, graph, stagingEdges, "in1");
            nodeData.AddPortWithValue("in2", nodeData.DataType, new float[] { RadiansToDegrees, RadiansToDegrees, RadiansToDegrees, RadiansToDegrees });
        }
    }
}
