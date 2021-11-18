using System;
using System.Collections.Generic;
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

        public override CheckedExpression Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedGroupExpression(Expression.Clone(cloningInfo));
        }
    }
}