using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public class ReturnStatement : Statement
    {
        public Expression Expression { get; }

        public ReturnStatement(Expression expr, TextSpan span)
            : base(span)
        {
            Expression = expr;
        }

        public override T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}