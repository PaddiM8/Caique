using System;
using Caique.Parsing;

namespace Caique.CheckedTree
{
    public class CheckedExpressionStatement : CheckedStatement
    {
        public CheckedExpression Expression { get; }

        public CheckedExpressionStatement(CheckedExpression expr)
        {
            Expression = expr;
        }
    }
}