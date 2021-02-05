using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public class ReturnStatement : Statement
    {
        public Expression? Expression { get; }

        public ReturnStatement(Expression? expr, TextSpan span)
            : base(span)
        {
            Expression = expr;
        }
    }
}