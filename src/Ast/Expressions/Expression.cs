using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public abstract class Expression
    {
        public TextSpan Span { get; }

        public DataType? DataType { get; set; }

        public Expression(TextSpan span)
        {
            Span = span;
        }

        public abstract T Accept<T>(IExpressionVisitor<T> visitor);
    }
}