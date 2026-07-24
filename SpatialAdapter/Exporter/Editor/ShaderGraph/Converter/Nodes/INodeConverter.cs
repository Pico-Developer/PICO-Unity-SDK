using System;
using UnityEditor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal interface INodeConverter
    {
        Type GetShaderGraphNodeType();

        bool NodeCanConvert(AbstractMaterialNode shaderGraphNode);

        void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges edges);
    }

    internal abstract class NodeConverterBase<T> : INodeConverter where T : AbstractMaterialNode
    {
        public Type GetShaderGraphNodeType() => typeof(T);

        public virtual bool NodeCanConvert(AbstractMaterialNode shaderGraphNode) => shaderGraphNode is T;

        public abstract void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph,
            StagingEdges stagingEdges);
    }
}