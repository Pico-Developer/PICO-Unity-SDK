
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    internal abstract class Token
    {
        internal string Content { get; set; }
        internal virtual bool PrecedesPrefix => false;

        internal abstract InputNodeDef Compile(SyntaxNode node, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output);
    }

    internal class Symbol : Token
    {
        internal override InputNodeDef Compile(SyntaxNode node, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> outputs)
        {
            return NodeDefCompiler.CompileSymbol(node, inputs, outputs);
        }
    }

    internal class Literal : Token
    {
        internal override InputNodeDef Compile(SyntaxNode node, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output)
        {
            if ("fF".Contains(Content.Last()))
                Content = Content.Substring(0, Content.Length - 1);
            return new FloatInputNodeDef(MaterialXDataType.Float, float.Parse(Content));
        }
    }

    internal class Operator : Token
    {
        internal enum VariantType
        {
            Default,
            Nullary,
            Prefix,
            FunctionCall,
            VariableDefinition,
        }

        internal VariantType Variant { get; private set; }

        internal override bool PrecedesPrefix => Content switch
        {
            ")" or "]" or "}" => false,
                "++" or "--" => Variant == VariantType.Prefix,
                _ => true,
        };

        // HLSL follows C operator precedence/associativity rules:
        // https://en.cppreference.com/w/c/language/operator_precedence
        internal int Precedence => Content switch
        {
            "." => 1,
                "++" or "--" => (Variant == VariantType.Prefix) ? 2 : 1,
                "!" or "~" => 2,
                "+" or "-" => (Variant == VariantType.Prefix) ? 2 : 4,
                "*" or "/" or "%" => 3,
                "<<" or ">>" => 5,
                "<" or "<=" or ">" or ">=" => 6,
                "==" or "!=" => 7,
                "&" => 8,
                "^" => 9,
                "|" => 10,
                "&&" => 11,
                "||" => 12,
                "?" or ":" => 13,
                "=" or "+=" or "-=" or "*=" or "/=" or "%=" or "<<=" or ">>=" or "&=" or "|=" or "^=" => 14,
                "," => 15,
                ";" => 16,
                "(" or "{" or "[" => int.MaxValue, // Braces are handled as special cases.
                _ => 1,
        };

        internal bool IsRightAssociative => Content switch
        {
            "++" or "--" => Variant == VariantType.Prefix,
                "+" or "-" => Variant == VariantType.Prefix,
                "!" or "~" => true,
                "?" or ":" => true,
                "=" or "+=" or "-=" or "*=" or "/=" or "%=" or "<<=" or ">>=" or "&=" or "|=" or "^=" => true,
                _ => false,
        };

        internal Operator(string content, Token lastToken)
        {
            var lastPrecedesPrefix = lastToken == null || lastToken.PrecedesPrefix;
            Content = content;
            Variant = Content switch
            {
                "+" or "-" or "++" or "--" => lastPrecedesPrefix ? VariantType.Prefix : VariantType.Default,
                    _ => VariantType.Default,
            };
        }

        internal Operator(string content, VariantType variant = VariantType.Default)
        {
            Variant = variant;
            Content = content;
        }
        
        internal bool TakesPrecedenceOver(Operator other)
        {
            var thisPrecedence = this.Precedence;
            var otherPrecedence = other.Precedence;
            return thisPrecedence < otherPrecedence ||
                thisPrecedence == otherPrecedence && !other.IsRightAssociative;
        }

        internal int GetArity(int operandCount)
        {
            return Content switch
            {
                "(" or "[" or "{" => (Variant == VariantType.Nullary) ? 0 : 1,
                    "++" or "--" => 1,
                    "!" or "~" => 1,
                    "+" or "-" => (Variant == VariantType.Prefix) ? 1 : 2,
                    ";" => operandCount == 1 ? 1 : 2,
                    _ => (Variant == VariantType.FunctionCall || Variant == VariantType.VariableDefinition) ? 1 : 2,
            };
        }
        internal override InputNodeDef Compile(SyntaxNode node, Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output)
        {
            return NodeDefCompiler.CompileOperator(node, inputs, output);
        }
    }

    // A node in the abstract syntax tree consisting of an identifying lexeme and zero or more children
    // (zero children for a literal value, one for a unary operation, two for a binary operation, etc.)
    internal class SyntaxNode
    {
        internal Token Token { get; private set; }
        internal List<SyntaxNode> Children { get; private set; }

        internal SyntaxNode(Token token, List<SyntaxNode> children = null)
        {
            Token = token;
            if (children == null)
            {
                Children = new();
                return;
            }
            Children = children
                .SelectMany<SyntaxNode, SyntaxNode>(child =>
                {
                    // Collapse contents of parentheses, separators.
                    if (child.Token is Operator childOp && "(,;".Contains(childOp.Content))
                        return child.Children;
                    else
                        return new[] { child };
                })
                .ToList();
        }

        internal InputNodeDef Compile(Dictionary<string, MaterialXDataType> inputs, Dictionary<string, NodeDef> output)
        {
            return Token.Compile(this, inputs, output);
        }
    }

    internal class Tokenizer
    {
        private string mExpr;

        struct Location
        {
            internal int indexInLine;
            internal int lineBeginIndex;
            internal string line;

            internal char? Character() { return line == null ? null : line[indexInLine]; }

            public Location(int indexInLine = -1, int lineBeginIndex = 0, string line = null)
            {
                this.indexInLine = indexInLine;
                this.lineBeginIndex = lineBeginIndex;
                this.line = line;
            }
        };

        private Location mCurrentLocation = new Location(-1, 0, null);
        internal Tokenizer(string rawInput)
        {

            // Set current position to the first character of the text
            this.mExpr = rawInput;
        }

        // Move to next character
        private void MoveToNextLine()
        {
            mCurrentLocation.line = null;
        }
        private bool MoveToNextLocation()
        {
            while (true)
            {
                if (mCurrentLocation.line == null)
                {
                    if (mCurrentLocation.lineBeginIndex >= mExpr.Length)
                        return false;
                    var nextNewLineIndex = mExpr.IndexOf('\n', mCurrentLocation.lineBeginIndex);
                    var lineEndIndex = (nextNewLineIndex == -1) ? mExpr.Length : nextNewLineIndex;
                    mCurrentLocation.line = mExpr.Substring(mCurrentLocation.lineBeginIndex, lineEndIndex - mCurrentLocation.lineBeginIndex);
                    mCurrentLocation.indexInLine = -1;

                    // Move index to next line
                    mCurrentLocation.lineBeginIndex = lineEndIndex + 1;
                }
                if (mCurrentLocation.indexInLine + 1 < mCurrentLocation.line.Length)
                {
                    mCurrentLocation.indexInLine++;
                    return true;
                }
                else
                    mCurrentLocation.line = null;
            }
        }

        internal char PeekCurrentChar()
        {
            return (char)mCurrentLocation.Character();
        }
        internal char PeekNextChar()
        {
            return mCurrentLocation.indexInLine + 1 < mCurrentLocation.line.Length ? mCurrentLocation.line[mCurrentLocation.indexInLine+1] : '\0';
        }
        internal char PeekNextNextChar()
        {
            return mCurrentLocation.indexInLine + 2 < mCurrentLocation.line.Length ? mCurrentLocation.line[mCurrentLocation.indexInLine+2] : '\0';
        }


        internal Token GetNextToken(Token lastToken)
        {
            while(MoveToNextLocation())
            {
                var ch = mCurrentLocation.Character();
                if (ch == null) continue;
                ch = (char)ch;
                if (char.IsWhiteSpace((char)ch)) continue;
                string lexeme = new("");

                if (ch == '/')
                {
                    switch (PeekNextChar())
                    {
                        case '/': 
                            MoveToNextLine();
                            continue; // single line comment
                        case '*':   
                            while(true)
                            {
                                // Look for matchin '*/'
                                if(!MoveToNextLocation())
                                    throw new Exception("Unmatched comment block");
                                if (mCurrentLocation.Character() == '*' && PeekNextChar() == '/')
                                {
                                    MoveToNextLocation();
                                    break;
                                }
                            }
                            continue; // multiline comments
                    }
                }

                if (ch == '_' || char.IsLetter((char)ch))
                {
                    // Symbols: [_a-zA-z][_a-zA-Z0-9]*
                    string tokenString = new string("");
                    tokenString += PeekCurrentChar();
                    while (PeekNextChar() == '_' || char.IsLetterOrDigit(PeekNextChar()))
                    {
                        MoveToNextLocation();
                        tokenString += mCurrentLocation.Character();
                    }
                    return new Symbol() { Content = tokenString };
                }

                if (char.IsDigit((char)ch) || "+-".Contains((char)ch) &&
                            (char.IsDigit(PeekNextChar()) || PeekNextChar() == '.') ||
                        ch == '.' && char.IsDigit(PeekNextChar()))
                {
                    // Numeric literals: [+-]?([0-9]+(.[0-9]*)?|.[0-9]+)([eE][+-]?[0-9]+)?[fF]?
                    string numericalString = new string("");
                    numericalString += PeekCurrentChar();

                    // We take the first element regardlessly
                    //if ("+-".Contains((char)ch))
                    //{
                    //    //numericalString += (char)ch;
                    //    MoveToNextLocation();
                    //}

                    while (char.IsDigit(PeekNextChar()))
                    {
                        MoveToNextLocation();
                        numericalString += PeekCurrentChar();
                    }
                    if (PeekNextChar() == '.')
                    {
                        MoveToNextLocation();
                        numericalString += PeekCurrentChar();

                        while (char.IsDigit(PeekNextChar()))
                        {
                            MoveToNextLocation();
                            numericalString += PeekCurrentChar();
                        }
                    }
                    if ("eE".Contains(PeekNextChar()))
                    {
                        MoveToNextLocation();
                        numericalString += PeekCurrentChar();

                        if ("+-".Contains(PeekNextChar()))
                        {
                            MoveToNextLocation();
                            numericalString += PeekCurrentChar();
                        }

                        while (char.IsDigit(PeekNextChar()))
                        {
                            MoveToNextLocation();
                            numericalString += PeekCurrentChar();
                        }
                    }
                    if ("fF".Contains(PeekNextChar()))
                    {
                        MoveToNextLocation();
                        numericalString += PeekCurrentChar();
                    }

                    return (new Literal() { Content = numericalString });
                }

                string opStr = new string("");
                if (ch == PeekNextChar() && "<>".Contains((char)ch) && PeekNextChar() == '=')
                {
                    // <<=, >>=
                    opStr += ch;
                    MoveToNextLocation();
                    opStr += PeekCurrentChar();
                    MoveToNextLocation();
                    opStr += PeekCurrentChar();

                    return new Operator(opStr, lastToken);
                }
                else if (PeekCurrentChar() == PeekNextChar() && "+-=<>&|".Contains(PeekCurrentChar()) ||
                        PeekNextChar() == '=' && "+-*/%<>!&|^".Contains(PeekCurrentChar()))
                {
                    // Two-character operators:
                    // ++, --, ==, <<, >>, &&, ||, +=, -=, *=, /=, %=, <=, >=, !=, &=, |=, ^=
                    opStr += PeekCurrentChar();
                    MoveToNextLocation();
                    opStr += PeekCurrentChar();
                    return new Operator(opStr, lastToken);
                }
                else{
                    opStr+= PeekCurrentChar();
                    return new Operator(opStr, lastToken);
                }
            }
            return null;
        }
    }
}
