using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public class LiteralExpression : Expression
    {
        public Token Value { get; }

        public LiteralExpression(Token value)
            : base(value.Span)
        {
            Value = value;
        }

        public override T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}