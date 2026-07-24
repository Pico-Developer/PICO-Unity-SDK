
using UnityEditor;
using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    internal class SpatialAdapterShaderGraphExporter : SpatialAdapterAssetExporter<int>
    {
        public SpatialAdapterShaderGraphExporter(SpatialAdapterExporterSettings inSettings) : base(inSettings)
        {
        }
        
        public override bool RegisterAsset(string assetPath, string exportedPath, bool isAssetDirty)
        {
            if (!settings.m_exportShaderGraphs)
            {
                return true;
            }

            Shader shader;
            
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset is Shader)
            {
                shader = asset as Shader;
            }
            else
            {
                Debug.LogWarning($"SpatialAdapter ShaderGraph Exporter: Invalid shader at {assetPath}");
                return true;
            }

            int shaderId = shader.GetInstanceID();
            AddExportedPath(shaderId, exportedPath);
            
            if (!isAssetDirty)
            {
                return true;
            }

            ShaderGraphExporter.ExportShaderGraph(assetPath, exportedPath, AssetExportHelpers.GetExportDirectory());
            
            return true;
        }
    }
}