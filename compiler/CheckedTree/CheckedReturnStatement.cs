using System;
using Caique.Parsing;

namespace Caique.CheckedTree
{
    public class CheckedReturnStatement : CheckedStatement
    {
        public CheckedExpression? Expression { get; }

        public CheckedReturnStatement(CheckedExpression? expr)
        {
            Expression = expr;
        }
    }
}