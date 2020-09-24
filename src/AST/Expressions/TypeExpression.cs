using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class TypeExpression : IExpression
    {
        public Token Identifier { get; }

        public TextSpan Span { get; }

        public TypeExpression(Token identifier)
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