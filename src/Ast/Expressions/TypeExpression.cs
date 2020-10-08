using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class TypeExpression : IExpression
    {
        public List<Token> ModulePath { get; }

        public TextSpan Span { get; }

        public TypeExpression(List<Token> modulePath)
        {
            ModulePath = modulePath;
            Span = modulePath[0].Span.Add(modulePath[^1].Span);
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}