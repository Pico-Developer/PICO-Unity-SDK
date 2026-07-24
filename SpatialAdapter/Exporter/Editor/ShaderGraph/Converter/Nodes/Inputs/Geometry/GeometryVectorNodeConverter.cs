
using System.Collections.Generic;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using System;
using NUnit.Framework;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    abstract class GeometryVectorNodeConverter<T> : NodeConverterBase<T>
        where T: GeometryNode
    {
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            Dictionary<string, MaterialXDataType> inputNamesTypes = new Dictionary<string, MaterialXDataType>()
            {
                ["Out"] = MaterialXDataType.Vector3,
            };

            Dictionary<string, NodeDef> ast;

            switch (((GeometryNode)shaderGraphNode).space)
            {
                case CoordinateSpace.View:
                    ast = new()
                    {
                        ["Out"] = new(MaterialXNodeType.Normalize, MaterialXDataType.Vector3, "Out", new()
                        {
                            ["in"] = new InlineInputNodeDef(MaterialXNodeType.Convert, MaterialXDataType.Vector3, new()
                            {
                                ["in"] = new InlineInputNodeDef(MaterialXNodeType.TransformMatrix, MaterialXDataType.Vector4, new()
                                {
                                    // Convert to vector4, then zero out w component to transform as vector.
                                    ["in"] = new InlineInputNodeDef(MaterialXNodeType.Multiply, MaterialXDataType.Vector4, new()
                                    {
                                        ["in1"] = new InlineInputNodeDef(MaterialXNodeType.Convert, MaterialXDataType.Vector4, new()
                                        {
                                            ["in"] = new InlineInputNodeDef(NodeType, MaterialXDataType.Vector3, new()
                                            {
                                                ["space"] = new StringInputNodeDef("object"),
                                            }),
                                        }),
                                        ["in2"] = new FloatInputNodeDef(MaterialXDataType.Vector4, 1.0f, 1.0f, 1.0f, 0.0f),
                                    }),
                                    ["mat"] = new PerStageInputNodeDef(
                                        new InlineInputNodeDef(
                                            MaterialXNodeType.PicoGeometryModifierModelToView,
                                            MaterialXDataType.Matrix44, new(), "modelToView"),
                                        new InlineInputNodeDef(
                                            MaterialXNodeType.RealityKitSurfaceModelToView,
                                            MaterialXDataType.Matrix44, new(), "modelToView")),
                                }),
                            }),
                        }),
                    };
                    break;

                case CoordinateSpace.Tangent:
                    // Tangent space vectors don't need to be flipped.
                    ast = new()
                    {
                        ["Out"] = new(NodeType, MaterialXDataType.Vector3, "Out", new()
                        {
                            ["space"] = new StringInputNodeDef("tangent"),
                        }),
                    };
                    break;

                default:
                    ast = new()
                    {
                        // Flip z coordinate to convert RealityKit space to Unity space.
                        ["Out"] = new(MaterialXNodeType.Multiply, MaterialXDataType.Vector3, "Out", new()
                        {
                            ["in1"] = new InlineInputNodeDef(NodeType, MaterialXDataType.Vector3, new()
                            {
                                // TODO: Implement positoin adapter
                                //["space"] = new StringInputNodeDef(PositionAdapter.SpaceToMtlxString(space)),
                            }),
                            ["in2"] = new FloatInputNodeDef(MaterialXDataType.Vector3, new[] { 1.0f, 1.0f, -1.0f }),
                        }),
                    };
                    break;
            }
            List<MaterialSlot> outputSlots = new();
            shaderGraphNode.GetOutputSlots(outputSlots);
            CompoundContext context = new CompoundContext(shaderGraphNode, graph, stagingEdges, Hint, ast);

            CustomFunctionHelper.CombineNodeDefs(context, ast, outputSlots);
        }
        protected abstract string Hint { get; }
        protected abstract string NodeType { get; }
    }

    static class GeomHelpers
    {
        public static string GetStringSpace(CoordinateSpace coordinateSpace)
        {
            string space = "";
            switch (coordinateSpace)
            {
                case CoordinateSpace.Object:
                    break;
                case CoordinateSpace.View:
                    Assert.Fail("Unable to use view space for Spatial Editor");
                    break;
                case CoordinateSpace.World:
                    space = "world";
                    break;
                case CoordinateSpace.Tangent:
                    space = "tangent";
                    break;
                case CoordinateSpace.AbsoluteWorld:
                    space = "world"; // TODO: Add absolute node connection
                    Debug.LogWarning("Defaulting absolute world space to world space");
                    break;
                case CoordinateSpace.Screen:
                    Assert.Fail("Unable to use screen space for Spatial Editor");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return space;
        }
    }
    
    
}