using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class UnaryExpression : IExpression
    {
        public Token Operator { get; }

        public IExpression Value { get; }

        public UnaryExpression(Token op, IExpression value)
        {
            Operator = op;
            Value = value;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}