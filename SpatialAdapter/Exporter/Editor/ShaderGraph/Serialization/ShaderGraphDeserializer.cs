using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Serialization;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal static class ShaderGraphDeserializer
    {
        internal static GraphData LoadGraphData(string path)
        {
            var fileContents = File.ReadAllText(path, Encoding.UTF8);
            var assetGuid = AssetDatabase.AssetPathToGUID(path);
            var isSubGraph = Path.GetExtension(path) == ".shadersubgrpah";

            var graphData = new GraphData
            {
                assetGuid = assetGuid,
                isSubGraph = isSubGraph,
                messageManager = null
            };
            
            MultiJson.Deserialize(graphData, fileContents);
            graphData.OnEnable();
            graphData.ValidateGraph();
            return graphData;
        }
    }
}