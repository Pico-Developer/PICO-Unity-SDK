using System;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class RedirectNodeConverter : INodeConverter
    {
        private static readonly Type RedirectNodeType = ResolveRedirectNodeType();

        public Type GetShaderGraphNodeType() => RedirectNodeType ?? typeof(AbstractMaterialNode);

        public bool NodeCanConvert(AbstractMaterialNode shaderGraphNode)
        {
            return RedirectNodeType != null && shaderGraphNode != null && shaderGraphNode.GetType() == RedirectNodeType;
        }

        public void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges edges)
        {
            // Unity Redirect nodes only organize graph wiring and do not affect generated shader code.
            // Export intentionally emits no MaterialX node.
        }

        private static Type ResolveRedirectNodeType()
        {
            var redirectNodeDataType = Type.GetType("UnityEditor.ShaderGraph.RedirectNodeData, Unity.ShaderGraph.Editor");
            if (redirectNodeDataType != null && typeof(AbstractMaterialNode).IsAssignableFrom(redirectNodeDataType))
            {
                return redirectNodeDataType;
            }

            redirectNodeDataType = Type.GetType("UnityEditor.ShaderGraph.RedirectNodeData, UnityEditor.ShaderGraph");
            if (redirectNodeDataType != null && typeof(AbstractMaterialNode).IsAssignableFrom(redirectNodeDataType))
            {
                return redirectNodeDataType;
            }

            return null;
        }
    }
}
