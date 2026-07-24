using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ColorNodeConverter : NodeConverterBase<ColorNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            if (shaderGraphNode is ColorNode colorNode)
            {
                // HDR color may exceed range [0, 1], user vector 4 instead of Color4
                var nodeType = colorNode.color.mode == ColorMode.HDR ? MaterialXDataType.Vector4 : MaterialXDataType.Color4;
                var nodeData = MaterialXGraphUtil.AddNaryOperatorNode(
                    MaterialXNodeType.Constant, shaderGraphNode, graph, stagingEdges, "Color", outputType: nodeType);

                // Convert color constants to linear color space.
                var c = colorNode.color.color.linear;
                var value = new float[] { c.r, c.g, c.b, c.a };

                nodeData.AddPortWithValue("value", nodeType, value);
            }
        }
    }
}