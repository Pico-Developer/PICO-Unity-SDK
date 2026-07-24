using System;
using System.ComponentModel;
using System.Reflection;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    public class MaterialXNodeType
    {
        // Extracted from https://github.com/AcademySoftwareFoundation/MaterialX/blob/main/documents/Specification/MaterialX.Specification.md#nodes
        
        // Material
        internal const string Material = "surfacematerial";
        
        // ----- Standard Source Nodes -----
        // Texture Nodes
        internal const string Image = "image";
        internal const string TiledImage = "tiledimage";
        internal const string TriplanarProjection = "triplanarprojection";

        // Procedural Nodes
        internal const string Constant = "constant";
        internal const string RampLR = "ramplr";
        internal const string RampTB = "ramptb";
        internal const string Ramp4 = "ramp4";
        internal const string SplitLR = "splitlr";
        internal const string SplitTB = "splittb";
        internal const string RandomFloat = "randomfloat";
        internal const string RandomColor = "randomcolor";

        // Noise Nodes
        internal const string Noise2D = "noise2d";
        internal const string Noise3D = "noise3d";
        internal const string Fractal3D = "fractal3d";
        internal const string CellNoise2D = "cellnoise2d";
        internal const string CellNoise3D = "cellnoise3d";
        internal const string WorleyNoise2D = "worleynoise2d";
        internal const string WorleyNoise3D = "worleynoise3d";
        internal const string UnifiedNoise2D = "unifiednoise2d";
        internal const string UnifiedNoise3D = "unifiednoise3d";
        
        // Geometric Nodes
        internal const string GeomPosition = "position";
        internal const string GeomNormal = "normal";
        internal const string GeomTangent = "tangent";
        internal const string GeomBitangent = "bitangent";
        internal const string GeomBump = "bump";
        internal const string GeomTexCoord = "texcoord";
        internal const string GeomColor = "geomcolor";
        internal const string GeomPropValue = "geompropvalue";
        internal const string GeomPropValueUniform = "geompropvalueuniform";
        internal const string GeomViewDirection = "viewdirection";
            
        // Application Nodes
        internal const string Frame = "frame";
        internal const string Time = "time";
        
        // ----- Standard Operator Nodes -----
        // Math Nodes
        internal const string Add = "add";
        internal const string Subtract = "subtract";
        internal const string Multiply = "multiply";
        internal const string Divide = "divide";
        internal const string Modulo = "modulo";
        internal const string Invert = "invert";
        internal const string AbsVal = "absval";
        internal const string Sign = "sign";
        internal const string Floor = "floor";
        internal const string Ceil = "ceil";
        internal const string Round = "round";
        internal const string Power = "power";
        internal const string SafePower = "safepower";
        internal const string Sine = "sin";
        internal const string Cosine = "cos";
        internal const string Tangent = "tan";
        internal const string Arcsine = "asin";
        internal const string Arccosine = "acos";
        internal const string Arctangent2 = "atan2";
        internal const string SquareRoot = "sqrt";
        internal const string NaturalLog = "ln";
        internal const string Exponential = "exp";
        internal const string Clamp = "clamp";
        internal const string TriangleWave = "trianglewave";
        internal const string Min = "min";
        internal const string Max = "max";
        internal const string Normalize = "normalize";
        internal const string Magnitude = "magnitude";
        internal const string Distance = "distance";
        internal const string DotProduct = "dotproduct";
        internal const string CrossProduct = "crossproduct";
        internal const string TransformPoint = "transformpoint";
        internal const string TransformVector = "transformvector";
        internal const string TransformNormal = "transformnormal";
        internal const string TransformMatrix = "transformmatrix";
        internal const string NormalMap = "normalmap";
        internal const string CreateMatrix = "creatematrix";
        internal const string Transpose = "transpose";
        internal const string Determinant = "determinant";
        internal const string InvertMatrix = "invertmatrix";
        internal const string Rotate2D = "rotate2d";
        internal const string Rotate3D = "rotate3d";
        internal const string Reflect = "reflect";
        internal const string Refract = "refract";
        internal const string Place2D = "place2d";
        internal const string Dot = "dot";

        // Logical Operator Nodes
        internal const string And = "and";
        internal const string Or = "or";
        internal const string Xor = "xor";
        internal const string Not = "not";

        // Adjustment Nodes
        internal const string Contrast = "contrast";
        internal const string Remap = "remap";
        internal const string Range = "range";
        internal const string Step = "step";
        internal const string SmoothStep = "smoothstep";
        internal const string Luminance = "luminance";
        internal const string RGBToHSV = "rgbtohsv";
        internal const string HSVToRGB = "hsvtorgb";
        internal const string HSVAdjust = "hsvadjust";
        internal const string Saturate = "saturate";
        internal const string ColorCorrect = "colorcorrect";

        // Compositing Nodes
        internal const string Preult = "premult";
        internal const string Unpremult = "unpremult";
        
        // Mix Node
        internal const string Mix = "mix";
        
        // Conditional Nodes
        internal const string IfGreater = "ifgreater";
        internal const string IfGreaterEq = "ifgreatereq";
        internal const string IfEqual = "ifequal";
        internal const string Switch = "switch";
        
        // Channel Nodes
        internal const string Extract = "extract";
        internal const string Convert = "convert";
        internal const string Combine2 = "combine2";
        internal const string Combine3 = "combine3";
        internal const string Combine4 = "combine4";
        internal const string Separate2 = "separate2";
        internal const string Separate3 = "separate3";
        internal const string Separate4 = "separate4";

        // TODO(xutong.zhou) Confirm this is supported properly as this is not in all MaterialX specifications
        internal const string Swizzle = "swizzle";
        
        // Custom not types
        // TODO: Convert to OS6 custom nodes
        internal const string PicoUnlit = "realitykit_unlit";
        internal const string PicoPbr = "realitykit_pbr";
        internal const string PicoGeometryModifierModelToView = "realitykit_geometry_modifier_model_to_view";
        internal const string RealityKitSurfaceModelToView = "realitykit_surface_model_to_view";
        internal const string RealityKitGeometryModifierViewToProjection = "realitykit_geometry_modifier_view_to_projection";
        internal const string RealityKitSurfaceViewToProjection = "realitykit_surface_view_to_projection";
        internal const string RealityKitSurfaceScreenPosition = "realitykit_surface_screen_position";
        internal const string RealityKitViewDirection = "realitykit_viewdirection";
        internal const string RealityKitReflect = "realitykit_reflect";

        //  Texture/Image Nodes
        internal const string MaterialXImage = "image";
        internal const string MaterialXTiledImage = "tiledimage";

        // USD
        internal const string USDPreviewSurface = "UsdPreviewSurface";
        internal const string USDPrimvarReader = "UsdPrimvarReader";
        internal const string USDUVTexture = "USDUVTexture";
                
        // Geometry
        //  'GeometryModifier' is a non-standard node - RealityKit and OS6 define their own versions.
        internal const string GeometryModifierNode = "geometrymodifier";
        internal const string GeometryModification = "GeometryModification";
    }

    internal enum MaterialXDataType
    {
        [Description("unsupported")] Unsupported,
        [Description("displacementshader")] Displacement,
        [Description("vertexshader")] Vertex, // RealityKit and OS6 custom node type
        [Description("surfaceshader")] Surface,
        [Description("material")] Material,
        [Description("integer")] Integer,
        [Description("boolean")] Boolean,
        [Description("float")] Float,
        [Description("vector2")] Vector2,
        [Description("vector3")] Vector3,
        [Description("vector4")] Vector4,
        [Description("color3")] Color3,
        [Description("color4")] Color4,
        [Description("matrix22")] Matrix22,
        [Description("matrix33")] Matrix33,
        [Description("matrix44")] Matrix44,
        [Description("filename")] Filename,
        [Description("string")] String,
        [Description("floatarray")] FloatArray,
        [Description("color4array")] Color4Array,
    }
    
    internal static class MaterialXDataTypeExtensions
    {
        // Extension method to get the description of the enum value
        internal static string ToTypeString(this MaterialXDataType dataType)
        {
            FieldInfo field = dataType.GetType().GetField(dataType.ToString());
            DescriptionAttribute attribute = (DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));

            return attribute == null ? dataType.ToString() : attribute.Description;
        }
        
        internal static int GetSizeInByte(this MaterialXDataType dataType) => dataType switch
        {
            MaterialXDataType.Integer => 4,
            MaterialXDataType.Boolean => 4, // use 4 bytes for bool on purpose
            MaterialXDataType.Float => 4,
            MaterialXDataType.Vector2 => 8,
            MaterialXDataType.Vector3 => 12,
            MaterialXDataType.Color3 => 12,
            MaterialXDataType.Vector4 => 16,
            MaterialXDataType.Color4 => 16,
            MaterialXDataType.Matrix22 => 16,
            MaterialXDataType.Matrix33 => 36,
            MaterialXDataType.Matrix44 => 64,
            _ => 0,
        };
        internal static int GetLength(MaterialXDataType datatype) => datatype switch
        {
            MaterialXDataType.Integer => 1,
            MaterialXDataType.Boolean => 1,
            MaterialXDataType.Float => 1,
            MaterialXDataType.Vector2 => 2,
            MaterialXDataType.Vector3 => 3,
            MaterialXDataType.Color3 => 3,
            MaterialXDataType.Vector4 => 4,
            MaterialXDataType.Color4 => 4,
            MaterialXDataType.Matrix22 => 4,
            MaterialXDataType.Matrix33 => 9,
            MaterialXDataType.Matrix44 => 16,
            _ => 0,
        };

        
        internal static int GetElementLength(this MaterialXDataType dataType) => dataType switch
        {
            MaterialXDataType.Matrix22 => 2,
            MaterialXDataType.Matrix33 => 3,
            MaterialXDataType.Matrix44 => 4,
            MaterialXDataType.Color4Array => 4,
            _ => 1,
        };


        internal static MaterialXDataType GetTypeOfLength(int length) => length switch
        {
            1 => MaterialXDataType.Float,
            2 => MaterialXDataType.Vector2,
            3 => MaterialXDataType.Vector3,
            4 => MaterialXDataType.Vector4,
            9 => MaterialXDataType.Matrix33,
            16 => MaterialXDataType.Matrix44,
            _ => MaterialXDataType.Unsupported,
        };

        internal static bool IsColor(this MaterialXDataType dataType) =>
            dataType == MaterialXDataType.Color3 || dataType == MaterialXDataType.Color4 ||
            dataType == MaterialXDataType.Color4Array;

        internal static bool IsVector(this MaterialXDataType dataType) =>
            dataType == MaterialXDataType.Vector2 ||
            dataType == MaterialXDataType.Vector3 ||
            dataType == MaterialXDataType.Vector4;

        internal static bool IsString(this MaterialXDataType dataType) =>
            dataType == MaterialXDataType.String;

        internal static bool IsFileName(this MaterialXDataType dataType) =>
            dataType == MaterialXDataType.Filename;

        internal static bool IsMatrix(this MaterialXDataType dataType) => dataType.ToTypeString().Contains("matrix");

        internal static int ChannelCount(this MaterialXDataType dataType) => dataType.GetSizeInByte() / 4;

        internal static bool IsScalar(this MaterialXDataType dataType) => dataType.GetSizeInByte() == 4;

        internal static bool IsArray(this MaterialXDataType dataType) => dataType.ToTypeString().Contains("array");

        internal static MaterialXDataType GetTypeForHlsl(string hlsl) => hlsl switch
        {
            "float" => MaterialXDataType.Float,
            "float2" => MaterialXDataType.Vector2,
            "float3" => MaterialXDataType.Vector3,
            "float4" => MaterialXDataType.Vector4,
            "float2x2" => MaterialXDataType.Matrix22,
            "float3x3" => MaterialXDataType.Matrix33,
            "float4x4" => MaterialXDataType.Matrix44,
            _ => MaterialXDataType.Unsupported,
        };

    }
}