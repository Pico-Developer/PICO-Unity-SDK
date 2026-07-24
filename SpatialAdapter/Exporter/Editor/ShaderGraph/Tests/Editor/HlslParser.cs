using System.Collections;
using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class HlslParser
{
    // A Test behaves as an ordinary method
    [Test]
    public void Assignment()
    {
        Dictionary<string, MaterialXDataType> input = new() 
        {["fA"] = MaterialXDataType.Float};
        var outputNodeDef = CustomFunctionHelper.ParseHlsl(input, "fOut = fA;");
    }
    [Test]
    public void Frensnel()
    {
        Dictionary<string, MaterialXDataType> input = new() 
        {["Normal"] = MaterialXDataType.Vector3,
        ["ViewDir"] = MaterialXDataType.Vector3,
        ["Power"] = MaterialXDataType.Float};

        var outputNodeDef = CustomFunctionHelper.ParseHlsl(input, "Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power)");
    }

    [Test]
    public void FloorFunction()
    {
        Dictionary<string, MaterialXDataType> input = new()
        {
            ["In"] = MaterialXDataType.Float
        };

        var outputNodeDef = CustomFunctionHelper.ParseHlsl(input, "Out = floor(In);");
        Assert.IsNotNull(outputNodeDef);
    }
    
}
