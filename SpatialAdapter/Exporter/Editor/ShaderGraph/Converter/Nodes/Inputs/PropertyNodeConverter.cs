using System.Collections.Generic;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class PropertyNodeConverter : NodeConverterBase<PropertyNode>
    {

        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            var pnode = (PropertyNode)shaderGraphNode;
            var property = pnode.property;

            // Gradients are special and can be statically evaluated, so we do not need them.
            if (property is GradientShaderProperty)
                return;

            // Unique and stable property name, should be identical to the material property name.
            string nodeName = pnode.property.referenceName;

            var slot = shaderGraphNode.FindSlot<MaterialSlot>(0);
            stagingEdges.AddPort(slot.slotReference, nodeName);

            // Property Nodes are special, in that we only need 1 to exist for each property.
            if (graph.HasNode(nodeName))
                return;

            var nodeType = MaterialXNodeType.Constant;
            MaterialXDataType dataType = (pnode.property.propertyType == PropertyType.Color) ?
                MaterialXDataType.Color4 : TypeUtil.GetMaterialXDataType(slot);

            var nodeData = graph.AddNode(nodeName, nodeType, dataType, true);

            if (dataType == MaterialXDataType.Filename)
            {
                try
                {
                    var tprop = (Texture2DShaderProperty)property;
                    var texture = tprop.value.texture;
                    var filename = ConverterUtil.GetExportedTextureReferencePath(graph.FilePath, texture);

                    if (texture != null)
                    {
                        graph.RegisterTexture(texture);
                    }

                    nodeData.AddPortWithStringValue("value", dataType, filename);
                }
                catch
                {
                    // FNF or no texture file was referenced.
                    nodeData.AddPortWithStringValue("value", dataType, "placeholder.png");
                }
            }
            else
            {
                var values = GetDefaultValue(property);
                nodeData.AddPortWithValue("value", dataType, values);
            }
        }


        internal static float[] GetDefaultValue(AbstractShaderProperty property)
        {
            switch (property)
            {
                case ColorShaderProperty c4: return new float[4] { c4.value.r, c4.value.g, c4.value.b, c4.value.a };
                case BooleanShaderProperty b: return new float[1] { b.value ? 1f : 0f };
                case Vector1ShaderProperty f: return new float[1] { f.value };
                case Vector2ShaderProperty v2: return new float[2] { v2.value.x, v2.value.y };
                case Vector3ShaderProperty v3: return new float[3] { v3.value.x, v3.value.y, v3.value.z };
                case Vector4ShaderProperty vd: return new float[4] { vd.value.x, vd.value.y, vd.value.z, vd.value.w };
                case Matrix2ShaderProperty m2:
                    return new float[4]
                    {
                        m2.value.m00, m2.value.m01,
                        m2.value.m10, m2.value.m11,
                    };
                case Matrix3ShaderProperty m3:
                    return new float[9]
                    {
                        m3.value.m00, m3.value.m01, m3.value.m02,
                        m3.value.m10, m3.value.m11, m3.value.m12,
                        m3.value.m20, m3.value.m21, m3.value.m22,
                    };
                case Matrix4ShaderProperty m4:
                    return new float[16]
                    {
                        m4.value.m00, m4.value.m01, m4.value.m02, m4.value.m03,
                        m4.value.m10, m4.value.m11, m4.value.m12, m4.value.m13,
                        m4.value.m20, m4.value.m21, m4.value.m22,m4.value.m23,
                        m4.value.m30, m4.value.m31, m4.value.m32,m4.value.m33,
                    };
                default: return null;
            }
        }
    }
}
