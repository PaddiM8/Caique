using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedExpressionStatement : CheckedStatement
    {
        public CheckedExpression Expression { get; }

        public CheckedExpressionStatement(CheckedExpression expression)
        {
            Expression = expression;
        }

        public override CheckedStatement Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedExpressionStatement(Expression.Clone(cloningInfo));
        }
    }
}