using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class TypeExpression : Expression
    {
        public List<Token> ModulePath { get; }

        public TypeExpression(List<Token> modulePath)
            : base(modulePath[0].Span.Add(modulePath[^1].Span))
        {
            ModulePath = modulePath;
        }
    }
}