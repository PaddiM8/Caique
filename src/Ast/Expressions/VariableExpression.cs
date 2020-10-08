using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class VariableExpression : IExpression
    {
        public Token Identifier { get; }

        public TextSpan Span { get; }

        public VariableExpression(Token identifier)
        {
            Identifier = identifier;
            Span = Identifier.Span;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}