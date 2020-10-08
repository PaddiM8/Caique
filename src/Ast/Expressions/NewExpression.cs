using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class NewExpression : IExpression
    {
        public TypeExpression Type { get; }

        public List<IExpression> Arguments { get; }

        public TextSpan Span { get; }

        public NewExpression(TypeExpression type, List<IExpression> arguments,
                             TextSpan span)
        {
            Type = type;
            Arguments = arguments;
            Span = span;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}