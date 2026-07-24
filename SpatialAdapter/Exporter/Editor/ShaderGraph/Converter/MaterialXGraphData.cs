using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal class MaterialXGraphData
    {
        private Dictionary<string, MaterialXNodeData> _nodeDataLookup = new();

        internal HashSet<Texture> InputTextures { get; private set; } = new();
        internal List<string> InputNodeNames { get; private set; } = new();
        internal List<string> SystemInputNodeNames { get; private set; } = new();

        internal Dictionary<(string dstNode, string dstPort), string> Edges { get; set; } = new();
        
        internal string Name { get; private set; }

        //  The shader's path as it appears at the top of a ShaderLab declaration
        internal string ShaderPath { get; private set; }
        
        //  The path of the actual ShaderGraph file within the project file hierarchy.
        internal string FilePath { get; private set; }
        
        internal IEnumerable<MaterialXNodeData> Nodes => _nodeDataLookup.Values;

        internal MaterialXGraphData(string name, string shaderPath, string filePath)
        {
            Name = name;
            ShaderPath = shaderPath;
            FilePath = filePath;
        }

        internal bool HasNode(string name) => _nodeDataLookup.ContainsKey(name);

        internal bool GetOrAddNode(string name, string nodeType, MaterialXDataType dataType, out MaterialXNodeData node)
        {
            if (_nodeDataLookup.TryGetValue(name, out var existingNode))
            {
                node = existingNode;
                return false;
            }

            node = AddNode(name, nodeType, dataType);
            return true;
        }
        
        internal MaterialXNodeData AddNode(string name, string nodeType, MaterialXDataType dataType, bool isInput = false, bool isSystemInput = false)
        {
            MaterialXNodeData node = new MaterialXNodeData(name, nodeType, dataType);
            _nodeDataLookup.Add(name, node);
            if (isInput)
                InputNodeNames.Add(name);
            if (isSystemInput)
                SystemInputNodeNames.Add(name);
            return node;
        }
        
        internal void AddPortAndEdge(string srcNode, string dstNode, string dstPort, MaterialXDataType dataType)
        {
            _nodeDataLookup[dstNode].AddPort(dstPort, dataType);
            AddEdge(srcNode, dstNode, dstPort);
        }
        
        internal void AddEdge(string srcNode, string dstNode, string dstPort)
            => Edges.Add((dstNode, dstPort), srcNode);
        
        internal bool HasConnection(string dstNode, string dstPort)
            => Edges.ContainsKey((dstNode, dstPort));
        internal string GetConnectedNode(string dstNode, string dstPort)
            => Edges.GetValueOrDefault((dstNode, dstPort));

        internal MaterialXNodeData GetConnectedNodeData(MaterialXNodeData dstNodeData, string dstPort)
        {
            string srcNode = GetConnectedNode(dstNodeData.Name, dstPort);
            return _nodeDataLookup[srcNode];
        }
        
        internal bool TryGetConnectedNode(MaterialXNodeData dstNode, string dstPort, out MaterialXNodeData srcNode)
        {
            if (!Edges.TryGetValue((dstNode.Name, dstPort), out var connectedNodeName))
            {
                srcNode = default;
                return false;
            }

            srcNode = _nodeDataLookup[connectedNodeName];
            return true;
        }
        
        internal MaterialXNodeData GetNode(string nodeName)
            => _nodeDataLookup.GetValueOrDefault(nodeName);

        //  This ensures that texture files are properly exported to the correct path.
        public void RegisterTexture(Texture texture)
        {
            InputTextures.Add(texture);
        }
    }

    internal class MaterialXNodeData
    {
        // input ports
        private Dictionary<string, MaterialXPortData> _portsLookup = new();

        internal string Name { get; set; }

        internal string NodeType { get; set; }

        // output data type
        internal MaterialXDataType DataType { get; set; }

        internal string OutputName { get; set; } = "out";

        internal IEnumerable<MaterialXPortData> Ports => _portsLookup.Values;

        internal MaterialXNodeData(string name, string nodeType, MaterialXDataType dataType)
        {
            Name = name;
            NodeType = nodeType;
            DataType = dataType;
        }

        internal bool HasPort(string name) => _portsLookup.ContainsKey(name);

        internal MaterialXPortData GetPort(string name)
        {
            _portsLookup.TryGetValue(name, out var data);
            return data;
        }

        internal void AddPort(string name, MaterialXDataType type) =>
            _portsLookup.Add(name, new MaterialXPortData(name, type));

        internal void AddPortWithValue(string name, MaterialXDataType type, object value) =>
            _portsLookup.Add(name, new MaterialXPortData(name, type, value));

        internal void AddPortWithStringValue(string name, MaterialXDataType type, string value) =>
            _portsLookup.Add(name, new MaterialXPortData(name, type, value));
    }

    internal class MaterialXPortData
    {
        private static readonly Encoding StringEncoding = Encoding.Unicode;
        internal string Name { get; set; }
        internal MaterialXDataType DataType { get; set; }
        internal byte[] ByteValue { get; private set; }

        internal float[] Value
        {
            get
            {
                if (ByteValue == null)
                    return default;

                BinaryFormatter bf = new BinaryFormatter();
                using MemoryStream ms = new MemoryStream(ByteValue);
                object obj = bf.Deserialize(ms);
                return (float[])obj;
            }
        }

        internal string StringValue
        {
            get
            {
                if (DataType == MaterialXDataType.String || DataType == MaterialXDataType.Filename)
                {
                    return StringEncoding.GetString(ByteValue);
                }
                else
                {
                    return null;
                }
            }
        }

        internal MaterialXPortData(string name, MaterialXDataType dataType)
        {
            Name = name;
            DataType = dataType;
        }

        internal MaterialXPortData(string name, MaterialXDataType dataType, object value) : this(name, dataType)
        {
            if (value == null) return;
            object finalVal = value;
            if (dataType == MaterialXDataType.Boolean && value is bool)
            {
                finalVal = new[] { (bool)value ? 1.0f : 0.0f };
            } else if (dataType == MaterialXDataType.Float && value is float)
            {
                finalVal = new[] { (float)value };
            } else if (dataType == MaterialXDataType.Integer && value is int)
            {
                finalVal = new[] { (float)(int)value };
            }

            BinaryFormatter binaryFormatter = new BinaryFormatter();
            using MemoryStream stream = new MemoryStream();
            binaryFormatter.Serialize(stream, finalVal);
            ByteValue = stream.ToArray();
        }

        internal MaterialXPortData(string name, MaterialXDataType dataType, string stringValue) : this(name, dataType)
        {
            ByteValue = StringEncoding.GetBytes(stringValue);
        }
    }
}