using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public class UnaryExpression : Expression
    {
        public Token Operator { get; }

        public Expression Value { get; }

        public UnaryExpression(Token op, Expression value)
            : base(op.Span.Add(value.Span))
        {
            Operator = op;
            Value = value;
        }
    }
}