using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedReturnStatement : CheckedStatement
    {
        public CheckedExpression? Expression { get; }

        public CheckedReturnStatement(CheckedExpression? expression)
        {
            Expression = expression;
        }

        public override CheckedStatement Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedReturnStatement(Expression?.Clone(cloningInfo));
        }
    }
}