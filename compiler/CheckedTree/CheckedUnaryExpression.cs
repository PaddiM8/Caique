using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

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

        public override CheckedExpression Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedUnaryExpression(Operator, Value.Clone(cloningInfo));
        }
    }
}