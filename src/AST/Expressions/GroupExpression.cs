using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class GroupExpression : IExpression
    {
        public IExpression Expression { get; }

        public TextSpan Span { get; }

        public GroupExpression(IExpression expression, TextSpan span)
        {
            Expression = expression;
            Span = span;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}