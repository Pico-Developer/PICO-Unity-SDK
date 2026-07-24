using System.Collections.Generic;
using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.Assertions;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class MultiplyNodeConverter : NodeConverterBase<MultiplyNode>
    {
        private bool IsMatrix(MaterialXDataType dataType)
        {
            return dataType == MaterialXDataType.Matrix22 || dataType == MaterialXDataType.Matrix33 ||
                   dataType == MaterialXDataType.Matrix44;
        }
        
        
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var inputSlots = new List<MaterialSlot>();
            shaderGraphNode.GetInputSlots(inputSlots);

            var aType = TypeUtil.GetMaterialXDataType(inputSlots[0]);
            var bType = TypeUtil.GetMaterialXDataType(inputSlots[1]);

            // TODO: Support matrix node functionality 
            Assert.IsFalse(IsMatrix(aType) || IsMatrix(bType), "Matrix multiplication conversion is not currently supported");
            
            MaterialXGraphUtil.AddBinaryOperatorNode(MaterialXNodeType.Multiply, shaderGraphNode, graph, stagingEdges);
        }
    }
}