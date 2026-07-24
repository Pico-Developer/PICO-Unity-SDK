using UnityEditor.ShaderGraph;
using System.Collections.Generic;
using NUnit.Framework;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class DotProductNodeConverter : NodeConverterBase<DotProductNode>
    {
        private bool IsVector(MaterialXDataType dataType)
        {
            return dataType == MaterialXDataType.Float || dataType == MaterialXDataType.Vector2 || dataType == MaterialXDataType.Vector3 ||
                   dataType == MaterialXDataType.Vector4;
        }
        
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            // Ensure types are the same kind of vector
            var inputSlots = new List<MaterialSlot>();
            shaderGraphNode.GetInputSlots(inputSlots);

            var aType = TypeUtil.GetMaterialXDataType(inputSlots[0]);
            var bType = TypeUtil.GetMaterialXDataType(inputSlots[1]);
            
            Assert.IsTrue(IsVector(aType), "Invalid input A for distance node");
            Assert.IsTrue(IsVector(bType), "Invalid input B for distance node");

            //MaterialXDataType smallestVector = (aType > bType) ? bType : aType;
            Dictionary<string, string> portMap = new()
            {
                { "A", "in1" },
                { "B", "in2" }
            };
            MaterialXGraphUtil.AddNaryOperatorNode(MaterialXNodeType.DotProduct, shaderGraphNode, graph, stagingEdges, "DotProduct", portMap);
        }
    }
}