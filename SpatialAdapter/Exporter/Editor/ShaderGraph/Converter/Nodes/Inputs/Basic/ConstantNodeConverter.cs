using System;
using UnityEditor.ShaderGraph;
using System.Collections.Generic;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ConstantNodeConverter : NodeConverterBase<ConstantNode>
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            float value = -1.0f; 
            var constant = ((ConstantNode)shaderGraphNode).constant;

            switch (constant)
            {
                case ConstantType.PI:
                    value = 3.1415926f;
                    break;
                case ConstantType.TAU:
                    value = 6.28318530f;
                    break;
                case ConstantType.PHI:
                    value = 1.618034f;
                    break;
                case ConstantType.E:
                    value = 2.718282f;
                    break;
                case ConstantType.SQRT2:
                    value = 1.414214f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
                
            var graphNode = MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.Constant, shaderGraphNode, graph, stagingEdges,
                "Float", outputType: MaterialXDataType.Float);
            graphNode.AddPortWithValue("value", MaterialXDataType.Float, new float[] { value });
        }
    }
}