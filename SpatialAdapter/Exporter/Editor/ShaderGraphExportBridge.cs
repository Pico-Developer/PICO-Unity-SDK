using UnityEditor;
using UnityGLTF;
using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    [InitializeOnLoad]
    internal static class ShaderGraphExportBridge
    {
        static ShaderGraphExportBridge()
        {
            GLTFEditorExporter.ExportShaderGraphWithOverride = ShaderGraphExporter.ExportShaderGraph;
        }
    }
}
