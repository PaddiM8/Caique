using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class LiteralExpression : IExpression
    {
        public Token Value { get; }

        public LiteralExpression(Token value)
        {
            Value = value;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}