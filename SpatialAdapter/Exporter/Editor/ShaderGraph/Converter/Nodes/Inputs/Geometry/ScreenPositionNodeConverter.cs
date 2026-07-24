using System.Collections.Generic;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine.UIElements;
namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class ScreenPositionNodeConverter : NodeConverterBase<ScreenPositionNode>
    {
        internal static MaterialXNodeData SetupScreenSpace(ScreenSpaceType type, MaterialXGraphData graph)
        {
            if (graph.GetOrAddNode("ClipPosition", MaterialXNodeType.TransformMatrix, MaterialXDataType.Vector4, out var clipPos))
            {
                if (graph.GetOrAddNode("WorldSpacePosition3", MaterialXNodeType.GeomPosition, MaterialXDataType.Vector3, out var wPos))
                    wPos.AddPortWithStringValue("space", MaterialXDataType.String, "world");

                if (graph.GetOrAddNode("WorldSpacePosition4", MaterialXNodeType.Convert, MaterialXDataType.Vector4, out var wPos4))
                    graph.AddPortAndEdge(wPos.Name, wPos4.Name, "in", MaterialXDataType.Vector3);

                if (graph.GetOrAddNode("ViewProjection", MaterialXNodeType.Multiply, MaterialXDataType.Matrix44, out var viewProj))
                {
                    MaterialXGraphUtil.InitializeImplicitProperty(SpatialAdapterShaderGlobals.ViewMatrix, MaterialXDataType.Matrix44, graph);
                    MaterialXGraphUtil.InitializeImplicitProperty(SpatialAdapterShaderGlobals.ProjectionMatrix, MaterialXDataType.Matrix44, graph);
                    graph.AddPortAndEdge(SpatialAdapterShaderGlobals.ViewMatrix, viewProj.Name, "in1", MaterialXDataType.Matrix44);
                    graph.AddPortAndEdge(SpatialAdapterShaderGlobals.ProjectionMatrix, viewProj.Name, "in2", MaterialXDataType.Matrix44);
                }

                graph.AddPortAndEdge(wPos4.Name, clipPos.Name, "in", MaterialXDataType.Vector4);
                graph.AddPortAndEdge(viewProj.Name, clipPos.Name, "mat", MaterialXDataType.Matrix44);
            }
            if (type == ScreenSpaceType.Pixel)
                return clipPos;

            if (type == ScreenSpaceType.Raw) // clip/.w => .xy*.5+.5 => .xyz
            {
                if (graph.GetOrAddNode("ScreenPositionRaw", MaterialXNodeType.Combine4, MaterialXDataType.Vector4, out var raw))
                {
                    var clipW = graph.AddNode("ClipPositionW", MaterialXNodeType.Swizzle, MaterialXDataType.Float);
                    graph.AddPortAndEdge(clipPos.Name, clipW.Name, "in", MaterialXDataType.Vector4);
                    clipW.AddPortWithStringValue("channels", MaterialXDataType.Vector4, "w");

                    var clipH = graph.AddNode("ClipPositionHomogenized", MaterialXNodeType.Divide, MaterialXDataType.Vector4);
                    graph.AddPortAndEdge(clipPos.Name, clipH.Name, "in1", MaterialXDataType.Vector4);
                    graph.AddPortAndEdge(clipW.Name, clipH.Name, "in2", MaterialXDataType.Float);

                    var clipHxy = graph.AddNode("ClipPositionHomoXY", MaterialXNodeType.Swizzle, MaterialXDataType.Vector2);
                    graph.AddPortAndEdge(clipH.Name, clipHxy.Name, "in", MaterialXDataType.Vector4);
                    clipHxy.AddPortWithStringValue("channels", MaterialXDataType.String, "xy");

                    var clipHzw = graph.AddNode("ClipPositionHomoZW", MaterialXNodeType.Swizzle, MaterialXDataType.Vector2);
                    graph.AddPortAndEdge(clipH.Name, clipHzw.Name, "in", MaterialXDataType.Vector4);
                    clipHzw.AddPortWithStringValue("channels", MaterialXDataType.String, "zw");

                    var scaled = graph.AddNode("ClipPositionHomoXYScaled", MaterialXNodeType.Multiply, MaterialXDataType.Vector2);
                    graph.AddPortAndEdge(clipHxy.Name, scaled.Name, "in1", MaterialXDataType.Vector2);
                    scaled.AddPortWithValue("in2", MaterialXDataType.Float, new float[] { .5f, .5f });

                    var offset = graph.AddNode("ClipPositionHomoXYOffset", MaterialXNodeType.Add, MaterialXDataType.Vector2);
                    graph.AddPortAndEdge(scaled.Name, offset.Name, "in1", MaterialXDataType.Vector2);
                    offset.AddPortWithValue("in2", MaterialXDataType.Float, new float[] { .5f, .5f });

                    // REALLY? Combining two vec2's should work in the spec, but it fails in the reference implementation...
                    // so we're really going to fully decompose and rebuild using floats.
                    var x = graph.AddNode("ClipPositionRawX", MaterialXNodeType.Swizzle, MaterialXDataType.Float);
                    var y = graph.AddNode("ClipPositionRawY", MaterialXNodeType.Swizzle, MaterialXDataType.Float);
                    var z = graph.AddNode("ClipPositionRawZ", MaterialXNodeType.Swizzle, MaterialXDataType.Float);
                    var w = graph.AddNode("ClipPositionRawW", MaterialXNodeType.Swizzle, MaterialXDataType.Float);
                    x.AddPortWithStringValue("channels", MaterialXDataType.String, "x");
                    y.AddPortWithStringValue("channels", MaterialXDataType.String, "y");
                    z.AddPortWithStringValue("channels", MaterialXDataType.String, "x");
                    w.AddPortWithStringValue("channels", MaterialXDataType.String, "y");
                    graph.AddPortAndEdge(offset.Name, x.Name, "in", MaterialXDataType.Vector2);
                    graph.AddPortAndEdge(offset.Name, y.Name, "in", MaterialXDataType.Vector2);
                    graph.AddPortAndEdge(clipHzw.Name, z.Name, "in", MaterialXDataType.Vector2);
                    graph.AddPortAndEdge(clipHzw.Name, w.Name, "in", MaterialXDataType.Vector2);


                    graph.AddPortAndEdge(x.Name, raw.Name, "in1", MaterialXDataType.Float);
                    graph.AddPortAndEdge(y.Name, raw.Name, "in2", MaterialXDataType.Float);
                    graph.AddPortAndEdge(z.Name, raw.Name, "in3", MaterialXDataType.Float);
                    graph.AddPortAndEdge(w.Name, raw.Name, "in4", MaterialXDataType.Float);
                }
                return raw;
            }

            MaterialXGraphUtil.InitializeImplicitProperty(SpatialAdapterShaderGlobals.ScreenParams, MaterialXDataType.Vector4, graph);
            if (graph.GetOrAddNode("ScreenWidth", MaterialXNodeType.Swizzle, MaterialXDataType.Float, out var screenWidth))
            {
                screenWidth.AddPortWithStringValue("channels", MaterialXDataType.String, "x");
                graph.AddPortAndEdge(SpatialAdapterShaderGlobals.ScreenParams, screenWidth.Name, "in", MaterialXDataType.Vector4);
            }
            if (graph.GetOrAddNode("ScreenHeight", MaterialXNodeType.Swizzle, MaterialXDataType.Float, out var screenHeight))
            {
                screenHeight.AddPortWithStringValue("channels", MaterialXDataType.String, "y");
                graph.AddPortAndEdge(SpatialAdapterShaderGlobals.ScreenParams, screenHeight.Name, "in", MaterialXDataType.Vector4);
            }

            if (graph.GetOrAddNode("NDCPosition", MaterialXNodeType.Divide, MaterialXDataType.Vector4, out var NDCPos)) // clip / screenDim
            {
                if (graph.GetOrAddNode("ScreenDimension4", MaterialXNodeType.Combine4, MaterialXDataType.Vector4, out var screenDim))
                {
                    graph.AddPortAndEdge("ScreenWidth", screenDim.Name, "in1", MaterialXDataType.Float);
                    graph.AddPortAndEdge("ScreenHeight", screenDim.Name, "in2", MaterialXDataType.Float);
                    screenDim.AddPortWithValue("in3", MaterialXDataType.Float, new float[] { 1 });
                    screenDim.AddPortWithValue("in4", MaterialXDataType.Float, new float[] { 1 });
                }

                graph.AddPortAndEdge(clipPos.Name, NDCPos.Name, "in1", MaterialXDataType.Vector4);
                graph.AddPortAndEdge(screenDim.Name, NDCPos.Name, "in2", MaterialXDataType.Vector4);
            }
            if (type == ScreenSpaceType.Default)
                return NDCPos;

            // ndc*2-1
            if (graph.GetOrAddNode("ScreenPositionCenter", MaterialXNodeType.Subtract, MaterialXDataType.Vector4, out var center))
            {
                var doubleCenterNode = graph.AddNode("ScreenPositionCenteredPreMult", MaterialXNodeType.Multiply, MaterialXDataType.Vector4);
                graph.AddPortAndEdge(NDCPos.Name, doubleCenterNode.Name, "in1", MaterialXDataType.Vector4);
                doubleCenterNode.AddPortWithValue("in2", MaterialXDataType.Float, new float[] { 2 });

                graph.AddPortAndEdge(doubleCenterNode.Name, center.Name, "in1", MaterialXDataType.Vector4);
                center.AddPortWithValue("in2", MaterialXDataType.Float, new float[] { 1 });
            }
            if (type == ScreenSpaceType.Center)
                return center;

            // (frac(center.x*screenDim.x/screenDim.y, center.y), center.z, center.w)
            if (graph.GetOrAddNode("ScreenPositionTiled", MaterialXNodeType.Combine4, MaterialXDataType.Vector4, out var tiled))
            {
                var cx = graph.AddNode("ScreenPositionCenterX", MaterialXNodeType.Swizzle, MaterialXDataType.Float);
                var cy = graph.AddNode("ScreenPositionCenterY", MaterialXNodeType.Swizzle, MaterialXDataType.Float);
                var fzw = graph.AddNode("ScreenPositionCenterZW", MaterialXNodeType.Swizzle, MaterialXDataType.Vector2);

                cx.AddPortWithStringValue("channels", MaterialXDataType.String, "x");
                cy.AddPortWithStringValue("channels", MaterialXDataType.String, "y");
                fzw.AddPortWithStringValue("channels", MaterialXDataType.String, "xy");


                graph.AddPortAndEdge(center.Name, cx.Name, "in", MaterialXDataType.Vector4);
                graph.AddPortAndEdge(center.Name, cy.Name, "in", MaterialXDataType.Vector4);
                graph.AddPortAndEdge(center.Name, fzw.Name, "in", MaterialXDataType.Vector4);

                var aspect = graph.AddNode("ScreenPositionAspectRatio", MaterialXNodeType.Divide, MaterialXDataType.Float); // aspect = screenWidth/screenHeight
                graph.AddPortAndEdge("ScreenWidth", aspect.Name, "in1", MaterialXDataType.Float);
                graph.AddPortAndEdge("ScreenHeight", aspect.Name, "in2", MaterialXDataType.Float);

                var csx = graph.AddNode("ScreenPositionCenterScaledX", MaterialXNodeType.Multiply, MaterialXDataType.Float); // x * aspect
                graph.AddPortAndEdge(cx.Name, csx.Name, "in1", MaterialXDataType.Float);
                graph.AddPortAndEdge(aspect.Name, csx.Name, "in2", MaterialXDataType.Float);

                var cxy = graph.AddNode("ScreenPositionCenterScaledXY", MaterialXNodeType.Combine2, MaterialXDataType.Vector2); // vector2(x*aspect, y)
                graph.AddPortAndEdge(csx.Name, cxy.Name, "in1", MaterialXDataType.Float);
                graph.AddPortAndEdge(cy.Name, cxy.Name, "in2", MaterialXDataType.Float);

                // fract => x - floor(x)
                var csxy = graph.AddNode("ScreenPositionCenterFloorXY", MaterialXNodeType.Floor, MaterialXDataType.Vector2);
                graph.AddPortAndEdge(cxy.Name, csxy.Name, "in", MaterialXDataType.Vector2);

                var fxy = graph.AddNode("ScreenPositionCenterSubXY", MaterialXNodeType.Subtract, MaterialXDataType.Vector2);
                graph.AddPortAndEdge(cxy.Name, fxy.Name, "in1", MaterialXDataType.Vector2);
                graph.AddPortAndEdge(csxy.Name, fxy.Name, "in2", MaterialXDataType.Vector2);


                var x = graph.AddNode("ScreenPositionTiledX", MaterialXNodeType.Swizzle, MaterialXDataType.Float);
                var y = graph.AddNode("ScreenPositionTiledY", MaterialXNodeType.Swizzle, MaterialXDataType.Float);
                var z = graph.AddNode("ScreenPositionTiledZ", MaterialXNodeType.Swizzle, MaterialXDataType.Float);
                var w = graph.AddNode("ScreenPositionTiledW", MaterialXNodeType.Swizzle, MaterialXDataType.Float);
                x.AddPortWithStringValue("channels", MaterialXDataType.String, "x");
                y.AddPortWithStringValue("channels", MaterialXDataType.String, "y");
                z.AddPortWithStringValue("channels", MaterialXDataType.String, "x");
                w.AddPortWithStringValue("channels", MaterialXDataType.String, "y");
                graph.AddPortAndEdge(fxy.Name, x.Name, "in", MaterialXDataType.Vector2);
                graph.AddPortAndEdge(fxy.Name, y.Name, "in", MaterialXDataType.Vector2);
                graph.AddPortAndEdge(fzw.Name, z.Name, "in", MaterialXDataType.Vector2);
                graph.AddPortAndEdge(fzw.Name, w.Name, "in", MaterialXDataType.Vector2);


                graph.AddPortAndEdge(x.Name, tiled.Name, "in1", MaterialXDataType.Float);
                graph.AddPortAndEdge(y.Name, tiled.Name, "in2", MaterialXDataType.Float);
                graph.AddPortAndEdge(z.Name, tiled.Name, "in3", MaterialXDataType.Float);
                graph.AddPortAndEdge(w.Name, tiled.Name, "in4", MaterialXDataType.Float);
            }
            return tiled;
        }
        public override void Convert(AbstractMaterialNode shaderGraphNode, MaterialXGraphData graph, StagingEdges stagingEdges)
        {
            if (shaderGraphNode is not ScreenPositionNode snode)
                return;

            //var nodeData = SetupScreenSpace(snode.screenSpaceType, graph);
            //stagingEdges.AddPort(ShaderGraphUtil.SlotUtil.GetPrimaryOutputSlot(shaderGraphNode).slotReference, nodeData.Name);

            // Reality kit extension

            // For pixel coordinates, RealityKit provides a custom node.
            if (snode.screenSpaceType == ScreenSpaceType.Pixel)
            {
                var nodeData = MaterialXGraphUtil.AddNaryOperatorNode(
                                    MaterialXNodeType.RealityKitSurfaceScreenPosition, shaderGraphNode, graph, stagingEdges, "ScreenPosition");
                return;
            }


            NodeDef baseInputDef = new(MaterialXNodeType.TransformMatrix, MaterialXDataType.Vector4, "out", new()
            {
                ["in"] = new InlineInputNodeDef(MaterialXNodeType.TransformMatrix, MaterialXDataType.Vector4, new()
                {
                    ["in"] = new InlineInputNodeDef(MaterialXNodeType.Convert, MaterialXDataType.Vector4, new()
                    {
                        ["in"] = new InlineInputNodeDef(MaterialXNodeType.GeomPosition, MaterialXDataType.Vector3, new()
                        {
                            ["space"] = new StringInputNodeDef("object"),
                        }),
                    }),
                    ["mat"] = new PerStageInputNodeDef(
            new InlineInputNodeDef(
                MaterialXNodeType.PicoGeometryModifierModelToView,
                MaterialXDataType.Matrix44, new(), "modelToView"),
            new InlineInputNodeDef(
                MaterialXNodeType.RealityKitSurfaceModelToView,
                MaterialXDataType.Matrix44, new(), "modelToView")),
                }),
                ["mat"] = new PerStageInputNodeDef(
        new InlineInputNodeDef(
            MaterialXNodeType.RealityKitGeometryModifierViewToProjection,
            MaterialXDataType.Matrix44, new(), "viewToProjection"),
        new InlineInputNodeDef(
            MaterialXNodeType.RealityKitSurfaceViewToProjection,
            MaterialXDataType.Matrix44, new(), "viewToProjection")),
            });

            if (snode.screenSpaceType == ScreenSpaceType.Raw)
            {
                // throw unimplemented
                return;
            }

            float scale = 0.5f;
            NodeDef centerDef = new(MaterialXNodeType.Multiply, MaterialXDataType.Vector4, "out", new()
            {
                ["in1"] = new InlineInputNodeDef(MaterialXNodeType.Divide, MaterialXDataType.Vector4, new()
                {
                    ["in1"] = new InternalInputNodeDef("Base"),
                    ["in2"] = new InlineInputNodeDef(MaterialXNodeType.Swizzle, MaterialXDataType.Float, new()
                    {
                        ["in"] = new InternalInputNodeDef("Base"),
                        ["channels"] = new StringInputNodeDef("w"),
                    }),
                }),
                ["in2"] = new FloatInputNodeDef(MaterialXDataType.Vector4, scale, scale, 0.0f, 0.0f),
            });

            Dictionary<string, NodeDef> abstractSyntaxTreeNode = new(){
                ["Base"] = baseInputDef,
                ["Out"] = new(MaterialXNodeType.Add, MaterialXDataType.Vector4, "Out", new()
                    {
                        ["in1"] = new InlineInputNodeDef(centerDef),
                        ["in2"] = new FloatInputNodeDef(MaterialXDataType.Vector4, 0.5f, 0.5f, 0.0f, 0.0f),
                    }),
            };

            List<MaterialSlot> outputSlots = new();
            shaderGraphNode.GetOutputSlots(outputSlots);
            CompoundContext context = new CompoundContext(shaderGraphNode, graph, stagingEdges, "ScreenPosition", abstractSyntaxTreeNode);

            CustomFunctionHelper.CombineNodeDefs(context, abstractSyntaxTreeNode, outputSlots);

        }
    }
}