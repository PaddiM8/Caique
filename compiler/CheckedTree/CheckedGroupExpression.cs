using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedGroupExpression : CheckedExpression
    {
        public CheckedExpression Expression { get; }

        public CheckedGroupExpression(CheckedExpression expression)
            : base(expression.DataType)
        {
            Expression = expression;
        }
    }
}