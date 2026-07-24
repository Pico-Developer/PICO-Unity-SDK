using System.Collections;
using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TokenizerTest
{
    // A Test behaves as an ordinary method
    [Test]
    public void Empty()
    {
        Tokenizer tokenizer = new Tokenizer("");
        string result = "";

        Token lastToken = null;
        for (Token token = tokenizer.GetNextToken(lastToken); token != null; token = tokenizer.GetNextToken(lastToken))
        {
             result += token.Content;
             lastToken = token;
        }
        string resultRef = "";

        Assert.AreEqual(resultRef, result);
    }
    [Test]
    public void Oneliner()
    {
        Tokenizer tokenizer = new Tokenizer("Out = fA + fB * 1.0f;");
        string result = "";
        string resultRef = "Out=fA+fB*1.0f;";

        Token lastToken = null;
        for (Token token = tokenizer.GetNextToken(lastToken); token != null; token = tokenizer.GetNextToken(lastToken))
        {
             result += token.Content;
             lastToken = token;
        }

        Assert.AreEqual(resultRef, result);
    }

    [Test]
    public void OnelineComment()
    {
        Tokenizer tokenizer = new Tokenizer("//Out = fA + fB * 1.0f;");
        string result = "";
        string resultRef = "";

        Token lastToken = null;
        for (Token token = tokenizer.GetNextToken(lastToken); token != null; token = tokenizer.GetNextToken(lastToken))
        {
             result += token.Content;
             lastToken = token;
        }
        
        Assert.AreEqual(resultRef, result);

    }
    [Test]
    public void MultilineComment()
    {
        Tokenizer tokenizer = new Tokenizer("/*Out = fA + fB * 1.0f;\nasdfasdf\n*/");
        string result = "";
        string resultRef = "";

        Token lastToken = null;
        for (Token token = tokenizer.GetNextToken(lastToken); token != null; token = tokenizer.GetNextToken(lastToken))
        {
             result += token.Content;
             lastToken = token;
        }

        Assert.AreEqual(resultRef, result);
    }
    [Test]
    public void Multiline()
    {
        Tokenizer tokenizer = new Tokenizer("Out = fA + fB * 1.0f;\n e = m * c * c");
        string result = "";
        string resultRef = "Out=fA+fB*1.0f;e=m*c*c";
        
        Token lastToken = null;
        for (Token token = tokenizer.GetNextToken(lastToken); token != null; token = tokenizer.GetNextToken(lastToken))
        {
             result += token.Content;
             lastToken = token;
        }

        Assert.AreEqual(resultRef, result);
    }
}
