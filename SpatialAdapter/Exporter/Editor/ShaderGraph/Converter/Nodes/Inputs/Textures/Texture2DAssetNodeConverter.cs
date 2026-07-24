using System.Linq;
using UnityEditor.ShaderGraph;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class Texture2DAssetNodeConverter : NodeConverterBase<Texture2DAssetNode>
    {
        private static bool ShouldInlineTextureAsset(MaterialSlot slot)
        {
            if (slot == null)
                return false;

            var downstreamEdges = slot.owner.owner.GetEdges(slot.slotReference).ToList();
            if (downstreamEdges.Count == 0)
                return false;

            return downstreamEdges.All(edge =>
                edge.inputSlot.node is SampleTexture2DNode &&
                edge.inputSlot.slot.RawDisplayName() == "Texture");
        }

        private static string GetVariableNameForSlot(MaterialSlot slot)
        {
            var variableName = slot.owner.GetVariableNameForSlot(slot.id);
            var startIndex = variableName.IndexOf('(');
            var endIndex = variableName.LastIndexOf(')');
            if (startIndex != -1 && endIndex != -1)
                variableName = variableName.Substring(startIndex + 1, endIndex - startIndex - 1);
            
            return variableName;
        }

        public override void Convert(AbstractMaterialNode node, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var slot = ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(node);
            if (ShouldInlineTextureAsset(slot))
                return;

            MaterialXGraphUtil.AddImplicitPropertyFromNode(
                GetVariableNameForSlot(slot), MaterialXDataType.Filename, node, graph, stagingEdges, slot.RawDisplayName());
        }
    }
}
