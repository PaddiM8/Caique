using System;
using Caique.Parsing;

namespace Caique.CheckedTree
{
    public class CheckedUnaryExpression : CheckedExpression
    {
        public Token Operator { get; }

        public CheckedExpression Value { get; }

        public CheckedUnaryExpression(Token op, CheckedExpression value)
            : base(value.DataType)
        {
            Operator = op;
            Value = value;
        }
    }
}