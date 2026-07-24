using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class SamplerStateNodeConverter : NodeConverterBase<SamplerStateNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            // Sampler state settings are consumed directly by texture sampling converters.
            // This node does not need to emit a standalone MaterialX node.
        }
    }
}
