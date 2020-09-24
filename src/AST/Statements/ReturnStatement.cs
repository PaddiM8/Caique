using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class ReturnStatement : IStatement
    {
        public IExpression Expression { get; }

        public TextSpan Span { get; }

        public ReturnStatement(IExpression expr, TextSpan span)
        {
            Expression = expr;
            Span = span;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}